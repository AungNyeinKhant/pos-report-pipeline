using Amazon.SQS;
using Amazon.SQS.Model;
using PosPipeline.Core.Contracts;
using PosPipeline.Core.Ports;

namespace PosPipeline.Aws;

/// <summary>SQS-backed <see cref="ICalculationQueue"/>.</summary>
public sealed class SqsCalculationQueue : ICalculationQueue
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;

    public SqsCalculationQueue(IAmazonSQS sqs, string queueUrl)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
    }

    public Task PublishAsync(CalculationQueueMessage message, CancellationToken cancellationToken = default) =>
        _sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = JsonDefaults.Serialize(message),
            },
            cancellationToken);
}
