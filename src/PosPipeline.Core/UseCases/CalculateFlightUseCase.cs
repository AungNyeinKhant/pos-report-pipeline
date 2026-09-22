using PosPipeline.Core.Calculations;
using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.Ports;
using PosPipeline.Core.Time;

namespace PosPipeline.Core.UseCases;

/// <summary>
/// Raised when the queue references a parsed report that is not in the table. Retrying can
/// help (the write may not have settled yet), so this is treated as transient.
/// </summary>
public sealed class ParsedReportNotFoundException : Exception
{
    public ParsedReportNotFoundException(string flightId, string timestamp)
        : base($"No parsed report found for flight {flightId} at {timestamp}.")
    {
    }
}

/// <summary>
/// Estimates remaining flight time and fuel at arrival for one parsed POS report, then
/// stores the result as its own S3 object and a row in the results table.
/// </summary>
public sealed class CalculateFlightUseCase
{
    private readonly IObjectStore _objectStore;
    private readonly IParsedReportRepository _parsedReports;
    private readonly ICalculationResultRepository _results;
    private readonly IClock _clock;
    private readonly IPipelineLogger _logger;

    public CalculateFlightUseCase(
        IObjectStore objectStore,
        IParsedReportRepository parsedReports,
        ICalculationResultRepository results,
        IClock clock,
        IPipelineLogger logger)
    {
        _objectStore = objectStore;
        _parsedReports = parsedReports;
        _results = results;
        _clock = clock;
        _logger = logger;
    }

    /// <exception cref="ParsedReportNotFoundException">The referenced parsed report is missing.</exception>
    /// <exception cref="UnknownAirportException">The destination is not in the airport directory.</exception>
    public async Task<CalculationResultDocument> ExecuteAsync(
        CalculationQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        var record = await _parsedReports.GetAsync(message.FlightId, message.Timestamp, cancellationToken)
            ?? throw new ParsedReportNotFoundException(message.FlightId, message.Timestamp);

        var reportJson = await _objectStore.GetTextAsync(record.AttachmentKey, cancellationToken);
        var report = JsonDefaults.Deserialize<ParsedPosReportDocument>(reportJson);

        var destination = AirportDirectory.GetLocation(report.Destination);

        var calculation = FlightCalculator.Calculate(
            new GeoPoint(report.Latitude, report.Longitude),
            destination,
            report.GroundSpeedKnots,
            report.FuelOnBoardKg,
            report.FuelFlowKgPerHour);

        var producedAt = _clock.UtcNow;
        var resultKey = StorageKeys.Result(message.FlightId, producedAt);

        var document = new CalculationResultDocument
        {
            FlightId = message.FlightId,
            Timestamp = Iso8601.ToMilliseconds(producedAt),
            ReportTimestamp = report.Timestamp,
            Input = new CalculationInputDocument
            {
                CurrentLatitude = report.Latitude,
                CurrentLongitude = report.Longitude,
                Destination = report.Destination,
                DestinationLatitude = destination.Latitude,
                DestinationLongitude = destination.Longitude,
                GroundSpeedKnots = report.GroundSpeedKnots,
                FuelOnBoardKg = report.FuelOnBoardKg,
                FuelFlowKgPerHour = report.FuelFlowKgPerHour,
            },
            DistanceNauticalMiles = Math.Round(calculation.DistanceNauticalMiles, 1, MidpointRounding.AwayFromZero),
            RemainingFlightTimeMinutes = calculation.RemainingFlightTimeMinutes,
            EstimatedFuelAtArrivalKg = calculation.EstimatedFuelAtArrivalKgRounded,
            LowFuelWarning = calculation.LowFuelWarning,
        };

        await _objectStore.PutTextAsync(
            resultKey,
            JsonDefaults.Serialize(document),
            "application/json",
            cancellationToken);

        var status = calculation.LowFuelWarning ? FlightStatus.LowFuelWarning : FlightStatus.Calculated;

        await _results.SaveAsync(
            new CalculationResultRecord(message.FlightId, document.Timestamp, resultKey, status, report.Timestamp),
            cancellationToken);

        if (calculation.LowFuelWarning)
        {
            _logger.Warn(
                $"Flight {message.FlightId} is projected to arrive at {report.Destination} with " +
                $"{document.EstimatedFuelAtArrivalKg} kg of fuel.");
        }
        else
        {
            _logger.Info(
                $"Calculated flight {message.FlightId}: {document.RemainingFlightTimeMinutes} minutes and " +
                $"{document.EstimatedFuelAtArrivalKg} kg remaining at {report.Destination}.");
        }

        return document;
    }
}
