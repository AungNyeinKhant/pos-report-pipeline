using System.Net;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using PosPipeline.Aws;
using PosPipeline.Core.Parsing;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PosPipeline.Parser;

/// <summary>
/// Triggered by object creation under the "pos/" prefix. Parses the raw message into a
/// structured report, stores it under "attachment/", records it in the parsed-reports table
/// and queues it for calculation.
/// </summary>
public class Function
{
    public async Task HandleAsync(S3Event s3Event, ILambdaContext context)
    {
        var useCase = PipelineServices.CreateParseUseCase(context.Logger);

        foreach (var record in s3Event.Records)
        {
            // Object keys arrive URL-encoded, and these keys contain ':' from the timestamp.
            var key = WebUtility.UrlDecode(record.S3.Object.Key);

            try
            {
                await useCase.ExecuteAsync(key);
            }
            catch (PosReportFormatException error)
            {
                // The stored message cannot be parsed, and a retry would parse the same bytes
                // again. Record it and move on; the raw object stays in S3 for investigation.
                context.Logger.LogError($"Discarding unparseable object '{key}': {error.Message}");
            }

            // Anything else (S3, DynamoDB or SQS failing) is left to propagate: Lambda retries
            // the event twice and then delivers it to the parser's dead-letter queue.
        }
    }
}
