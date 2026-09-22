using PosPipeline.Core.Model;
using PosPipeline.Core.Parsing;
using PosPipeline.Core.UseCases;
using PosPipeline.Tests.Fakes;
using Xunit;

namespace PosPipeline.Tests.UseCases;

public class IngestPosReportUseCaseTests
{
    private const string ExampleMessage = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 4, 15, 12, 30, 123, TimeSpan.Zero);

    private readonly InMemoryObjectStore _objectStore = new();
    private readonly IngestPosReportUseCase _useCase;

    public IngestPosReportUseCaseTests() =>
        _useCase = new IngestPosReportUseCase(_objectStore, new FixedClock(ReceivedAt), new TestLogger());

    [Fact]
    public async Task Stores_the_raw_message_under_the_pos_prefix_and_acknowledges_it()
    {
        var result = await _useCase.ExecuteAsync(ExampleMessage);

        Assert.Equal("UL20420260904RGNBKK", result.FlightId);
        Assert.Equal(FlightStatus.Received, result.Status);
        Assert.Equal("pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z", result.ObjectKey);
        Assert.Equal(ExampleMessage, _objectStore.Objects[result.ObjectKey]);
        Assert.Equal("text/plain", _objectStore.ContentTypes[result.ObjectKey]);
    }

    [Fact]
    public async Task Stores_nothing_when_the_message_is_malformed()
    {
        await Assert.ThrowsAsync<PosReportFormatException>(() => _useCase.ExecuteAsync("not a pos report"));

        Assert.Empty(_objectStore.Objects);
    }
}
