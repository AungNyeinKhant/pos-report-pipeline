using PosPipeline.Core.Calculations;
using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.UseCases;
using PosPipeline.Tests.Fakes;
using Xunit;

namespace PosPipeline.Tests.UseCases;

public class CalculateFlightUseCaseTests
{
    private const string FlightId = "UL20420260904RGNBKK";
    private const string ReportTimestamp = "2026-09-04T12:05:00Z";
    private const string AttachmentKey = "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json";

    private static readonly DateTimeOffset ProducedAt = new(2026, 9, 4, 12, 6, 15, 842, TimeSpan.Zero);

    private readonly InMemoryObjectStore _objectStore = new();
    private readonly InMemoryParsedReportRepository _parsedReports = new();
    private readonly InMemoryCalculationResultRepository _results = new();
    private readonly CalculateFlightUseCase _useCase;

    public CalculateFlightUseCaseTests() =>
        _useCase = new CalculateFlightUseCase(
            _objectStore,
            _parsedReports,
            _results,
            new FixedClock(ProducedAt),
            new TestLogger());

    [Fact]
    public async Task Produces_the_worked_example_result()
    {
        GivenParsedReport(destination: "BKK", fuelOnBoardKg: 12500, fuelFlowKgPerHour: 2800);

        var document = await _useCase.ExecuteAsync(Message());

        Assert.Equal(FlightId, document.FlightId);
        Assert.Equal("2026-09-04T12:06:15.842Z", document.Timestamp);
        Assert.Equal(ReportTimestamp, document.ReportTimestamp);
        Assert.Equal(43, document.RemainingFlightTimeMinutes);
        Assert.Equal(10513, document.EstimatedFuelAtArrivalKg);
        Assert.False(document.LowFuelWarning);

        // The result repeats its inputs so it can be read on its own.
        Assert.Equal(16.705, document.Input.CurrentLatitude);
        Assert.Equal(96.2083, document.Input.CurrentLongitude);
        Assert.Equal("BKK", document.Input.Destination);
        Assert.Equal(450, document.Input.GroundSpeedKnots);
        Assert.Equal(12500, document.Input.FuelOnBoardKg);
        Assert.Equal(2800, document.Input.FuelFlowKgPerHour);
    }

    [Fact]
    public async Task Stores_the_result_as_its_own_object_and_row()
    {
        GivenParsedReport(destination: "BKK", fuelOnBoardKg: 12500, fuelFlowKgPerHour: 2800);

        await _useCase.ExecuteAsync(Message());

        const string expectedKey = "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json";
        Assert.True(_objectStore.Objects.ContainsKey(expectedKey));

        var record = Assert.Single(_results.Records);
        Assert.Equal(FlightId, record.FlightId);
        Assert.Equal("2026-09-04T12:06:15.842Z", record.Timestamp);
        Assert.Equal(expectedKey, record.AttachmentKey);
        Assert.Equal(FlightStatus.Calculated, record.Status);
        Assert.Equal(ReportTimestamp, record.ReportTimestamp);
    }

    [Fact]
    public async Task Marks_the_record_when_the_flight_cannot_reach_its_destination_on_the_fuel_on_board()
    {
        GivenParsedReport(destination: "SIN", fuelOnBoardKg: 5000, fuelFlowKgPerHour: 8000);

        var document = await _useCase.ExecuteAsync(Message());

        Assert.True(document.LowFuelWarning);
        Assert.True(document.EstimatedFuelAtArrivalKg < 0);
        Assert.Equal(FlightStatus.LowFuelWarning, Assert.Single(_results.Records).Status);
    }

    [Fact]
    public async Task Fails_when_the_queued_report_is_not_in_the_table()
    {
        // Transient by nature: the write may not have settled yet, so the message is retried
        // and only reaches the dead-letter queue if the record never appears.
        await Assert.ThrowsAsync<ParsedReportNotFoundException>(() => _useCase.ExecuteAsync(Message()));
    }

    [Fact]
    public async Task Fails_when_the_destination_is_not_in_the_airport_directory()
    {
        GivenParsedReport(destination: "ZZZ", fuelOnBoardKg: 12500, fuelFlowKgPerHour: 2800);

        var error = await Assert.ThrowsAsync<UnknownAirportException>(() => _useCase.ExecuteAsync(Message()));

        Assert.Equal("ZZZ", error.AirportCode);
        Assert.Empty(_results.Records);
    }

    private static CalculationQueueMessage Message() =>
        new() { FlightId = FlightId, Timestamp = ReportTimestamp };

    private void GivenParsedReport(string destination, double fuelOnBoardKg, double fuelFlowKgPerHour)
    {
        var document = new ParsedPosReportDocument
        {
            FlightId = FlightId,
            FlightNumber = "UL204",
            Departure = "RGN",
            Destination = destination,
            Timestamp = ReportTimestamp,
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = fuelOnBoardKg,
            FuelFlowKgPerHour = fuelFlowKgPerHour,
        };

        _objectStore.Objects[AttachmentKey] = JsonDefaults.Serialize(document);
        _parsedReports.Records.Add(new ParsedReportRecord(FlightId, ReportTimestamp, AttachmentKey));
    }
}
