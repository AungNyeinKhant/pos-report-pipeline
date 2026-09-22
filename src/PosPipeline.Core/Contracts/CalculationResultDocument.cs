namespace PosPipeline.Core.Contracts;

/// <summary>
/// The calculation result stored under the "results/" prefix. It repeats the inputs so the
/// object is self-contained: anyone reading it can see what the numbers were derived from
/// without joining back to the parsed report.
/// </summary>
public sealed class CalculationResultDocument
{
    public required string FlightId { get; init; }

    /// <summary>When this result was produced, ISO-8601 UTC with milliseconds.</summary>
    public required string Timestamp { get; init; }

    /// <summary>The POS report timestamp these inputs came from.</summary>
    public required string ReportTimestamp { get; init; }

    public required CalculationInputDocument Input { get; init; }

    public required double DistanceNauticalMiles { get; init; }
    public required int RemainingFlightTimeMinutes { get; init; }
    public required double EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
}

/// <summary>The inputs a calculation was run with.</summary>
public sealed class CalculationInputDocument
{
    public required double CurrentLatitude { get; init; }
    public required double CurrentLongitude { get; init; }
    public required string Destination { get; init; }
    public required double DestinationLatitude { get; init; }
    public required double DestinationLongitude { get; init; }
    public required double GroundSpeedKnots { get; init; }
    public required double FuelOnBoardKg { get; init; }
    public required double FuelFlowKgPerHour { get; init; }
}
