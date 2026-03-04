namespace CalledAssistant.Services
{
    public interface ILlmProvider : IDisposable
    {
        string ProviderName { get; }
        IAsyncEnumerable<string> ChatStreamAsync(string userMessage, string systemPrompt, CancellationToken ct = default);
        Task<bool> TestConnectionAsync();
    }
}
