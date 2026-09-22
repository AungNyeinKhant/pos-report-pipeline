namespace PosPipeline.Core.Model;

/// <summary>The states a flight's most recent report can be observed in.</summary>
public static class FlightStatus
{
    /// <summary>Raw message accepted and stored; the parser has not run yet.</summary>
    public const string Received = "RECEIVED";

    /// <summary>Parsed and persisted; the calculator has not produced a result yet.</summary>
    public const string Parsed = "PARSED";

    /// <summary>Calculation complete.</summary>
    public const string Calculated = "CALCULATED";

    /// <summary>Calculation complete and the flight is projected to run out of fuel.</summary>
    public const string LowFuelWarning = "LOW_FUEL_WARNING";
}
