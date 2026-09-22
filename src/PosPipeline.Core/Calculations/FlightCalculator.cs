using PosPipeline.Core.Model;

namespace PosPipeline.Core.Calculations;

/// <summary>
/// Remaining flight time and fuel at arrival.
///
/// A deliberately simplified model: great-circle distance to the
/// destination, flown at the current ground speed, burning fuel at the current flow rate.
/// It ignores wind, routing, climb/descent and reserves, so it is not an operational figure.
/// </summary>
public static class FlightCalculator
{
    public static FlightCalculation Calculate(
        GeoPoint currentPosition,
        GeoPoint destination,
        double groundSpeedKnots,
        double fuelOnBoardKg,
        double fuelFlowKgPerHour)
    {
        if (groundSpeedKnots <= 0)
        {
            // Time to destination is undefined at zero ground speed (e.g. a report sent on stand).
            throw new ArgumentOutOfRangeException(
                nameof(groundSpeedKnots),
                groundSpeedKnots,
                "Ground speed must be greater than zero to estimate remaining flight time.");
        }

        var distanceNauticalMiles = Haversine.DistanceNauticalMiles(currentPosition, destination);
        var remainingFlightTimeHours = distanceNauticalMiles / groundSpeedKnots;
        var estimatedFuelAtArrivalKg = fuelOnBoardKg - (fuelFlowKgPerHour * remainingFlightTimeHours);

        return new FlightCalculation(
            distanceNauticalMiles,
            remainingFlightTimeHours,
            estimatedFuelAtArrivalKg);
    }
}
