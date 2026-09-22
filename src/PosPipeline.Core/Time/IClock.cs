namespace PosPipeline.Core.Time;

/// <summary>
/// Wall-clock access as a dependency. The pipeline derives flight dates and object keys
/// from "now", so tests need to pin it to a fixed instant.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
