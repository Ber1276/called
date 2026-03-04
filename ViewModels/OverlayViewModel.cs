using CalledAssistant.Models;
using CalledAssistant.Services;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using InputMode = CalledAssistant.Models.InputMode;

namespace CalledAssistant.ViewModels
{
    public enum OverlayState
    {
        Idle,           // 等待输入
        Recording,      // 正在录音
        Transcribing,   // 语音转文字中
        Thinking,       // LLM 推理中
        Responding,     // 流式显示回答
        Done            // 完成
    }

    public class OverlayViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.AudioCaptureService _audioCapture;
        private Services.ISttService? _sttService;
        private Services.ILlmProvider? _llmProvider;
        private CancellationTokenSource? _cts;

        // -------- 状态属性 --------
        private OverlayState _state = OverlayState.Idle;
        public OverlayState State
        {
            get => _state;
            set
            {
                SetField(ref _state, value);
                OnPropertyChanged(nameof(IsVoiceMode));
                OnPropertyChanged(nameof(IsTextMode));
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(MicIconText));
                OnPropertyChanged(nameof(CanSend));
            }
        }

        public bool IsVoiceMode => InputMode == InputMode.Voice;
        public bool IsTextMode => InputMode == InputMode.Text;
        public bool IsRecording => State == OverlayState.Recording;
        public bool IsProcessing => State is OverlayState.Transcribing or OverlayState.Thinking or OverlayState.Responding;

        public string StatusText => State switch
        {
            OverlayState.Idle => IsVoiceMode ? "按 Space 开始新录音" : "请输入问题",
            OverlayState.Recording => "录音中... 按 Space 停止并识别",
            OverlayState.Transcribing => "正在识别语音...",
            OverlayState.Thinking => "AI 思考中...",
            OverlayState.Responding => "AI 回答中... 按 Space 开始新录音",
            OverlayState.Done => "按 Space 开始新录音，Esc 退出",
            _ => ""
        };

        public string MicIconText => State == OverlayState.Recording ? "⏹" : "🎤";

        // -------- 音量动画 --------
        private float _volumeLevel = 0f;
        public float VolumeLevel
        {
            get => _volumeLevel;
            set => SetField(ref _volumeLevel, value);
        }

        // -------- 输入 --------
        private InputMode _inputMode;
        public InputMode InputMode
        {
            get => _inputMode;
            set
            {
                SetField(ref _inputMode, value);
                OnPropertyChanged(nameof(IsVoiceMode));
                OnPropertyChanged(nameof(IsTextMode));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private string _textInput = string.Empty;
        public string TextInput
        {
            get => _textInput;
            set
            {
                SetField(ref _textInput, value);
                OnPropertyChanged(nameof(CanSend));
            }
        }

        public bool CanSend => !IsProcessing && !string.IsNullOrWhiteSpace(TextInput);

        // -------- 识别/回答 --------
        private string _transcribedText = string.Empty;
        public string TranscribedText
        {
            get => _transcribedText;
            set => SetField(ref _transcribedText, value);
        }

        private string _responseText = string.Empty;
        public string ResponseText
        {
            get => _responseText;
            set
            {
                // 跳过 SetField 的相等性检查（追加场景下值肯定不同，避免额外装箱比较）
                _responseText = value;
                OnPropertyChanged();
            }
        }

        // -------- Commands --------
        public ICommand ToggleMicCommand { get; }
        public ICommand SendTextCommand { get; }
        public ICommand SwitchToVoiceCommand { get; }
        public ICommand SwitchToTextCommand { get; }
        public ICommand CancelCommand { get; }

        public OverlayViewModel(Services.SettingsService settingsService, Services.AudioCaptureService audioCapture)
        {
            _settingsService = settingsService;
            _audioCapture = audioCapture;
            _inputMode = settingsService.Current.InputMode;

            _audioCapture.VolumeChanged += v => Application.Current.Dispatcher.Invoke(() => VolumeLevel = v);
            _audioCapture.RecordingCompleted += OnRecordingCompleted;

            ToggleMicCommand = new RelayCommand(_ => ToggleMic(), _ => !IsProcessing);
            SendTextCommand = new RelayCommandAsync(_ => SendTextAsync(), _ => CanSend);
            SwitchToVoiceCommand = new RelayCommand(_ => SwitchToVoice());
            SwitchToTextCommand = new RelayCommand(_ => SwitchToText());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        public void OnActivated()
        {
            // 重置对话状态
            TranscribedText = string.Empty;
            ResponseText = string.Empty;
            TextInput = string.Empty;

            // 确保使用最新设置
            _inputMode = _settingsService.Current.InputMode;
            OnPropertyChanged(nameof(InputMode));
            OnPropertyChanged(nameof(IsVoiceMode));
            OnPropertyChanged(nameof(IsTextMode));

            // 语音优先时自动开始第一轮录音
            if (_inputMode == InputMode.Voice)
            {
                StartRecording();
            }
            else
            {
                State = OverlayState.Idle;
            }
        }

        private void StartRecording()
        {
            if (State == OverlayState.Recording) return;
            State = OverlayState.Recording;
            _audioCapture.StartRecording();
        }

        private void ToggleMic()
        {
            if (State == OverlayState.Recording)
                _audioCapture.StopRecording();
            else
                StartRecording();
        }

        private void SwitchToVoice()
        {
            InputMode = InputMode.Voice;
            if (State == OverlayState.Idle)
                StartRecording();
        }

        private void SwitchToText()
        {
            if (State == OverlayState.Recording)
                _audioCapture.StopRecording();
            InputMode = InputMode.Text;
            State = OverlayState.Idle;
        }

        private void OnRecordingCompleted(byte[] wavData, NAudio.Wave.WaveFormat format)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Run(() => ProcessVoiceAsync(wavData));
            });
        }

        /// <summary>
        /// 由外部（键盘 Space）调用：录音中则停止并提交，否则开始新一轮录音。
        /// 处理中（转写/LLM）时忽略，避免打断。
        /// </summary>
        public void OnSpacePressed()
        {
            if (InputMode != InputMode.Voice) return;

            switch (State)
            {
                case OverlayState.Recording:
                    // 停止录音 → 触发 OnRecordingCompleted → 转写
                    _audioCapture.StopRecording();
                    break;

                case OverlayState.Idle:
                case OverlayState.Done:
                case OverlayState.Responding:
                    // 取消正在进行的 LLM 流（如 Responding），开始全新一轮
                    _cts?.Cancel();
                    TranscribedText = string.Empty;
                    ResponseText = string.Empty;
                    StartRecording();
                    break;

                // Transcribing / Thinking：忽略，避免打断处理
            }
        }

        private async Task ProcessVoiceAsync(byte[] wavData)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            SetState(OverlayState.Transcribing);
            SetResponseText(string.Empty);

            try
            {
                EnsureSttService();
                var text = await _sttService!.TranscribeAsync(wavData, _settingsService.Current.Stt.Language, _cts.Token);

                Application.Current.Dispatcher.Invoke(
                    () => TranscribedText = text,
                    DispatcherPriority.Normal);

                if (string.IsNullOrWhiteSpace(text))
                {
                    SetState(OverlayState.Idle);
                    return;
                }

                await RunLlmAsync(text, _cts.Token);
            }
            catch (OperationCanceledException) { SetState(OverlayState.Idle); }
            catch (Exception ex)
            {
                SetResponseText($"[错误] {ex.Message}");
                SetState(OverlayState.Done);
            }
        }

        private async Task SendTextAsync()
        {
            var text = TextInput.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            TranscribedText = text;
            TextInput = string.Empty;
            ResponseText = string.Empty;

            try
            {
                // 切到后台线程执行 LLM，避免 UI 线程阻塞
                await Task.Run(() => RunLlmAsync(text, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException) { SetState(OverlayState.Idle); }
            catch (Exception ex)
            {
                SetResponseText($"[错误] {ex.Message}");
                SetState(OverlayState.Done);
            }
        }

        private async Task RunLlmAsync(string userText, CancellationToken ct)
        {
            SetState(OverlayState.Thinking);
            EnsureLlmProvider();

            var systemPrompt = _settingsService.Current.Llm.SystemPrompt;
            bool firstToken = true;

            // 用 StringBuilder 缓冲 token，限速批量推送到 UI
            var buffer = new StringBuilder();
            var lastFlush = DateTime.UtcNow;
            const int FlushIntervalMs = 60; // 约 16fps 刷新

            try
            {
                await foreach (var token in _llmProvider!.ChatStreamAsync(userText, systemPrompt, ct))
                {
                    if (firstToken)
                    {
                        SetState(OverlayState.Responding);
                        firstToken = false;
                    }

                    buffer.Append(token);

                    // 每 60ms 或缓冲超过 200 字符时批量刷新一次
                    var now = DateTime.UtcNow;
                    if ((now - lastFlush).TotalMilliseconds >= FlushIntervalMs || buffer.Length > 200)
                    {
                        var chunk = buffer.ToString();
                        buffer.Clear();
                        lastFlush = now;
                        AppendResponseText(chunk);
                    }
                }

                // 刷新剩余缓冲
                if (buffer.Length > 0)
                    AppendResponseText(buffer.ToString());
            }
            finally
            {
                SetState(OverlayState.Done);
            }
        }

        // 线程安全地追加回答文本（通过 Dispatcher 低优先级调度，不阻塞后台线程）
        private void AppendResponseText(string chunk)
        {
            Application.Current.Dispatcher.InvokeAsync(
                () => ResponseText += chunk,
                DispatcherPriority.Background);
        }

        // 线程安全地设置状态
        private void SetState(OverlayState state)
        {
            Application.Current.Dispatcher.InvokeAsync(
                () => State = state,
                DispatcherPriority.Normal);
        }

        // 线程安全地设置回答文本
        private void SetResponseText(string text)
        {
            Application.Current.Dispatcher.InvokeAsync(
                () => ResponseText = text,
                DispatcherPriority.Normal);
        }

        private void EnsureSttService()
        {
            var provider = _settingsService.Current.Stt.Provider;
            if (_sttService == null || ShouldRecreateStt(provider))
            {
                _sttService?.Dispose();
                _sttService = provider switch
                {
                    Models.SttProvider.OpenAiWhisper => new OpenAiWhisperSttService(_settingsService),
                    _ => new WhisperLocalSttService(_settingsService)
                };
            }
        }

        private bool ShouldRecreateStt(Models.SttProvider provider)
        {
            return provider == Models.SttProvider.OpenAiWhisper && _sttService is not OpenAiWhisperSttService
                || provider == Models.SttProvider.LocalWhisper && _sttService is not WhisperLocalSttService;
        }

        private void EnsureLlmProvider()
        {
            var provider = _settingsService.Current.Llm.Provider;
            if (_llmProvider == null || ShouldRecreateLlm(provider))
            {
                _llmProvider?.Dispose();
                _llmProvider = provider switch
                {
                    Models.LlmProvider.Ollama => new OllamaLlmProvider(_settingsService),
                    _ => new OpenAiCompatibleLlmProvider(_settingsService,
                        provider == Models.LlmProvider.OpenAI ? "OpenAI" : "自定义 OpenAI 兼容")
                };
            }
        }

        private bool ShouldRecreateLlm(Models.LlmProvider provider)
        {
            return provider == Models.LlmProvider.Ollama && _llmProvider is not OllamaLlmProvider
                || provider != Models.LlmProvider.Ollama && _llmProvider is OllamaLlmProvider;
        }

        public void Cancel()
        {
            _cts?.Cancel();
            if (State == OverlayState.Recording)
                _audioCapture.StopRecording();
            State = OverlayState.Idle;
        }

        public void Cleanup()
        {
            Cancel();
            _sttService?.Dispose();
            _llmProvider?.Dispose();
        }
    }
}
