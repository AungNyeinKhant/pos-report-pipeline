using PosPipeline.Core.Time;

namespace PosPipeline.Core.Model;

/// <summary>
/// Every S3 key the pipeline uses, in one place. Keys are built from the flight ID and a
/// UTC ISO-8601 timestamp, so objects for a flight sort by time inside their prefix.
/// </summary>
public static class StorageKeys
{
    /// <summary>Raw POS report text as received.</summary>
    public const string RawPrefix = "pos/";

    /// <summary>Structured POS report JSON produced by the parser.</summary>
    public const string AttachmentPrefix = "attachment/";

    /// <summary>Calculation result JSON produced by the calculator.</summary>
    public const string ResultPrefix = "results/";

    /// <summary>pos/{flightId}-{receivedAt}, e.g. pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z</summary>
    public static string RawReport(string flightId, DateTimeOffset receivedAt) =>
        $"{RawPrefix}{flightId}-{Iso8601.ToMilliseconds(receivedAt)}";

    /// <summary>attachment/{flightId}-{reportTimestamp}.json</summary>
    public static string Attachment(string flightId, DateTimeOffset reportTimestamp) =>
        $"{AttachmentPrefix}{flightId}-{Iso8601.ToSeconds(reportTimestamp)}.json";

    /// <summary>results/{flightId}-{now}.json</summary>
    public static string Result(string flightId, DateTimeOffset now) =>
        $"{ResultPrefix}{flightId}-{Iso8601.ToMilliseconds(now)}.json";

    /// <summary>
    /// Reads back the flight ID and receive time from a raw report key. The parser uses this
    /// so it reproduces exactly the flight ID the API already returned to the caller, and so
    /// it resolves the report's year and month from the same instant the API used, rather
    /// than from its own clock.
    /// </summary>
    public static bool TryParseRawReport(string? key, out string flightId, out DateTimeOffset receivedAt)
    {
        flightId = string.Empty;
        receivedAt = default;

        if (key is null || !key.StartsWith(RawPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // The flight ID contains no separators, so the first '-' is the boundary.
        var remainder = key[RawPrefix.Length..];
        var separatorIndex = remainder.IndexOf('-');
        if (separatorIndex <= 0)
        {
            return false;
        }

        flightId = remainder[..separatorIndex];
        return Iso8601.TryParse(remainder[(separatorIndex + 1)..], out receivedAt);
    }
}
