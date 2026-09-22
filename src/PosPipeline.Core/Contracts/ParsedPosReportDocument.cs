using PosPipeline.Core.Model;
using PosPipeline.Core.Time;

namespace PosPipeline.Core.Contracts;

/// <summary>
/// The structured POS report stored under the "attachment/" prefix and read back by the
/// calculator. It is the contract between the two Lambdas, so it is kept separate from the
/// <see cref="PosReport"/> domain model and changes only deliberately.
/// </summary>
public sealed class ParsedPosReportDocument
{
    public required string FlightId { get; init; }
    public required string FlightNumber { get; init; }
    public required string Departure { get; init; }
    public required string Destination { get; init; }

    /// <summary>POS report time, ISO-8601 UTC to the second, e.g. "2026-09-04T12:05:00Z".</summary>
    public required string Timestamp { get; init; }

    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double GroundSpeedKnots { get; init; }
    public required double FuelOnBoardKg { get; init; }
    public required double FuelFlowKgPerHour { get; init; }

    public static ParsedPosReportDocument From(PosReport report, string flightId) => new()
    {
        FlightId = flightId,
        FlightNumber = report.Identity.FlightNumber,
        Departure = report.Identity.Departure,
        Destination = report.Identity.Destination,
        Timestamp = Iso8601.ToSeconds(report.Timestamp),
        Latitude = report.Latitude,
        Longitude = report.Longitude,
        GroundSpeedKnots = report.GroundSpeedKnots,
        FuelOnBoardKg = report.FuelOnBoardKg,
        FuelFlowKgPerHour = report.FuelFlowKgPerHour,
    };
}
