using PosPipeline.Core.Model;

namespace PosPipeline.Core.Ports;

/// <summary>The calculation-results table.</summary>
public interface ICalculationResultRepository
{
    Task SaveAsync(CalculationResultRecord record, CancellationToken cancellationToken = default);

    /// <summary>The most recent calculation for a flight, or null if it has none.</summary>
    Task<CalculationResultRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default);
}
