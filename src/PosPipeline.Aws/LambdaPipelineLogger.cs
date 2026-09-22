using Amazon.Lambda.Core;
using PosPipeline.Core.Ports;

namespace PosPipeline.Aws;

/// <summary>
/// Writes the domain's log events to CloudWatch through the Lambda logger, so that the log
/// level is preserved and warnings and errors can be picked out by a query or metric filter.
/// </summary>
public sealed class LambdaPipelineLogger : IPipelineLogger
{
    private readonly ILambdaLogger _logger;

    public LambdaPipelineLogger(ILambdaLogger logger) => _logger = logger;

    public void Info(string message) => _logger.LogInformation(message);

    public void Warn(string message) => _logger.LogWarning(message);

    public void Error(string message, Exception? exception = null) =>
        _logger.LogError(exception is null ? message : $"{message} {exception}");
}
