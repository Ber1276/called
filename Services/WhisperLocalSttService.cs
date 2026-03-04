using System.IO;
using Whisper.net;
using Whisper.net.Ggml;

namespace CalledAssistant.Services
{
    public class WhisperLocalSttService : ISttService
    {
        private WhisperFactory? _factory;
        private string? _loadedModelPath;
        private readonly SettingsService _settingsService;

        public string ProviderName => "本地 Whisper";

        public WhisperLocalSttService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        private async Task EnsureModelLoadedAsync()
        {
            var modelPath = _settingsService.GetModelPath();

            if (_factory != null && _loadedModelPath == modelPath)
                return;

            // 如果模型文件不存在，则下载
            if (!File.Exists(modelPath))
            {
                await DownloadModelAsync(modelPath);
            }

            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _loadedModelPath = modelPath;
        }

        private async Task DownloadModelAsync(string modelPath)
        {
            var size = _settingsService.Current.Stt.ModelSize;
            await DownloadModelAsync(size, modelPath, null, CancellationToken.None);
        }

        /// <summary>
        /// 下载指定大小的 Whisper 模型到指定路径，支持进度回调。
        /// progress 回调参数：(已下载字节数, 总字节数 or -1)
        /// </summary>
        public static async Task DownloadModelAsync(
            Models.WhisperModelSize size,
            string modelPath,
            IProgress<(long downloaded, long total)>? progress,
            CancellationToken ct)
        {
            var ggmlType = size switch
            {
                Models.WhisperModelSize.Tiny => GgmlType.Tiny,
                Models.WhisperModelSize.Small => GgmlType.Small,
                Models.WhisperModelSize.Medium => GgmlType.Medium,
                Models.WhisperModelSize.LargeV3 => GgmlType.LargeV3,
                _ => GgmlType.Base
            };

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
            using var fileStream = File.OpenWrite(modelPath);

            if (progress == null)
            {
                await modelStream.CopyToAsync(fileStream, ct);
            }
            else
            {
                // 尝试获取总大小
                long total = -1;
                try { total = modelStream.Length; } catch { }

                const int bufferSize = 81920;
                var buffer = new byte[bufferSize];
                long downloaded = 0;
                int read;

                progress.Report((0, total));
                while ((read = await modelStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;
                    progress.Report((downloaded, total));
                }
            }
        }

        public async Task<string> TranscribeAsync(byte[] wavData, string language, CancellationToken ct = default)
        {
            await EnsureModelLoadedAsync();

            using var processor = _factory!.CreateBuilder()
                .WithLanguage(string.IsNullOrEmpty(language) ? "auto" : language)
                .Build();

            using var memStream = new MemoryStream(wavData);

            var segments = new System.Collections.Generic.List<string>();
            await foreach (var segment in processor.ProcessAsync(memStream, ct))
            {
                segments.Add(segment.Text.Trim());
            }

            return string.Join(" ", segments).Trim();
        }

        public Task<bool> TestConnectionAsync()
        {
            try
            {
                var modelPath = _settingsService.GetModelPath();
                if (!File.Exists(modelPath))
                {
                    // 模型文件不存在时返回 true（首次使用会自动下载）
                    return Task.FromResult(true);
                }

                if (_factory == null || _loadedModelPath != modelPath)
                {
                    _factory?.Dispose();
                    _factory = WhisperFactory.FromPath(modelPath);
                    _loadedModelPath = modelPath;
                }
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public void UnloadModel()
        {
            _factory?.Dispose();
            _factory = null;
            _loadedModelPath = null;
        }

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}
