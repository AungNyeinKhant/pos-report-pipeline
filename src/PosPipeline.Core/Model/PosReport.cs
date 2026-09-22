namespace PosPipeline.Core.Model;

/// <summary>A fully parsed POS report, with position already converted to decimal degrees.</summary>
public sealed record PosReport(
    FlightIdentity Identity,
    DateTimeOffset Timestamp,
    double Latitude,
    double Longitude,
    double GroundSpeedKnots,
    double FuelOnBoardKg,
    double FuelFlowKgPerHour)
{
    public string FlightId => Identity.FlightId;
}
