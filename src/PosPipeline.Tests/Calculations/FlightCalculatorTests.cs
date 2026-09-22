using PosPipeline.Core.Calculations;
using PosPipeline.Core.Model;
using Xunit;

namespace PosPipeline.Tests.Calculations;

public class FlightCalculatorTests
{
    private static readonly GeoPoint ExamplePosition = new(16.705, 96.2083);

    [Fact]
    public void Calculate_reproduces_the_worked_example()
    {
        var calculation = FlightCalculator.Calculate(
            ExamplePosition,
            AirportDirectory.GetLocation("BKK"),
            groundSpeedKnots: 450,
            fuelOnBoardKg: 12500,
            fuelFlowKgPerHour: 2800);

        Assert.Equal(319.4, Math.Round(calculation.DistanceNauticalMiles, 1));
        Assert.Equal(43, calculation.RemainingFlightTimeMinutes);
        Assert.Equal(10513, calculation.EstimatedFuelAtArrivalKgRounded);
        Assert.False(calculation.LowFuelWarning);
    }

    [Fact]
    public void Calculate_flags_a_flight_that_runs_out_of_fuel_before_arrival()
    {
        // Yangon to Singapore is ~1030 nm; at 450 kt that is over two hours of flying,
        // which this aircraft does not have the fuel for.
        var calculation = FlightCalculator.Calculate(
            ExamplePosition,
            AirportDirectory.GetLocation("SIN"),
            groundSpeedKnots: 450,
            fuelOnBoardKg: 5000,
            fuelFlowKgPerHour: 8000);

        Assert.True(calculation.EstimatedFuelAtArrivalKg < 0);
        Assert.True(calculation.LowFuelWarning);
    }

    [Fact]
    public void Calculate_returns_zero_distance_when_the_aircraft_is_over_the_destination()
    {
        var calculation = FlightCalculator.Calculate(
            AirportDirectory.GetLocation("BKK"),
            AirportDirectory.GetLocation("BKK"),
            groundSpeedKnots: 450,
            fuelOnBoardKg: 12500,
            fuelFlowKgPerHour: 2800);

        Assert.Equal(0, calculation.DistanceNauticalMiles, 6);
        Assert.Equal(0, calculation.RemainingFlightTimeMinutes);
        Assert.Equal(12500, calculation.EstimatedFuelAtArrivalKgRounded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Calculate_rejects_a_ground_speed_that_makes_the_estimate_meaningless(double groundSpeedKnots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FlightCalculator.Calculate(
            ExamplePosition,
            AirportDirectory.GetLocation("BKK"),
            groundSpeedKnots,
            fuelOnBoardKg: 12500,
            fuelFlowKgPerHour: 2800));
    }
}
