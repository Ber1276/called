namespace CalledAssistant.Services
{
    public interface ISttService : IDisposable
    {
        Task<string> TranscribeAsync(byte[] wavData, string language, CancellationToken ct = default);
        Task<bool> TestConnectionAsync();
        string ProviderName { get; }
    }
}
