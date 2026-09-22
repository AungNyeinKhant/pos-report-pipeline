using PosPipeline.Core.Calculations;
using PosPipeline.Core.Model;
using Xunit;

namespace PosPipeline.Tests.Calculations;

public class HaversineTests
{
    [Fact]
    public void Distance_between_a_point_and_itself_is_zero()
    {
        var point = new GeoPoint(16.705, 96.2083);

        Assert.Equal(0, Haversine.DistanceNauticalMiles(point, point), 9);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var yangon = new GeoPoint(16.9073, 96.1332);
        var bangkok = new GeoPoint(13.6900, 100.7501);

        Assert.Equal(
            Haversine.DistanceNauticalMiles(yangon, bangkok),
            Haversine.DistanceNauticalMiles(bangkok, yangon),
            9);
    }

    [Fact]
    public void One_degree_of_latitude_is_sixty_nautical_miles()
    {
        // A nautical mile is defined as one minute of arc, so this is a useful sanity check.
        // The spherical earth model puts it at 60.04 nm, hence the whole-mile tolerance.
        var distance = Haversine.DistanceNauticalMiles(new GeoPoint(0, 0), new GeoPoint(1, 0));

        Assert.Equal(60, distance, 0);
    }

    [Fact]
    public void Distance_across_hemispheres_uses_the_signed_coordinates()
    {
        var distance = Haversine.DistanceNauticalMiles(new GeoPoint(-1, 0), new GeoPoint(1, 0));

        Assert.Equal(120, distance, 0);
    }
}
