namespace PosPipeline.Core.Model;

/// <summary>Output of the remaining-flight-time / fuel-at-arrival calculation.</summary>
public sealed record FlightCalculation(
    double DistanceNauticalMiles,
    double RemainingFlightTimeHours,
    double EstimatedFuelAtArrivalKg)
{
    /// <summary>Whole minutes remaining, which is the unit the API reports.</summary>
    public int RemainingFlightTimeMinutes =>
        (int)Math.Round(RemainingFlightTimeHours * 60, MidpointRounding.AwayFromZero);

    /// <summary>Fuel at arrival rounded to whole kilograms, which is the unit the API reports.</summary>
    public double EstimatedFuelAtArrivalKgRounded =>
        Math.Round(EstimatedFuelAtArrivalKg, MidpointRounding.AwayFromZero);

    /// <summary>True when the flight is projected to arrive with less than no fuel.</summary>
    public bool LowFuelWarning => EstimatedFuelAtArrivalKg < 0;
}
