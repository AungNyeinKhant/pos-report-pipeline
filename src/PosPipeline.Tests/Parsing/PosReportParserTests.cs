using PosPipeline.Core.Parsing;
using PosPipeline.Core.Time;
using Xunit;

namespace PosPipeline.Tests.Parsing;

public class PosReportParserTests
{
    private const string ExampleMessage = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";

    /// <summary>The message carries no year or month, so both come from this instant.</summary>
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 4, 15, 12, 30, 123, TimeSpan.Zero);

    [Fact]
    public void Parse_reads_every_field_of_the_example_message()
    {
        var report = PosReportParser.Parse(ExampleMessage, ReceivedAt);

        Assert.Equal("UL204", report.Identity.FlightNumber);
        Assert.Equal("RGN", report.Identity.Departure);
        Assert.Equal("BKK", report.Identity.Destination);
        Assert.Equal("UL20420260904RGNBKK", report.FlightId);
        Assert.Equal("2026-09-04T12:05:00Z", Iso8601.ToSeconds(report.Timestamp));
        Assert.Equal(16.705, report.Latitude);
        Assert.Equal(96.2083, report.Longitude);
        Assert.Equal(450, report.GroundSpeedKnots);
        Assert.Equal(12500, report.FuelOnBoardKg);
        Assert.Equal(2800, report.FuelFlowKgPerHour);
    }

    [Fact]
    public void ParseIdentity_builds_the_flight_id_from_the_message_and_the_receive_date()
    {
        var identity = PosReportParser.ParseIdentity(ExampleMessage, ReceivedAt);

        Assert.Equal("UL20420260904RGNBKK", identity.FlightId);
        Assert.Equal(new DateOnly(2026, 9, 4), identity.FlightDate);
    }

    [Fact]
    public void ParseIdentity_ignores_surrounding_whitespace_and_casing()
    {
        var identity = PosReportParser.ParseIdentity($"  {ExampleMessage.ToLowerInvariant()}\r\n", ReceivedAt);

        Assert.Equal("UL20420260904RGNBKK", identity.FlightId);
    }

    [Fact]
    public void Parse_accepts_a_two_character_alphanumeric_airline_code()
    {
        var report = PosReportParser.Parse(
            "POS/3K21.FR SIN/TO KUL/041205/N0121.9E10359.5/380/8000/2100",
            ReceivedAt);

        Assert.Equal("3K21", report.Identity.FlightNumber);
        Assert.Equal("3K2120260904SINKUL", report.FlightId);
    }

    [Theory]
    [InlineData(null, "empty")]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("ACK/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800", "must start with")]
    [InlineData("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500", "segments")]
    [InlineData("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800/EXTRA", "segments")]
    [InlineData("POS/UL204 RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800", "flight and departure")]
    [InlineData("POS/UL204.FR RGN/BKK/041205/N1642.3E09612.5/450/12500/2800", "destination")]
    [InlineData("POS/UL204.FR RGN/TO BKK/4125/N1642.3E09612.5/450/12500/2800", "day and time")]
    [InlineData("POS/UL204.FR RGN/TO BKK/042505/N1642.3E09612.5/450/12500/2800", "day and time")]
    [InlineData("POS/UL204.FR RGN/TO BKK/041275/N1642.3E09612.5/450/12500/2800", "day and time")]
    public void ParseIdentity_rejects_malformed_messages(string? message, string expectedFragment)
    {
        var error = Assert.Throws<PosReportFormatException>(() => PosReportParser.ParseIdentity(message, ReceivedAt));

        Assert.Contains(expectedFragment, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/FAST/12500/2800", "ground speed")]
    [InlineData("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/-12500/2800", "fuel on board")]
    [InlineData("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800KG", "fuel flow")]
    public void Parse_rejects_malformed_measurements(string message, string expectedFragment)
    {
        var error = Assert.Throws<PosReportFormatException>(() => PosReportParser.Parse(message, ReceivedAt));

        Assert.Contains(expectedFragment, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseIdentity_accepts_a_message_the_full_parse_would_reject()
    {
        // The ingest API only validates the shape and the fields it needs; a bad speed field
        // is the parser's problem, not a 400.
        var identity = PosReportParser.ParseIdentity(
            "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/FAST/12500/2800",
            ReceivedAt);

        Assert.Equal("UL20420260904RGNBKK", identity.FlightId);
    }

    [Fact]
    public void ResolveFlightDate_takes_the_year_and_month_from_the_receive_time()
    {
        var date = PosReportParser.ResolveFlightDate(4, new DateTimeOffset(2027, 1, 31, 23, 59, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2027, 1, 4), date);
    }

    [Fact]
    public void ResolveFlightDate_rejects_a_day_that_does_not_exist_in_the_current_month()
    {
        // 2026-02 has 28 days, so a report claiming day 31 cannot be placed.
        Assert.Throws<PosReportFormatException>(
            () => PosReportParser.ResolveFlightDate(31, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void ResolveFlightDate_uses_the_receive_time_in_utc()
    {
        // 2026-10-01T01:30+07:00 is 2026-09-30T18:30Z, so the month is September.
        var date = PosReportParser.ResolveFlightDate(30, new DateTimeOffset(2026, 10, 1, 1, 30, 0, TimeSpan.FromHours(7)));

        Assert.Equal(new DateOnly(2026, 9, 30), date);
    }
}
