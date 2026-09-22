using PosPipeline.Core.Model;
using PosPipeline.Core.UseCases;
using PosPipeline.Tests.Fakes;
using Xunit;

namespace PosPipeline.Tests.UseCases;

/// <summary>
/// Runs the four use cases in the order the deployed pipeline runs them, wired to in-memory
/// doubles instead of S3, DynamoDB and SQS. It is the closest thing to an end-to-end test
/// that does not need an AWS account, and it is what to run first when changing the flow.
/// </summary>
public class PipelineWalkthroughTests
{
    private const string ExampleMessage = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";

    private readonly InMemoryObjectStore _objectStore = new();
    private readonly InMemoryParsedReportRepository _parsedReports = new();
    private readonly InMemoryCalculationResultRepository _results = new();
    private readonly RecordingCalculationQueue _queue = new();
    private readonly TestLogger _logger = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 4, 15, 12, 30, 123, TimeSpan.Zero));

    [Fact]
    public async Task A_report_flows_from_the_api_through_to_a_status_a_caller_can_read()
    {
        // 1. The API accepts the raw message and hands back the flight ID.
        var ingest = new IngestPosReportUseCase(_objectStore, _clock, _logger);
        var accepted = await ingest.ExecuteAsync(ExampleMessage);

        Assert.Equal("UL20420260904RGNBKK", accepted.FlightId);
        Assert.Equal(FlightStatus.Received, accepted.Status);

        // 2. Before the parser runs, the flight has no status to report.
        var status = new GetFlightStatusUseCase(_parsedReports, _results, _objectStore);
        Assert.Null(await status.ExecuteAsync(accepted.FlightId));

        // 3. The S3 event fires and the parser structures the message.
        var parse = new ParsePosReportUseCase(_objectStore, _parsedReports, _queue, _logger);
        await parse.ExecuteAsync(accepted.ObjectKey);

        var queued = Assert.Single(_queue.Messages);

        // Parsed but not calculated: the flight is known, the numbers are not there yet.
        var parsedStatus = await status.ExecuteAsync(accepted.FlightId);
        Assert.NotNull(parsedStatus);
        Assert.Null(parsedStatus!.RemainingFlightTimeMinutes);

        // 4. The queue triggers the calculator.
        _clock.UtcNow = new DateTimeOffset(2026, 9, 4, 15, 12, 33, 500, TimeSpan.Zero);
        var calculate = new CalculateFlightUseCase(_objectStore, _parsedReports, _results, _clock, _logger);
        await calculate.ExecuteAsync(queued);

        // 5. The caller now sees the calculated status.
        var finalStatus = await status.ExecuteAsync(accepted.FlightId);

        Assert.NotNull(finalStatus);
        Assert.Equal(43, finalStatus!.RemainingFlightTimeMinutes);
        Assert.Equal(10513, finalStatus.EstimatedFuelAtArrivalKg);
        Assert.False(finalStatus.LowFuelWarning);

        // One object per stage: the raw message, the parsed attachment, the result.
        Assert.Equal(3, _objectStore.Objects.Count);
        Assert.StartsWith(StorageKeys.RawPrefix, _objectStore.KeyWithPrefix(StorageKeys.RawPrefix));
        Assert.StartsWith(StorageKeys.AttachmentPrefix, _objectStore.KeyWithPrefix(StorageKeys.AttachmentPrefix));
        Assert.StartsWith(StorageKeys.ResultPrefix, _objectStore.KeyWithPrefix(StorageKeys.ResultPrefix));
    }
}
