using CalledAssistant.Models;
using CalledAssistant.Services;
using System.IO;
using System.Windows;
using System.Windows.Input;
using InputMode = CalledAssistant.Models.InputMode;

namespace CalledAssistant.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _draft;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _draft = CloneSettings(settingsService.Current);

            SaveCommand = new RelayCommand(_ => Save());
            TestSttCommand = new RelayCommandAsync(_ => TestSttAsync());
            TestLlmCommand = new RelayCommandAsync(_ => TestLlmAsync());
            BrowseModelCommand = new RelayCommand(_ => BrowseModel());
            DownloadModelCommand = new RelayCommandAsync(_ => DownloadModelAsync(), _ => !IsDownloading);
            OpenModelFolderCommand = new RelayCommand(_ => OpenModelFolder());

            RefreshModelStatus();
        }

        // -------- 热键 --------
        public bool HotkeyAlt
        {
            get => _draft.Hotkey.Alt;
            set { _draft.Hotkey.Alt = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyPreview)); }
        }
        public bool HotkeyCtrl
        {
            get => _draft.Hotkey.Ctrl;
            set { _draft.Hotkey.Ctrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyPreview)); }
        }
        public bool HotkeyShift
        {
            get => _draft.Hotkey.Shift;
            set { _draft.Hotkey.Shift = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyPreview)); }
        }

        private string _hotkeyKeyDisplay = string.Empty;
        public string HotkeyPreview => _draft.Hotkey.DisplayText;

        // -------- 输入模式 --------
        public bool InputModeVoice
        {
            get => _draft.InputMode == InputMode.Voice;
            set { if (value) { _draft.InputMode = InputMode.Voice; OnPropertyChanged(); OnPropertyChanged(nameof(InputModeDisplay)); } }
        }
        public bool InputModeText
        {
            get => _draft.InputMode == InputMode.Text;
            set { if (value) { _draft.InputMode = InputMode.Text; OnPropertyChanged(); OnPropertyChanged(nameof(InputModeDisplay)); } }
        }
        public string InputModeDisplay => _draft.InputMode == InputMode.Voice ? "语音优先" : "文字优先";

        // -------- STT --------
        public bool SttLocalWhisper
        {
            get => _draft.Stt.Provider == SttProvider.LocalWhisper;
            set { if (value) { _draft.Stt.Provider = SttProvider.LocalWhisper; OnPropertyChanged(); OnPropertyChanged(nameof(SttOpenAi)); } }
        }
        public bool SttOpenAi
        {
            get => _draft.Stt.Provider == SttProvider.OpenAiWhisper;
            set { if (value) { _draft.Stt.Provider = SttProvider.OpenAiWhisper; OnPropertyChanged(); OnPropertyChanged(nameof(SttLocalWhisper)); } }
        }

        public WhisperModelSize WhisperModelSize
        {
            get => _draft.Stt.ModelSize;
            set { _draft.Stt.ModelSize = value; OnPropertyChanged(); RefreshModelStatus(); }
        }
        public string WhisperModelPath
        {
            get => _draft.Stt.ModelPath;
            set { _draft.Stt.ModelPath = value; OnPropertyChanged(); RefreshModelStatus(); }
        }
        public string SttLanguage
        {
            get => _draft.Stt.Language;
            set { _draft.Stt.Language = value; OnPropertyChanged(); }
        }
        public string SttApiKey
        {
            get => _draft.Stt.ApiKey;
            set { _draft.Stt.ApiKey = value; OnPropertyChanged(); }
        }
        public string SttApiEndpoint
        {
            get => _draft.Stt.ApiEndpoint;
            set { _draft.Stt.ApiEndpoint = value; OnPropertyChanged(); }
        }

        public IEnumerable<WhisperModelSize> WhisperModelSizes => Enum.GetValues<WhisperModelSize>();

        // -------- LLM --------
        public LlmProvider LlmProvider
        {
            get => _draft.Llm.Provider;
            set { _draft.Llm.Provider = value; OnPropertyChanged(); }
        }
        public string LlmEndpoint
        {
            get => _draft.Llm.Endpoint;
            set { _draft.Llm.Endpoint = value; OnPropertyChanged(); }
        }
        public string LlmApiKey
        {
            get => _draft.Llm.ApiKey;
            set { _draft.Llm.ApiKey = value; OnPropertyChanged(); }
        }
        public string LlmModelName
        {
            get => _draft.Llm.ModelName;
            set { _draft.Llm.ModelName = value; OnPropertyChanged(); }
        }
        public string LlmSystemPrompt
        {
            get => _draft.Llm.SystemPrompt;
            set { _draft.Llm.SystemPrompt = value; OnPropertyChanged(); }
        }
        public IEnumerable<LlmProvider> LlmProviders => Enum.GetValues<LlmProvider>();

        // -------- 模型下载状态 --------
        private bool _isModelDownloaded;
        public bool IsModelDownloaded
        {
            get => _isModelDownloaded;
            set => SetField(ref _isModelDownloaded, value);
        }

        private string _modelFilePath = string.Empty;
        public string ModelFilePath
        {
            get => _modelFilePath;
            set => SetField(ref _modelFilePath, value);
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                SetField(ref _isDownloading, value);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set => SetField(ref _downloadProgress, value);
        }

        private string _downloadStatus = string.Empty;
        public string DownloadStatus
        {
            get => _downloadStatus;
            set => SetField(ref _downloadStatus, value);
        }

        private CancellationTokenSource? _downloadCts;

        private void RefreshModelStatus()
        {
            // 根据草稿中的模型大小计算默认路径
            var appDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CalledAssistant");

            // 如果用户手动选了文件路径，优先用那个
            string path;
            if (!string.IsNullOrWhiteSpace(_draft.Stt.ModelPath) && File.Exists(_draft.Stt.ModelPath))
            {
                path = _draft.Stt.ModelPath;
            }
            else
            {
                var sizeName = _draft.Stt.ModelSize switch
                {
                    WhisperModelSize.Tiny => "tiny",
                    WhisperModelSize.Small => "small",
                    WhisperModelSize.Medium => "medium",
                    WhisperModelSize.LargeV3 => "large-v3",
                    _ => "base"
                };
                path = System.IO.Path.Combine(appDataDir, $"ggml-{sizeName}.bin");
            }

            ModelFilePath = path;
            IsModelDownloaded = File.Exists(path);
            if (!IsDownloading)
                DownloadStatus = IsModelDownloaded ? "✅ 模型已就绪" : "⚠️ 模型文件不存在，可点击下载";
        }

        private async Task DownloadModelAsync()
        {
            var size = _draft.Stt.ModelSize;
            var sizeName = size switch
            {
                WhisperModelSize.Tiny => "tiny",
                WhisperModelSize.Small => "small",
                WhisperModelSize.Medium => "medium",
                WhisperModelSize.LargeV3 => "large-v3",
                _ => "base"
            };
            var appDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CalledAssistant");
            var modelPath = System.IO.Path.Combine(appDataDir, $"ggml-{sizeName}.bin");

            ModelFilePath = modelPath;
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatus = "准备下载...";
            _downloadCts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<(long downloaded, long total)>(p =>
                {
                    var (downloaded, total) = p;
                    if (total > 0)
                    {
                        DownloadProgress = (int)(downloaded * 100 / total);
                        DownloadStatus = $"下载中... {FormatBytes(downloaded)} / {FormatBytes(total)}";
                    }
                    else
                    {
                        DownloadStatus = $"下载中... {FormatBytes(downloaded)}";
                    }
                });

                await WhisperLocalSttService.DownloadModelAsync(size, modelPath, progress, _downloadCts.Token);

                DownloadProgress = 100;
                IsModelDownloaded = true;
                DownloadStatus = "✅ 下载完成！";

                // 自动填充模型路径
                WhisperModelPath = modelPath;
            }
            catch (OperationCanceledException)
            {
                DownloadStatus = "⚠️ 下载已取消";
                if (File.Exists(modelPath))
                    File.Delete(modelPath);
            }
            catch (Exception ex)
            {
                DownloadStatus = $"❌ 下载失败: {ex.Message}";
                if (File.Exists(modelPath))
                    File.Delete(modelPath);
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        private void OpenModelFolder()
        {
            var folder = System.IO.Path.GetDirectoryName(ModelFilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                System.Diagnostics.Process.Start("explorer.exe", folder);
        }

        // -------- 测试状态 --------
        private string _sttTestResult = string.Empty;
        public string SttTestResult
        {
            get => _sttTestResult;
            set => SetField(ref _sttTestResult, value);
        }

        private string _llmTestResult = string.Empty;
        public string LlmTestResult
        {
            get => _llmTestResult;
            set => SetField(ref _llmTestResult, value);
        }

        // -------- Commands --------
        public ICommand SaveCommand { get; }
        public ICommand TestSttCommand { get; }
        public ICommand TestLlmCommand { get; }
        public ICommand BrowseModelCommand { get; }
        public ICommand DownloadModelCommand { get; }
        public ICommand OpenModelFolderCommand { get; }

        private void Save()
        {
            // 将草稿应用到主设置
            _settingsService.Current.Hotkey = _draft.Hotkey;
            _settingsService.Current.InputMode = _draft.InputMode;
            _settingsService.Current.Stt = _draft.Stt;
            _settingsService.Current.Llm = _draft.Llm;
            _settingsService.Save();

            // 触发热键重新注册（通过事件通知 App）
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? SettingsSaved;

        private async Task TestSttAsync()
        {
            SttTestResult = "测试中...";
            try
            {
                using ISttService svc = _draft.Stt.Provider == SttProvider.OpenAiWhisper
                    ? new OpenAiWhisperSttService(_settingsService)
                    : new WhisperLocalSttService(_settingsService);

                var ok = await svc.TestConnectionAsync();
                SttTestResult = ok ? "✅ 连接成功" : "❌ 连接失败";
            }
            catch (Exception ex)
            {
                SttTestResult = $"❌ 错误: {ex.Message}";
            }
        }

        private async Task TestLlmAsync()
        {
            LlmTestResult = "测试中...";
            try
            {
                using ILlmProvider prov = _draft.Llm.Provider == LlmProvider.Ollama
                    ? new OllamaLlmProvider(_settingsService)
                    : new OpenAiCompatibleLlmProvider(_settingsService);

                var ok = await prov.TestConnectionAsync();
                LlmTestResult = ok ? "✅ 连接成功" : "❌ 连接失败";
            }
            catch (Exception ex)
            {
                LlmTestResult = $"❌ 错误: {ex.Message}";
            }
        }

        private void BrowseModel()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 Whisper GGML 模型文件",
                Filter = "GGML 模型文件 (*.bin)|*.bin|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                WhisperModelPath = dlg.FileName;
            }
        }

        public void HandleKeyCapture(System.Windows.Input.KeyEventArgs e)
        {
            // 排除修饰键本身
            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            if (key is System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
                    or System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
                    or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift)
                return;

            _draft.Hotkey.Alt = (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0;
            _draft.Hotkey.Ctrl = (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            _draft.Hotkey.Shift = (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            _draft.Hotkey.VirtualKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);

            OnPropertyChanged(nameof(HotkeyAlt));
            OnPropertyChanged(nameof(HotkeyCtrl));
            OnPropertyChanged(nameof(HotkeyShift));
            OnPropertyChanged(nameof(HotkeyPreview));
            e.Handled = true;
        }

        private static AppSettings CloneSettings(AppSettings src)
        {
            // 深拷贝，避免修改草稿影响当前运行配置
            var json = System.Text.Json.JsonSerializer.Serialize(src, new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }) ?? new AppSettings();
        }
    }
}
