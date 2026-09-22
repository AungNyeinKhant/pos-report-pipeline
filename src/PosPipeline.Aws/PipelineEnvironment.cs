namespace PosPipeline.Aws;

/// <summary>
/// The environment variables the CDK stack sets on the Lambdas. Reading them through one
/// type means a missing variable fails loudly at cold start instead of as a null reference
/// deep inside a handler.
/// </summary>
public static class PipelineEnvironment
{
    public const string BucketNameVariable = "REPORTS_BUCKET_NAME";
    public const string ParsedReportsTableVariable = "PARSED_REPORTS_TABLE_NAME";
    public const string ResultsTableVariable = "RESULTS_TABLE_NAME";
    public const string CalculationQueueUrlVariable = "CALCULATION_QUEUE_URL";

    public static string BucketName => Require(BucketNameVariable);

    public static string ParsedReportsTableName => Require(ParsedReportsTableVariable);

    public static string ResultsTableName => Require(ResultsTableVariable);

    public static string CalculationQueueUrl => Require(CalculationQueueUrlVariable);

    private static string Require(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Required environment variable '{name}' is not set.")
            : value;
    }
}
