using System.Globalization;
using System.Text.RegularExpressions;
using PosPipeline.Core.Model;

namespace PosPipeline.Core.Parsing;

/// <summary>
/// Parses the POS report wire format:
///
///   POS/{IATA}{flightNumber}.FR {departure}/TO {destination}/{ddHHmm}/{lat}{N|S}{lon}{E|W}/{groundSpeed}/{fuelOnBoard}/{fuelFlow}
///   POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800
///
/// Two entry points, because the two callers need different amounts of the message:
///   - <see cref="ParseIdentity"/> validates the shape and reads only the identifying
///     fields. The ingest API uses it so a message is rejected fast (400) but is not
///     refused for a problem in a field it does not need.
///   - <see cref="Parse"/> reads the whole message. The parser Lambda uses it.
/// </summary>
public static class PosReportParser
{
    /// <summary>Number of '/'-separated segments in a well-formed message.</summary>
    public const int ExpectedSegmentCount = 8;

    private const string Prefix = "POS/";

    private static readonly Regex FlightAndDeparturePattern = new(
        @"^(?<flightNumber>[A-Z0-9]{2}\d{1,4})\.FR\s+(?<departure>[A-Z]{3})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DestinationPattern = new(
        @"^TO\s+(?<destination>[A-Z]{3})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Hour and minute ranges are enforced here so an impossible time is a format error at the
    // API rather than an exception when the timestamp is later constructed.
    private static readonly Regex DayTimePattern = new(
        @"^(?<day>\d{2})(?<hour>[01]\d|2[0-3])(?<minute>[0-5]\d)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumberPattern = new(
        @"^\d+(?:\.\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates the message shape and extracts the flight identity.
    /// </summary>
    /// <param name="rawMessage">The raw POS report text.</param>
    /// <param name="receivedAt">
    /// When the message reached the system. The message carries only a day-of-month, so the
    /// year and month are taken from this instant.
    /// </param>
    /// <exception cref="PosReportFormatException">The message is malformed.</exception>
    public static FlightIdentity ParseIdentity(string? rawMessage, DateTimeOffset receivedAt) =>
        ParseIdentity(SplitAndValidate(rawMessage), receivedAt);

    private static FlightIdentity ParseIdentity(string[] segments, DateTimeOffset receivedAt)
    {
        var flightAndDeparture = Match(FlightAndDeparturePattern, segments[1], "flight and departure");
        var destination = Match(DestinationPattern, segments[2], "destination");
        var dayTime = Match(DayTimePattern, segments[3], "day and time");

        var day = int.Parse(dayTime.Groups["day"].Value, CultureInfo.InvariantCulture);

        return new FlightIdentity(
            flightAndDeparture.Groups["flightNumber"].Value,
            flightAndDeparture.Groups["departure"].Value,
            destination.Groups["destination"].Value,
            ResolveFlightDate(day, receivedAt));
    }

    /// <summary>
    /// Parses the full message, including position, speed and fuel.
    /// </summary>
    /// <param name="rawMessage">The raw POS report text.</param>
    /// <param name="receivedAt">
    /// When the message reached the system, used to supply the year and month the message omits.
    /// </param>
    /// <exception cref="PosReportFormatException">The message is malformed.</exception>
    public static PosReport Parse(string? rawMessage, DateTimeOffset receivedAt)
    {
        var segments = SplitAndValidate(rawMessage);
        var identity = ParseIdentity(segments, receivedAt);

        var dayTime = Match(DayTimePattern, segments[3], "day and time");
        var timestamp = new DateTimeOffset(
            identity.FlightDate.Year,
            identity.FlightDate.Month,
            identity.FlightDate.Day,
            int.Parse(dayTime.Groups["hour"].Value, CultureInfo.InvariantCulture),
            int.Parse(dayTime.Groups["minute"].Value, CultureInfo.InvariantCulture),
            second: 0,
            TimeSpan.Zero);

        var position = GeoCoordinateParser.Parse(segments[4]);

        return new PosReport(
            identity,
            timestamp,
            position.Latitude,
            position.Longitude,
            ParseNumber(segments[5], "ground speed"),
            ParseNumber(segments[6], "fuel on board"),
            ParseNumber(segments[7], "fuel flow"));
    }

    /// <summary>
    /// Combines the day-of-month from the message with the year and month of the receiving
    /// system. The message carries no year or month, so there is nothing else to go on.
    /// </summary>
    /// <exception cref="PosReportFormatException">The day does not exist in the current month.</exception>
    public static DateOnly ResolveFlightDate(int day, DateTimeOffset receivedAt)
    {
        var reference = receivedAt.ToUniversalTime();

        if (day < 1 || day > DateTime.DaysInMonth(reference.Year, reference.Month))
        {
            throw new PosReportFormatException(
                $"Day '{day:00}' does not exist in {reference.Year}-{reference.Month:00}.");
        }

        return new DateOnly(reference.Year, reference.Month, day);
    }

    private static string[] SplitAndValidate(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            throw new PosReportFormatException("POS report is empty.");
        }

        // POS reports are uppercase on the wire; normalising keeps a lowercase message usable.
        var normalised = rawMessage.Trim().ToUpperInvariant();

        if (!normalised.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new PosReportFormatException($"POS report must start with '{Prefix}'.");
        }

        var segments = normalised.Split('/');
        if (segments.Length != ExpectedSegmentCount)
        {
            throw new PosReportFormatException(
                $"POS report must have {ExpectedSegmentCount} '/'-separated segments but had {segments.Length}.");
        }

        return segments;
    }

    private static Match Match(Regex pattern, string segment, string label)
    {
        var match = pattern.Match(segment);
        if (!match.Success)
        {
            throw new PosReportFormatException($"POS report {label} segment '{segment}' is malformed.");
        }

        return match;
    }

    private static double ParseNumber(string segment, string label)
    {
        if (!NumberPattern.IsMatch(segment))
        {
            throw new PosReportFormatException($"POS report {label} segment '{segment}' is not a number.");
        }

        return double.Parse(segment, CultureInfo.InvariantCulture);
    }
}
