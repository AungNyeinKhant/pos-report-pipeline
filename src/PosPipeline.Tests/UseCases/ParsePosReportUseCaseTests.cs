using PosPipeline.Core.Contracts;
using PosPipeline.Core.Parsing;
using PosPipeline.Core.UseCases;
using PosPipeline.Tests.Fakes;
using Xunit;

namespace PosPipeline.Tests.UseCases;

public class ParsePosReportUseCaseTests
{
    private const string ExampleMessage = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
    private const string RawKey = "pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z";
    private const string ExpectedAttachmentKey = "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json";

    private readonly InMemoryObjectStore _objectStore = new();
    private readonly InMemoryParsedReportRepository _repository = new();
    private readonly RecordingCalculationQueue _queue = new();
    private readonly TestLogger _logger = new();
    private readonly ParsePosReportUseCase _useCase;

    public ParsePosReportUseCaseTests()
    {
        _useCase = new ParsePosReportUseCase(_objectStore, _repository, _queue, _logger);
        _objectStore.Objects[RawKey] = ExampleMessage;
    }

    [Fact]
    public async Task Writes_the_structured_report_as_a_json_attachment()
    {
        await _useCase.ExecuteAsync(RawKey);

        var document = JsonDefaults.Deserialize<ParsedPosReportDocument>(_objectStore.Objects[ExpectedAttachmentKey]);

        Assert.Equal("UL20420260904RGNBKK", document.FlightId);
        Assert.Equal("UL204", document.FlightNumber);
        Assert.Equal("RGN", document.Departure);
        Assert.Equal("BKK", document.Destination);
        Assert.Equal("2026-09-04T12:05:00Z", document.Timestamp);
        Assert.Equal(16.705, document.Latitude);
        Assert.Equal(96.2083, document.Longitude);
        Assert.Equal(450, document.GroundSpeedKnots);
        Assert.Equal(12500, document.FuelOnBoardKg);
        Assert.Equal(2800, document.FuelFlowKgPerHour);
        Assert.Equal("application/json", _objectStore.ContentTypes[ExpectedAttachmentKey]);
    }

    [Fact]
    public async Task Records_the_report_and_queues_it_for_calculation()
    {
        await _useCase.ExecuteAsync(RawKey);

        var record = Assert.Single(_repository.Records);
        Assert.Equal("UL20420260904RGNBKK", record.FlightId);
        Assert.Equal("2026-09-04T12:05:00Z", record.Timestamp);
        Assert.Equal(ExpectedAttachmentKey, record.AttachmentKey);

        var message = Assert.Single(_queue.Messages);
        Assert.Equal(record.FlightId, message.FlightId);
        Assert.Equal(record.Timestamp, message.Timestamp);
    }

    [Fact]
    public async Task Takes_the_year_and_month_from_the_key_not_from_its_own_clock()
    {
        // The same message parsed a month later still belongs to the September flight,
        // because the receive time is read back from the object key the API wrote.
        const string septemberKey = "pos/UL20420260904RGNBKK-2026-09-30T23:59:59.000Z";
        _objectStore.Objects[septemberKey] = ExampleMessage;

        var document = await _useCase.ExecuteAsync(septemberKey);

        Assert.Equal("UL20420260904RGNBKK", document.FlightId);
        Assert.Equal("2026-09-04T12:05:00Z", document.Timestamp);
    }

    [Fact]
    public async Task Reprocessing_the_same_object_produces_the_same_single_record()
    {
        await _useCase.ExecuteAsync(RawKey);
        await _useCase.ExecuteAsync(RawKey);

        Assert.Single(_repository.Records);
        Assert.Equal(2, _objectStore.Objects.Count); // the raw object and one attachment
    }

    [Theory]
    [InlineData("attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json")]
    [InlineData("pos/UL20420260904RGNBKK")]
    [InlineData("pos/UL20420260904RGNBKK-not-a-timestamp")]
    public async Task Rejects_a_key_it_cannot_read_a_flight_id_and_receive_time_from(string key)
    {
        _objectStore.Objects[key] = ExampleMessage;

        await Assert.ThrowsAsync<PosReportFormatException>(() => _useCase.ExecuteAsync(key));
    }

    [Fact]
    public async Task Warns_when_the_message_disagrees_with_the_flight_id_in_the_key()
    {
        const string mismatchedKey = "pos/XX99920260904RGNBKK-2026-09-04T15:12:30.123Z";
        _objectStore.Objects[mismatchedKey] = ExampleMessage;

        var document = await _useCase.ExecuteAsync(mismatchedKey);

        // The key wins: that is the ID the API already handed back to the caller.
        Assert.Equal("XX99920260904RGNBKK", document.FlightId);
        Assert.Contains(_logger.Messages, message => message.StartsWith("WARN", StringComparison.Ordinal));
    }
}
