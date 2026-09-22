using Amazon.DynamoDBv2.Model;

namespace PosPipeline.Aws;

/// <summary>Attribute names shared by both tables.</summary>
internal static class DynamoAttributes
{
    internal const string FlightId = "flightId";
    internal const string Timestamp = "timestamp";
    internal const string Attachment = "attachment";
    internal const string Status = "status";
    internal const string ReportTimestamp = "reportTimestamp";
}

internal static class DynamoQueries
{
    /// <summary>
    /// Newest row for a flight. Both tables sort by an ISO-8601 UTC timestamp, so reading the
    /// index backwards and taking one item gives the latest record - no scan, no sorting in code.
    /// </summary>
    internal static QueryRequest LatestForFlight(string tableName, string flightId) => new()
    {
        TableName = tableName,
        KeyConditionExpression = "#flightId = :flightId",
        ExpressionAttributeNames = new Dictionary<string, string> { ["#flightId"] = DynamoAttributes.FlightId },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":flightId"] = new(flightId) },
        ScanIndexForward = false,
        Limit = 1,
    };
}
