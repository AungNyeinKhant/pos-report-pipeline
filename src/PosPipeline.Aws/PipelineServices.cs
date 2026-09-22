using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.SQS;
using PosPipeline.Core.Ports;
using PosPipeline.Core.Time;
using PosPipeline.Core.UseCases;

namespace PosPipeline.Aws;

/// <summary>
/// Composition root for the Lambda entry points.
///
/// The AWS clients and the adapters around them are static and lazily created, so they are
/// built once per execution environment and reused across invocations (connection reuse is
/// most of what makes a warm Lambda fast). The use cases themselves are cheap objects and
/// are created per invocation, which lets each one log through that invocation's logger.
/// </summary>
public static class PipelineServices
{
    private static readonly Lazy<IAmazonS3> S3Client = new(() => new AmazonS3Client());
    private static readonly Lazy<IAmazonDynamoDB> DynamoDbClient = new(() => new AmazonDynamoDBClient());
    private static readonly Lazy<IAmazonSQS> SqsClient = new(() => new AmazonSQSClient());

    private static readonly Lazy<IObjectStore> ObjectStore =
        new(() => new S3ObjectStore(S3Client.Value, PipelineEnvironment.BucketName));

    private static readonly Lazy<IParsedReportRepository> ParsedReports =
        new(() => new DynamoDbParsedReportRepository(DynamoDbClient.Value, PipelineEnvironment.ParsedReportsTableName));

    private static readonly Lazy<ICalculationResultRepository> Results =
        new(() => new DynamoDbCalculationResultRepository(DynamoDbClient.Value, PipelineEnvironment.ResultsTableName));

    private static readonly Lazy<ICalculationQueue> CalculationQueue =
        new(() => new SqsCalculationQueue(SqsClient.Value, PipelineEnvironment.CalculationQueueUrl));

    public static IngestPosReportUseCase CreateIngestUseCase(ILambdaLogger logger) =>
        new(ObjectStore.Value, SystemClock.Instance, new LambdaPipelineLogger(logger));

    public static ParsePosReportUseCase CreateParseUseCase(ILambdaLogger logger) =>
        new(ObjectStore.Value, ParsedReports.Value, CalculationQueue.Value, new LambdaPipelineLogger(logger));

    public static CalculateFlightUseCase CreateCalculateUseCase(ILambdaLogger logger) =>
        new(ObjectStore.Value, ParsedReports.Value, Results.Value, SystemClock.Instance, new LambdaPipelineLogger(logger));

    public static GetFlightStatusUseCase CreateStatusUseCase() =>
        new(ParsedReports.Value, Results.Value, ObjectStore.Value);
}
