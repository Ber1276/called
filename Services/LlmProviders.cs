using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CalledAssistant.Services
{
    /// <summary>
    /// Ollama 本地 LLM（/api/chat 接口）
    /// </summary>
    public class OllamaLlmProvider : ILlmProvider
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public string ProviderName => "Ollama 本地";

        public OllamaLlmProvider(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            string userMessage,
            string systemPrompt,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var settings = _settingsService.Current.Llm;
            var endpoint = $"{settings.Endpoint.TrimEnd('/')}/api/chat";

            var body = new
            {
                model = settings.ModelName,
                stream = true,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userMessage }
                }
            };

            var json = JsonSerializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            string? errorMsg = null;
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                errorMsg = $"[错误] 无法连接 Ollama: {ex.Message}";
            }

            if (errorMsg != null || response == null)
            {
                yield return errorMsg ?? "[错误] 未知错误";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? token = null;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("content", out var content))
                    {
                        token = content.GetString();
                    }

                    if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                        break;
                }
                catch { /* 解析失败跳过 */ }

                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var settings = _settingsService.Current.Llm;
                var endpoint = $"{settings.Endpoint.TrimEnd('/')}/api/tags";
                var response = await _httpClient.GetAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose() => _httpClient.Dispose();
    }

    /// <summary>
    /// OpenAI 兼容接口（适用于 OpenAI、各类兼容服务）
    /// </summary>
    public class OpenAiCompatibleLlmProvider : ILlmProvider
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public string ProviderName { get; }

        public OpenAiCompatibleLlmProvider(SettingsService settingsService, string providerName = "OpenAI")
        {
            _settingsService = settingsService;
            ProviderName = providerName;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            string userMessage,
            string systemPrompt,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var settings = _settingsService.Current.Llm;
            var baseUrl = settings.Endpoint.TrimEnd('/');
            var endpoint = baseUrl.EndsWith("/v1") ? $"{baseUrl}/chat/completions"
                         : baseUrl.Contains("/v1/") ? baseUrl
                         : $"{baseUrl}/v1/chat/completions";

            var body = new
            {
                model = settings.ModelName,
                stream = true,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userMessage }
                }
            };

            var json = JsonSerializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            string? errorMsgOai = null;
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                errorMsgOai = $"[错误] 请求失败: {ex.Message}";
            }

            if (errorMsgOai != null || response == null)
            {
                yield return errorMsgOai ?? "[错误] 未知错误";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..].Trim();
                if (data == "[DONE]") break;

                string? token = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var content))
                        token = content.GetString();
                }
                catch { /* 解析失败跳过 */ }

                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var settings = _settingsService.Current.Llm;
                var baseUrl = settings.Endpoint.TrimEnd('/');
                var endpoint = baseUrl.EndsWith("/v1") ? $"{baseUrl}/models"
                             : $"{baseUrl}/v1/models";

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
