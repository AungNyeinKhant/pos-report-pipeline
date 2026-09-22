using PosPipeline.Core.Model;
using PosPipeline.Core.Parsing;
using PosPipeline.Core.Ports;
using PosPipeline.Core.Time;

namespace PosPipeline.Core.UseCases;

/// <summary>What the caller of POST /pos-reports is told, plus the key that was written.</summary>
public sealed record IngestResult(string FlightId, string Status, string ObjectKey);

/// <summary>
/// Accepts a raw POS report: validate its shape, derive the flight ID, store the raw text.
/// Nothing else happens synchronously - the S3 write is what starts the rest of the pipeline.
/// </summary>
public sealed class IngestPosReportUseCase
{
    private readonly IObjectStore _objectStore;
    private readonly IClock _clock;
    private readonly IPipelineLogger _logger;

    public IngestPosReportUseCase(IObjectStore objectStore, IClock clock, IPipelineLogger logger)
    {
        _objectStore = objectStore;
        _clock = clock;
        _logger = logger;
    }

    /// <exception cref="PosReportFormatException">The message is missing or malformed.</exception>
    public async Task<IngestResult> ExecuteAsync(string? rawMessage, CancellationToken cancellationToken = default)
    {
        var receivedAt = _clock.UtcNow;

        // Validates the message shape; anything malformed becomes a 400 in the handler.
        var identity = PosReportParser.ParseIdentity(rawMessage, receivedAt);

        var key = StorageKeys.RawReport(identity.FlightId, receivedAt);
        await _objectStore.PutTextAsync(key, rawMessage!.Trim(), "text/plain", cancellationToken);

        _logger.Info($"Accepted POS report for flight {identity.FlightId}, stored at {key}.");

        return new IngestResult(identity.FlightId, FlightStatus.Received, key);
    }
}
