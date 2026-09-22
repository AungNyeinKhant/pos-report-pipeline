using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PosPipeline.Aws;
using PosPipeline.Core.Contracts;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PosPipeline.Calculator;

/// <summary>
/// Triggered by the calculation queue. For each message it loads the parsed report, works out
/// the remaining flight time and the fuel expected at arrival, and stores the result.
///
/// Failures are reported per message (partial batch response) so that one bad report does not
/// force the whole batch to be redelivered. A message that keeps failing is moved to the
/// dead-letter queue by SQS after the configured number of receives.
/// </summary>
public class Function
{
    public async Task<SQSBatchResponse> HandleAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        var useCase = PipelineServices.CreateCalculateUseCase(context.Logger);
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var message = JsonDefaults.Deserialize<CalculationQueueMessage>(record.Body);
                await useCase.ExecuteAsync(message);
            }
            catch (Exception error)
            {
                context.Logger.LogError(
                    $"Calculation failed for message {record.MessageId}, returning it to the queue. {error}");

                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }
}
