using PosPipeline.Core.Parsing;
using Xunit;

namespace PosPipeline.Tests.Parsing;

public class GeoCoordinateParserTests
{
    [Theory]
    // Northern and eastern hemispheres: decimal = degrees + minutes / 60.
    [InlineData("N1642.3E09612.5", 16.705, 96.2083)]
    // Southern and western hemispheres are the same calculation, negated.
    [InlineData("S1642.3W09612.5", -16.705, -96.2083)]
    [InlineData("S1642.3E09612.5", -16.705, 96.2083)]
    [InlineData("N0000.0E00000.0", 0, 0)]
    // Whole minutes and the extremes of each range.
    [InlineData("N9000E18000", 90, 180)]
    [InlineData("S9000W18000", -90, -180)]
    [InlineData("N0121.9E10359.5", 1.365, 103.9917)]
    public void Parse_converts_degrees_and_minutes_to_decimal_degrees(string block, double latitude, double longitude)
    {
        var point = GeoCoordinateParser.Parse(block);

        Assert.Equal(latitude, point.Latitude);
        Assert.Equal(longitude, point.Longitude);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1642.3E09612.5")]       // missing latitude hemisphere
    [InlineData("N1642.3 E09612.5")]     // separator between the halves
    [InlineData("N1642.3X09612.5")]      // unknown longitude hemisphere
    [InlineData("N1642.3E9612.5")]       // longitude degrees must be 3 digits
    [InlineData("N164.3E09612.5")]       // latitude degrees must be 2 digits
    [InlineData("N1660.0E09612.5")]      // 60 minutes is not a valid value
    [InlineData("N9101.0E09612.5")]      // beyond the poles
    [InlineData("N1642.3E18100.0")]      // beyond the antimeridian
    public void Parse_rejects_a_malformed_or_out_of_range_block(string block)
    {
        Assert.Throws<PosReportFormatException>(() => GeoCoordinateParser.Parse(block));
    }
}
