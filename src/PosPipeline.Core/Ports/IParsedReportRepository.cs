using PosPipeline.Core.Model;

namespace PosPipeline.Core.Ports;

/// <summary>The parsed-reports table.</summary>
public interface IParsedReportRepository
{
    Task SaveAsync(ParsedReportRecord record, CancellationToken cancellationToken = default);

    Task<ParsedReportRecord?> GetAsync(string flightId, string timestamp, CancellationToken cancellationToken = default);

    /// <summary>The report with the highest timestamp for a flight, or null if it has none.</summary>
    Task<ParsedReportRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default);
}
