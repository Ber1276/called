using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CalledAssistant.Services
{
    public class OpenAiWhisperSttService : ISttService
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public string ProviderName => "OpenAI Whisper API";

        public OpenAiWhisperSttService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        public async Task<string> TranscribeAsync(byte[] wavData, string language, CancellationToken ct = default)
        {
            var settings = _settingsService.Current.Stt;
            var endpoint = string.IsNullOrWhiteSpace(settings.ApiEndpoint)
                ? "https://api.openai.com/v1/audio/transcriptions"
                : settings.ApiEndpoint;

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(wavData);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", "audio.wav");
            content.Add(new StringContent("whisper-1"), "model");

            if (!string.IsNullOrWhiteSpace(language))
                content.Add(new StringContent(language), "language");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // 发送一个简单的空白 wav 测试连接
                var settings = _settingsService.Current.Stt;
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                    return false;

                // 构建静音测试音频（1秒 16kHz 16bit 单声道）
                var silentWav = CreateSilentWav(16000, 1);
                var result = await TranscribeAsync(silentWav, "zh");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] CreateSilentWav(int sampleRate, int seconds)
        {
            int samples = sampleRate * seconds;
            using var ms = new MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + samples * 2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(samples * 2);
            for (int i = 0; i < samples; i++) bw.Write((short)0);

            return ms.ToArray();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
