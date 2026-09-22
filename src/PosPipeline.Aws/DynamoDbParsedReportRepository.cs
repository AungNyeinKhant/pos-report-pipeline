using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PosPipeline.Core.Model;
using PosPipeline.Core.Ports;

namespace PosPipeline.Aws;

/// <summary>The parsed-reports table: partition key "flightId", sort key "timestamp".</summary>
public sealed class DynamoDbParsedReportRepository : IParsedReportRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbParsedReportRepository(IAmazonDynamoDB dynamoDb, string tableName)
    {
        _dynamoDb = dynamoDb;
        _tableName = tableName;
    }

    public Task SaveAsync(ParsedReportRecord record, CancellationToken cancellationToken = default) =>
        _dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    [DynamoAttributes.FlightId] = new(record.FlightId),
                    [DynamoAttributes.Timestamp] = new(record.Timestamp),
                    [DynamoAttributes.Attachment] = new(record.AttachmentKey),
                },
            },
            cancellationToken);

    public async Task<ParsedReportRecord?> GetAsync(
        string flightId,
        string timestamp,
        CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    [DynamoAttributes.FlightId] = new(flightId),
                    [DynamoAttributes.Timestamp] = new(timestamp),
                },
                // The parser wrote this row moments ago, so read the authoritative copy rather
                // than risk an eventually consistent miss and an unnecessary retry.
                ConsistentRead = true,
            },
            cancellationToken);

        return response.IsItemSet ? ToRecord(response.Item) : null;
    }

    public async Task<ParsedReportRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDb.QueryAsync(
            DynamoQueries.LatestForFlight(_tableName, flightId),
            cancellationToken);

        return response.Items.Count == 0 ? null : ToRecord(response.Items[0]);
    }

    private static ParsedReportRecord ToRecord(IReadOnlyDictionary<string, AttributeValue> item) =>
        new(
            item[DynamoAttributes.FlightId].S,
            item[DynamoAttributes.Timestamp].S,
            item[DynamoAttributes.Attachment].S);
}
