using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Application.Tests;

public class ProcessingObserverTests
{
    [Fact]
    public async Task RunAsync_WithAnIncidentStartingEvent_NotifiesReadThenCheckpointed()
    {
        // Arrange
        var observer = new RecordingObserver();
        var processor = Processor(observer, ErrorEvent());

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["read", "checkpointed"], observer.Notifications);
    }

    [Fact]
    public async Task RunAsync_WithAnEventThatDoesNotStartAnIncident_NotifiesSkipped()
    {
        // Arrange
        var observer = new RecordingObserver();
        var processor = Processor(observer, WarningEvent());

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["read", "skipped", "checkpointed"], observer.Notifications);
    }

    [Fact]
    public async Task RunAsync_WhenAnEventIsCheckpointed_NotifiesCheckpointedAfterTheCheckpointCall()
    {
        // Arrange
        var observer = new RecordingObserver();
        var source = new ListSource(trace: observer.Notifications, ErrorEvent());
        var processor = new ProcessLogEvents(source, Pipeline(), NullLogger<ProcessLogEvents>.Instance, observer);

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["read", "checkpoint-call", "checkpointed"], observer.Notifications);
    }

    [Fact]
    public async Task RunAsync_WithAFailingEvent_NotifiesFailedAndStillPropagatesTheException()
    {
        // Arrange
        var observer = new RecordingObserver();
        var source = new ListSource(trace: null, ErrorEvent());
        var pipeline = new RunIncidentPipeline(
            new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
            new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
            new ThrowingReportWriter());
        var processor = new ProcessLogEvents(source, pipeline, NullLogger<ProcessLogEvents>.Instance, observer);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.RunAsync(CancellationToken.None));
        Assert.Equal(["read", "failed"], observer.Notifications);
        Assert.Equal(0, source.Checkpoints);
    }

    [Fact]
    public async Task RunAsync_WithoutAnObserver_ProcessesAndCheckpointsAsBefore()
    {
        // Arrange
        var source = new ListSource(trace: null, ErrorEvent());
        var processor = new ProcessLogEvents(source, Pipeline(), NullLogger<ProcessLogEvents>.Instance);

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, source.Checkpoints);
    }

    private static ProcessLogEvents Processor(IProcessingObserver observer, params LogEvent[] events) =>
        new(new ListSource(trace: null, events), Pipeline(), NullLogger<ProcessLogEvents>.Instance, observer);

    private static RunIncidentPipeline Pipeline() =>
        new(
            new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
            new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
            new RecordingReportWriter());

    private static LogEvent ErrorEvent() =>
        new(
            new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero),
            "Error",
            "Checkout.Api",
            "checkout failed",
            "TimeoutException: timed out",
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            new Dictionary<string, string>());

    private static LogEvent WarningEvent() =>
        new(
            new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero),
            "Warning",
            "Checkout.Api",
            "slow response",
            null,
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            new Dictionary<string, string>());

    /// <summary>Records the order of every notification it receives.</summary>
    private sealed class RecordingObserver : IProcessingObserver
    {
        public List<string> Notifications { get; } = [];

        public void OnEventRead() => Notifications.Add("read");

        public void OnEventSkipped() => Notifications.Add("skipped");

        public void OnCheckpointed() => Notifications.Add("checkpointed");

        public void OnEventFailed() => Notifications.Add("failed");
    }

    /// <summary>Delivers the given events once, counting checkpoints and optionally tracing them.</summary>
    private sealed class ListSource(List<string>? trace, params LogEvent[] events) : IEventSource
    {
        public int Checkpoints { get; private set; }

        public async Task RunAsync(
            Func<ReceivedEvent, CancellationToken, Task> handleEvent,
            CancellationToken cancellationToken)
        {
            foreach (var logEvent in events)
            {
                await handleEvent(
                    new ReceivedEvent(
                        logEvent,
                        _ =>
                        {
                            trace?.Add("checkpoint-call");
                            Checkpoints++;
                            return Task.CompletedTask;
                        }),
                    cancellationToken);
            }
        }
    }

    private sealed class ThrowingReportWriter : IReportWriter
    {
        public Task<string> WriteAsync(
            DiagnosticHandoff diagnostic,
            GroundingHandoff grounding,
            string draft,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The report store is down.");
    }
}
