using DevOpsCopilot.Application.Events;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Pipeline;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Application.Tests.Common;
using DevOpsCopilot.Application.Tests.Grounding;
using DevOpsCopilot.Application.Tests.Kql;
using DevOpsCopilot.Application.Tests.Reporting;
using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Domain.Reporting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Application.Tests.Pipeline;

public class NotificationPipelineTests
{
    [Fact]
    public async Task RunAsync_WithAConfiguredChannel_NotifiesItAfterWritingTheReport()
    {
        // Arrange
        var calls = new List<string>();
        var notifier = new FakeNotifier("teams", calls);
        var pipeline = Pipeline(new OrderedReportWriter(calls), new NotifyChannels([notifier], TestTimeouts.Generous));

        // Act
        var result = await pipeline.RunAsync(ErrorEvent());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["write", "teams"], calls);
        var sent = Assert.Single(notifier.Sent);
        Assert.Equal("written://draft.md", sent.ReportLocation);
        var delivery = Assert.Single(result.Deliveries);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
    }

    [Fact]
    public async Task RunAsync_WhenAChannelIsConfigured_WritesTheReportBeforeCallingAnyChannel()
    {
        // Arrange
        var calls = new List<string>();
        var notifiers = new[] { new FakeNotifier("teams", calls), new FakeNotifier("slack", calls) };
        var pipeline = Pipeline(new OrderedReportWriter(calls), new NotifyChannels(notifiers, TestTimeouts.Generous));

        // Act
        await pipeline.RunAsync(ErrorEvent());

        // Assert
        Assert.Equal("write", calls[0]);
        Assert.Equal(["write", "teams", "slack"], calls);
    }

    [Fact]
    public async Task RunAsync_WithNoConfiguredChannel_StillWritesTheReportAndReturnsNormally()
    {
        // Arrange
        var writer = new RecordingReportWriter();
        var pipeline = Pipeline(writer, notify: null);

        // Act
        var result = await pipeline.RunAsync(ErrorEvent());

        // Assert
        Assert.NotNull(result);
        Assert.Single(writer.Drafts);
        Assert.False(string.IsNullOrEmpty(result.DraftPath));
        Assert.Empty(result.Deliveries);
    }

    [Fact]
    public async Task RunAsync_WithNotificationAdded_WritesTheSameReportBytesAsBefore()
    {
        // Arrange
        var before = new RecordingReportWriter();
        var after = new RecordingReportWriter();
        var unconfigured = new[] { "teams", "slack", "discord", "email" }
            .Select(name => new FakeNotifier(name) { IsConfigured = false });

        // Act
        await Pipeline(before, notify: null).RunAsync(ErrorEvent());
        var result = await Pipeline(after, new NotifyChannels(unconfigured, TestTimeouts.Generous)).RunAsync(ErrorEvent());

        // Assert
        Assert.Equal(Assert.Single(before.Drafts), Assert.Single(after.Drafts));
        Assert.NotNull(result);
        Assert.All(result.Deliveries, delivery => Assert.Equal(NotificationStatus.Unavailable, delivery.Status));
    }

    [Fact]
    public async Task RunAsync_WhenEveryChannelFails_StillCheckpointsTheEvent()
    {
        // Arrange
        var checkpoints = 0;
        var failed = 0;
        var notifiers = new[] { "teams", "slack", "discord", "email" }
            .Select(name => new FakeNotifier(name) { Throws = new InvalidOperationException("down") });
        var pipeline = Pipeline(new RecordingReportWriter(), new NotifyChannels(notifiers, TestTimeouts.Generous));
        var source = new SingleEventSource(ErrorEvent(), () => checkpoints++);
        var observer = new CountingObserver(() => failed++);
        var processor = new ProcessLogEvents(source, pipeline, NullLogger<ProcessLogEvents>.Instance, observer);

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpoints);
        Assert.Equal(0, failed);
    }

    private static RunIncidentPipeline Pipeline(IReportWriter writer, NotifyChannels? notify) =>
        new(
            new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
            new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
            writer,
            notify: notify);

    private static LogEvent ErrorEvent() =>
        new(
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            "Error",
            "Checkout.Api",
            "checkout failed",
            "System.TimeoutException: checkout timed out",
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7");

    private sealed class OrderedReportWriter(List<string> calls) : IReportWriter
    {
        public Task<string> WriteAsync(
            DiagnosticHandoff diagnostic,
            GroundingHandoff grounding,
            string draft,
            CancellationToken cancellationToken)
        {
            calls.Add("write");
            return Task.FromResult("written://draft.md");
        }
    }

    private sealed class SingleEventSource(LogEvent logEvent, Action onCheckpoint) : IEventSource
    {
        public Task RunAsync(Func<ReceivedEvent, CancellationToken, Task> handler, CancellationToken cancellationToken)
        {
            var received = new ReceivedEvent(logEvent, _ =>
            {
                onCheckpoint();
                return Task.CompletedTask;
            });
            return handler(received, cancellationToken);
        }
    }

    private sealed class CountingObserver(Action onFailed) : IProcessingObserver
    {
        public void OnEventRead()
        {
        }

        public void OnEventSkipped()
        {
        }

        public void OnCheckpointed()
        {
        }

        public void OnEventFailed() => onFailed();
    }
}
