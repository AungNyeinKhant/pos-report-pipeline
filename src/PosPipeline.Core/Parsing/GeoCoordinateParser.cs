using System.Globalization;
using System.Text.RegularExpressions;
using PosPipeline.Core.Model;

namespace PosPipeline.Core.Parsing;

/// <summary>
/// Parses the position block of a POS report, e.g. "N1642.3E09612.5".
///
/// There is no separator between latitude and longitude other than the hemisphere letters,
/// and the two halves have different widths:
///   latitude  = [NS] + 2 degree digits + minutes (mm.m)
///   longitude = [EW] + 3 degree digits + minutes (mm.m)
/// so the block is matched as a whole rather than split on a delimiter.
/// </summary>
public static class GeoCoordinateParser
{
    /// <summary>Decimal places kept for a coordinate. 4 dp is ~11 m, well beyond POS report precision.</summary>
    public const int DecimalPlaces = 4;

    private static readonly Regex BlockPattern = new(
        @"^(?<latHemisphere>[NS])(?<latDegrees>\d{2})(?<latMinutes>\d{2}(?:\.\d+)?)" +
        @"(?<lonHemisphere>[EW])(?<lonDegrees>\d{3})(?<lonMinutes>\d{2}(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Parses a raw position block into decimal degrees.</summary>
    /// <exception cref="PosReportFormatException">The block is malformed or out of range.</exception>
    public static GeoPoint Parse(string block)
    {
        var match = BlockPattern.Match(block ?? string.Empty);
        if (!match.Success)
        {
            throw new PosReportFormatException(
                $"Position block '{block}' is not in the expected {{N|S}}ddmm.m{{E|W}}dddmm.m format.");
        }

        var latitude = ToDecimalDegrees(
            match.Groups["latDegrees"].Value,
            match.Groups["latMinutes"].Value,
            isNegative: match.Groups["latHemisphere"].Value == "S",
            maxAbsolute: 90,
            label: "Latitude");

        var longitude = ToDecimalDegrees(
            match.Groups["lonDegrees"].Value,
            match.Groups["lonMinutes"].Value,
            isNegative: match.Groups["lonHemisphere"].Value == "W",
            maxAbsolute: 180,
            label: "Longitude");

        return new GeoPoint(latitude, longitude);
    }

    /// <summary>decimal = degrees + minutes / 60, negated for the southern/western hemispheres.</summary>
    private static double ToDecimalDegrees(
        string degreesText,
        string minutesText,
        bool isNegative,
        int maxAbsolute,
        string label)
    {
        var degrees = int.Parse(degreesText, CultureInfo.InvariantCulture);
        var minutes = double.Parse(minutesText, CultureInfo.InvariantCulture);

        if (minutes >= 60)
        {
            throw new PosReportFormatException($"{label} minutes '{minutesText}' must be less than 60.");
        }

        var value = degrees + (minutes / 60.0);
        if (value > maxAbsolute)
        {
            throw new PosReportFormatException($"{label} '{degreesText}{minutesText}' is outside +/-{maxAbsolute} degrees.");
        }

        if (isNegative)
        {
            value = -value;
        }

        return Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
