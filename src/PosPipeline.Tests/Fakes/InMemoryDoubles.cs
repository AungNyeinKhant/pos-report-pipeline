using System.Collections.Concurrent;
using PosPipeline.Core.Contracts;
using PosPipeline.Core.Model;
using PosPipeline.Core.Ports;
using PosPipeline.Core.Time;

namespace PosPipeline.Tests.Fakes;

/// <summary>In-memory stand-ins for the AWS-backed ports, so use cases can be tested locally.</summary>
public sealed class InMemoryObjectStore : IObjectStore
{
    public ConcurrentDictionary<string, string> Objects { get; } = new();

    public ConcurrentDictionary<string, string> ContentTypes { get; } = new();

    public Task PutTextAsync(string key, string content, string contentType, CancellationToken cancellationToken = default)
    {
        Objects[key] = content;
        ContentTypes[key] = contentType;
        return Task.CompletedTask;
    }

    public Task<string> GetTextAsync(string key, CancellationToken cancellationToken = default) =>
        Objects.TryGetValue(key, out var content)
            ? Task.FromResult(content)
            : throw new KeyNotFoundException($"No object stored at '{key}'.");

    public string KeyWithPrefix(string prefix) => Objects.Keys.Single(key => key.StartsWith(prefix, StringComparison.Ordinal));
}

public sealed class InMemoryParsedReportRepository : IParsedReportRepository
{
    public List<ParsedReportRecord> Records { get; } = [];

    public Task SaveAsync(ParsedReportRecord record, CancellationToken cancellationToken = default)
    {
        // DynamoDB PutItem replaces an item with the same key; mirror that here.
        Records.RemoveAll(existing => existing.FlightId == record.FlightId && existing.Timestamp == record.Timestamp);
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<ParsedReportRecord?> GetAsync(string flightId, string timestamp, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.SingleOrDefault(r => r.FlightId == flightId && r.Timestamp == timestamp));

    public Task<ParsedReportRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records
            .Where(r => r.FlightId == flightId)
            .OrderByDescending(r => r.Timestamp, StringComparer.Ordinal)
            .FirstOrDefault());
}

public sealed class InMemoryCalculationResultRepository : ICalculationResultRepository
{
    public List<CalculationResultRecord> Records { get; } = [];

    public Task SaveAsync(CalculationResultRecord record, CancellationToken cancellationToken = default)
    {
        Records.RemoveAll(existing => existing.FlightId == record.FlightId && existing.Timestamp == record.Timestamp);
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<CalculationResultRecord?> GetLatestAsync(string flightId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records
            .Where(r => r.FlightId == flightId)
            .OrderByDescending(r => r.Timestamp, StringComparer.Ordinal)
            .FirstOrDefault());
}

public sealed class RecordingCalculationQueue : ICalculationQueue
{
    public List<CalculationQueueMessage> Messages { get; } = [];

    public Task PublishAsync(CalculationQueueMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

public sealed class TestLogger : IPipelineLogger
{
    public List<string> Messages { get; } = [];

    public void Info(string message) => Messages.Add($"INFO {message}");

    public void Warn(string message) => Messages.Add($"WARN {message}");

    public void Error(string message, Exception? exception = null) => Messages.Add($"ERROR {message}");
}
