using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PosPipeline.Core.Model;
using PosPipeline.Core.Ports;

namespace PosPipeline.Aws;

/// <summary>The results table: partition key "flightId", sort key "timestamp".</summary>
public sealed class DynamoDbCalculationResultRepository : ICalculationResultRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbCalculationResultRepository(IAmazonDynamoDB dynamoDb, string tableName)
    {
        _dynamoDb = dynamoDb;
        _tableName = tableName;
    }

    public Task SaveAsync(CalculationResultRecord record, CancellationToken cancellationToken = default) =>
        _dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    [DynamoAttributes.FlightId] = new(record.FlightId),
                    [DynamoAttributes.Timestamp] = new(record.Timestamp),
                    [DynamoAttributes.Attachment] = new(record.AttachmentKey),
                    [DynamoAttributes.Status] = new(record.Status),
                    [DynamoAttributes.ReportTimestamp] = new(record.ReportTimestamp),
                },
            },
            cancellationToken);

    public async Task<CalculationResultRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDb.QueryAsync(
            DynamoQueries.LatestForFlight(_tableName, flightId),
            cancellationToken);

        if (response.Items.Count == 0)
        {
            return null;
        }

        var item = response.Items[0];

        return new CalculationResultRecord(
            item[DynamoAttributes.FlightId].S,
            item[DynamoAttributes.Timestamp].S,
            item[DynamoAttributes.Attachment].S,
            item.TryGetValue(DynamoAttributes.Status, out var status) ? status.S : FlightStatus.Calculated,
            item.TryGetValue(DynamoAttributes.ReportTimestamp, out var reportTimestamp) ? reportTimestamp.S : string.Empty);
    }
}
