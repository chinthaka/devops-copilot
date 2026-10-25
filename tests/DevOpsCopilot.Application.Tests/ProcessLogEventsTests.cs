using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Application.Tests;

public class ProcessLogEventsTests
{
    [Fact]
    public async Task Restart_resumes_after_the_last_handled_checkpoint()
    {
        // Arrange
        var writer = new CountingReportWriter();
        var source = new ResumableFakeSource(
            [
                ErrorEvent(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), "4bf92f3577b34da6a3ce929d0e0e4736"),
                ErrorEvent(new DateTimeOffset(2026, 10, 20, 10, 1, 0, TimeSpan.Zero), "1bf92f3577b34da6a3ce929d0e0e4736"),
            ],
            writer);
        var processor = new ProcessLogEvents(
            source,
            new RunIncidentPipeline(
                new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
                new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
                writer),
            NullLogger<ProcessLogEvents>.Instance);

        // Act
        await processor.RunAsync(CancellationToken.None);
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal([0, 1], source.DeliveredIndexes);
        Assert.Equal(1, source.CheckpointedIndex);
        Assert.All(source.CheckpointWasAfterDraft, Assert.True);
        Assert.Equal(2, writer.Written);
    }

    private static LogEvent ErrorEvent(DateTimeOffset timestamp, string traceId)
    {
        return new LogEvent(
            timestamp,
            "Error",
            "Checkout.Api",
            "checkout failed",
            "TimeoutException: timed out",
            traceId,
            "00f067aa0ba902b7",
            new Dictionary<string, string>());
    }

    /// <summary>A source that, like a checkpointed stream, replays from after its last checkpoint.</summary>
    private sealed class ResumableFakeSource(IReadOnlyList<LogEvent> events, CountingReportWriter writer) : IEventSource
    {
        public List<int> DeliveredIndexes { get; } = [];

        public List<bool> CheckpointWasAfterDraft { get; } = [];

        public int CheckpointedIndex { get; private set; } = -1;

        public async Task RunAsync(
            Func<ReceivedEvent, CancellationToken, Task> handleEvent,
            CancellationToken cancellationToken)
        {
            var eventIndex = CheckpointedIndex + 1;
            if (eventIndex >= events.Count)
            {
                return;
            }

            DeliveredIndexes.Add(eventIndex);
            await handleEvent(
                new ReceivedEvent(
                    events[eventIndex],
                    _ =>
                    {
                        CheckpointWasAfterDraft.Add(writer.Written > eventIndex);
                        CheckpointedIndex = eventIndex;
                        return Task.CompletedTask;
                    }),
                cancellationToken);
        }
    }

    private sealed class CountingReportWriter : IReportWriter
    {
        public int Written { get; private set; }

        public Task<string> WriteAsync(
            DiagnosticHandoff diagnostic,
            GroundingHandoff grounding,
            string draft,
            CancellationToken cancellationToken)
        {
            Written++;
            return Task.FromResult($"memory://{diagnostic.EventId}.md");
        }
    }
}
