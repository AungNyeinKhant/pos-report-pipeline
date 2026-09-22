namespace PosPipeline.Core.Model;

/// <summary>
/// A parsed POS report as stored in the parsed-reports table.
/// Partition key: flight ID. Sort key: POS report timestamp.
/// </summary>
public sealed record ParsedReportRecord(string FlightId, string Timestamp, string AttachmentKey);

/// <summary>
/// A calculation as stored in the results table.
/// Partition key: flight ID. Sort key: the time the result was produced.
/// </summary>
/// <param name="Status">CALCULATED, or LOW_FUEL_WARNING when the flight is projected to run dry.</param>
/// <param name="ReportTimestamp">POS report the result was derived from, for traceability.</param>
public sealed record CalculationResultRecord(
    string FlightId,
    string Timestamp,
    string AttachmentKey,
    string Status,
    string ReportTimestamp);
