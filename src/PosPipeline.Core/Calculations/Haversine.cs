using PosPipeline.Core.Model;

namespace PosPipeline.Core.Calculations;

/// <summary>Great-circle distance between two points on a spherical earth.</summary>
public static class Haversine
{
    public const double EarthRadiusNauticalMiles = 3440.065;

    public static double DistanceNauticalMiles(GeoPoint from, GeoPoint to)
    {
        var dLat = ToRadians(to.Latitude - from.Latitude);
        var dLon = ToRadians(to.Longitude - from.Longitude);

        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
              + (Math.Cos(ToRadians(from.Latitude)) * Math.Cos(ToRadians(to.Latitude))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusNauticalMiles * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
