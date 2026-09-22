namespace PosPipeline.Core.Ports;

/// <summary>
/// Minimal logging port so use cases can record the handful of events worth recording
/// without taking a dependency on the Lambda runtime.
/// </summary>
public interface IPipelineLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
