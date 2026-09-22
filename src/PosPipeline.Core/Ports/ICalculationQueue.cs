using PosPipeline.Core.Contracts;

namespace PosPipeline.Core.Ports;

/// <summary>The queue that hands parsed reports to the calculator (SQS in production).</summary>
public interface ICalculationQueue
{
    Task PublishAsync(CalculationQueueMessage message, CancellationToken cancellationToken = default);
}
