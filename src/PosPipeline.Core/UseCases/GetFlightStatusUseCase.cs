using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.Ports;

namespace PosPipeline.Core.UseCases;

/// <summary>
/// Answers GET /status/{flightId} with the most recent thing known about a flight.
///
/// A calculation result is preferred when one exists; otherwise the latest parsed report is
/// reported, with the calculated fields left out. A flight whose report has been received but
/// not yet parsed has no row in either table yet and returns null, which the handler turns
/// into a 404.
/// </summary>
public sealed class GetFlightStatusUseCase
{
    private readonly IParsedReportRepository _parsedReports;
    private readonly ICalculationResultRepository _results;
    private readonly IObjectStore _objectStore;

    public GetFlightStatusUseCase(
        IParsedReportRepository parsedReports,
        ICalculationResultRepository results,
        IObjectStore objectStore)
    {
        _parsedReports = parsedReports;
        _results = results;
        _objectStore = objectStore;
    }

    public async Task<FlightStatusResponse?> ExecuteAsync(string flightId, CancellationToken cancellationToken = default)
    {
        var latestResult = await _results.GetLatestAsync(flightId, cancellationToken);
        if (latestResult is not null)
        {
            // The row holds only a pointer; the numbers live in the result object itself.
            var resultJson = await _objectStore.GetTextAsync(latestResult.AttachmentKey, cancellationToken);
            var result = JsonDefaults.Deserialize<CalculationResultDocument>(resultJson);

            return new FlightStatusResponse
            {
                FlightId = flightId,
                Timestamp = latestResult.Timestamp,
                RemainingFlightTimeMinutes = result.RemainingFlightTimeMinutes,
                EstimatedFuelAtArrivalKg = result.EstimatedFuelAtArrivalKg,
                LowFuelWarning = result.LowFuelWarning,
            };
        }

        var latestParsed = await _parsedReports.GetLatestAsync(flightId, cancellationToken);
        if (latestParsed is not null)
        {
            return new FlightStatusResponse
            {
                FlightId = flightId,
                Timestamp = latestParsed.Timestamp,
            };
        }

        return null;
    }
}
