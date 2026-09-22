using System.Globalization;

namespace PosPipeline.Core.Time;

/// <summary>
/// Every timestamp that leaves this system (S3 keys, DynamoDB sort keys, JSON payloads)
/// is a UTC ISO-8601 string produced here, so keys sort lexicographically by time.
/// </summary>
public static class Iso8601
{
    /// <summary>Second precision, used for POS report timestamps: 2026-09-04T12:05:00Z.</summary>
    public const string SecondFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Millisecond precision, used for "now" timestamps: 2026-09-04T15:12:30.123Z.</summary>
    public const string MillisecondFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string ToSeconds(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(SecondFormat, CultureInfo.InvariantCulture);

    public static string ToMilliseconds(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(MillisecondFormat, CultureInfo.InvariantCulture);

    /// <summary>Parses either of the two formats above back into a UTC instant.</summary>
    public static bool TryParse(string? value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out result);
}
