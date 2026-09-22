using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.Parsing;
using PosPipeline.Core.Ports;
using PosPipeline.Core.Time;

namespace PosPipeline.Core.UseCases;

/// <summary>
/// Turns a raw POS report object into a structured report: writes the JSON attachment,
/// records it in the parsed-reports table, and queues it for calculation.
///
/// Every write is keyed by (flight ID, report timestamp), so re-running this for the same
/// object - which S3 event delivery can cause - overwrites rather than duplicates.
/// </summary>
public sealed class ParsePosReportUseCase
{
    private readonly IObjectStore _objectStore;
    private readonly IParsedReportRepository _repository;
    private readonly ICalculationQueue _queue;
    private readonly IPipelineLogger _logger;

    public ParsePosReportUseCase(
        IObjectStore objectStore,
        IParsedReportRepository repository,
        ICalculationQueue queue,
        IPipelineLogger logger)
    {
        _objectStore = objectStore;
        _repository = repository;
        _queue = queue;
        _logger = logger;
    }

    /// <exception cref="PosReportFormatException">The object key or message is malformed.</exception>
    public async Task<ParsedPosReportDocument> ExecuteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!StorageKeys.TryParseRawReport(objectKey, out var flightId, out var receivedAt))
        {
            throw new PosReportFormatException(
                $"Object key '{objectKey}' is not a '{StorageKeys.RawPrefix}{{flightId}}-{{timestamp}}' key.");
        }

        var rawMessage = await _objectStore.GetTextAsync(objectKey, cancellationToken);

        // The receive time comes from the key, not from this Lambda's clock, so the year and
        // month filled in for the report are the same ones the API used to build the flight ID.
        var report = PosReportParser.Parse(rawMessage, receivedAt);

        if (!string.Equals(report.FlightId, flightId, StringComparison.Ordinal))
        {
            _logger.Warn(
                $"Flight ID from key '{flightId}' differs from the one derived from the message " +
                $"'{report.FlightId}'; keeping the key's value, which was returned to the caller.");
        }

        var document = ParsedPosReportDocument.From(report, flightId);
        var attachmentKey = StorageKeys.Attachment(flightId, report.Timestamp);

        await _objectStore.PutTextAsync(
            attachmentKey,
            JsonDefaults.Serialize(document),
            "application/json",
            cancellationToken);

        await _repository.SaveAsync(
            new ParsedReportRecord(flightId, document.Timestamp, attachmentKey),
            cancellationToken);

        await _queue.PublishAsync(
            new CalculationQueueMessage { FlightId = flightId, Timestamp = document.Timestamp },
            cancellationToken);

        _logger.Info($"Parsed {objectKey} into {attachmentKey} and queued flight {flightId} for calculation.");

        return document;
    }
}
