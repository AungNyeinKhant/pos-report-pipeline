using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.UseCases;
using PosPipeline.Tests.Fakes;
using Xunit;

namespace PosPipeline.Tests.UseCases;

public class GetFlightStatusUseCaseTests
{
    private const string FlightId = "UL20420260904RGNBKK";

    private readonly InMemoryObjectStore _objectStore = new();
    private readonly InMemoryParsedReportRepository _parsedReports = new();
    private readonly InMemoryCalculationResultRepository _results = new();
    private readonly GetFlightStatusUseCase _useCase;

    public GetFlightStatusUseCaseTests() =>
        _useCase = new GetFlightStatusUseCase(_parsedReports, _results, _objectStore);

    [Fact]
    public async Task Returns_null_for_a_flight_with_no_records_yet()
    {
        // The handler turns this into a 404, which is also what a caller sees in the moment
        // between the API accepting a report and the parser writing it down.
        Assert.Null(await _useCase.ExecuteAsync(FlightId));
    }

    [Fact]
    public async Task Omits_the_calculated_fields_while_the_calculation_is_still_in_flight()
    {
        _parsedReports.Records.Add(new ParsedReportRecord(FlightId, "2026-09-04T12:05:00Z", "attachment/a.json"));

        var status = await _useCase.ExecuteAsync(FlightId);

        Assert.NotNull(status);
        Assert.Equal("2026-09-04T12:05:00Z", status!.Timestamp);

        // No calculation yet, so the calculated fields are absent rather than zeroed.
        Assert.Null(status.RemainingFlightTimeMinutes);
        Assert.Null(status.EstimatedFuelAtArrivalKg);
        Assert.Null(status.LowFuelWarning);
    }

    [Fact]
    public async Task Reports_the_most_recent_calculation_once_one_exists()
    {
        _parsedReports.Records.Add(new ParsedReportRecord(FlightId, "2026-09-04T12:05:00Z", "attachment/a.json"));
        GivenResult("2026-09-04T12:06:15.842Z", remainingMinutes: 43, fuelAtArrivalKg: 10513, lowFuel: false);
        GivenResult("2026-09-04T12:16:03.100Z", remainingMinutes: 31, fuelAtArrivalKg: 10050, lowFuel: false);

        var status = await _useCase.ExecuteAsync(FlightId);

        Assert.NotNull(status);
        Assert.Equal("2026-09-04T12:16:03.100Z", status!.Timestamp);
        Assert.Equal(31, status.RemainingFlightTimeMinutes);
        Assert.Equal(10050, status.EstimatedFuelAtArrivalKg);
        Assert.False(status.LowFuelWarning);
    }

    [Fact]
    public async Task Surfaces_a_low_fuel_warning_in_the_status()
    {
        GivenResult("2026-09-04T12:06:15.842Z", remainingMinutes: 137, fuelAtArrivalKg: -3200, lowFuel: true);

        var status = await _useCase.ExecuteAsync(FlightId);

        Assert.NotNull(status);
        Assert.True(status!.LowFuelWarning);
        Assert.Equal(-3200, status.EstimatedFuelAtArrivalKg);
    }

    private void GivenResult(string timestamp, int remainingMinutes, double fuelAtArrivalKg, bool lowFuel)
    {
        var key = $"results/{FlightId}-{timestamp}.json";

        _objectStore.Objects[key] = JsonDefaults.Serialize(new CalculationResultDocument
        {
            FlightId = FlightId,
            Timestamp = timestamp,
            ReportTimestamp = "2026-09-04T12:05:00Z",
            Input = new CalculationInputDocument
            {
                CurrentLatitude = 16.705,
                CurrentLongitude = 96.2083,
                Destination = "BKK",
                DestinationLatitude = 13.69,
                DestinationLongitude = 100.7501,
                GroundSpeedKnots = 450,
                FuelOnBoardKg = 12500,
                FuelFlowKgPerHour = 2800,
            },
            DistanceNauticalMiles = 319.4,
            RemainingFlightTimeMinutes = remainingMinutes,
            EstimatedFuelAtArrivalKg = fuelAtArrivalKg,
            LowFuelWarning = lowFuel,
        });

        _results.Records.Add(new CalculationResultRecord(
            FlightId,
            timestamp,
            key,
            lowFuel ? FlightStatus.LowFuelWarning : FlightStatus.Calculated,
            "2026-09-04T12:05:00Z"));
    }
}
