namespace PosPipeline.Core.Ports;

/// <summary>Blob storage for raw messages, parsed reports and results (S3 in production).</summary>
public interface IObjectStore
{
    Task PutTextAsync(string key, string content, string contentType, CancellationToken cancellationToken = default);

    Task<string> GetTextAsync(string key, CancellationToken cancellationToken = default);
}
