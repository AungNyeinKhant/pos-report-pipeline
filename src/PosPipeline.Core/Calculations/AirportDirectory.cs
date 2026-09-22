using PosPipeline.Core.Model;

namespace PosPipeline.Core.Calculations;

/// <summary>
/// Destination coordinates by IATA airport code.
///
/// Hardcoded on purpose for this exercise: it is a small, read-mostly reference set, and
/// keeping it in-process avoids a network hop on every calculation. In production this
/// would come from a navigation database loaded at cold start (or a DynamoDB table
/// behind the same interface), which is why lookups go through this one type.
/// </summary>
public static class AirportDirectory
{
    private static readonly IReadOnlyDictionary<string, GeoPoint> Airports =
        new Dictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["RGN"] = new(16.9073, 96.1332),   // Yangon
            ["MDL"] = new(21.7022, 95.9779),   // Mandalay
            ["BKK"] = new(13.6900, 100.7501),  // Bangkok Suvarnabhumi
            ["DMK"] = new(13.9126, 100.6068),  // Bangkok Don Mueang
            ["SIN"] = new(1.3644, 103.9915),   // Singapore Changi
            ["KUL"] = new(2.7456, 101.7072),   // Kuala Lumpur
            ["CMB"] = new(7.1808, 79.8841),    // Colombo
            ["DXB"] = new(25.2532, 55.3657),   // Dubai
            ["HKG"] = new(22.3080, 113.9185),  // Hong Kong
            ["SGN"] = new(10.8188, 106.6520),  // Ho Chi Minh City
            ["DAC"] = new(23.8433, 90.3978),   // Dhaka
            ["CGK"] = new(-6.1256, 106.6559),  // Jakarta
            ["MNL"] = new(14.5086, 121.0198),  // Manila
            ["ICN"] = new(37.4602, 126.4407),  // Seoul Incheon
        };

    public static bool TryGetLocation(string? airportCode, out GeoPoint location)
    {
        location = default;
        return airportCode is not null && Airports.TryGetValue(airportCode, out location);
    }

    /// <exception cref="UnknownAirportException">The code is not in the directory.</exception>
    public static GeoPoint GetLocation(string airportCode) =>
        TryGetLocation(airportCode, out var location)
            ? location
            : throw new UnknownAirportException(airportCode);
}
