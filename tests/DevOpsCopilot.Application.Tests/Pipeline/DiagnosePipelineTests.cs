using DevOpsCopilot.Application.Tests.Common;
using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Events;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Pipeline;
using DevOpsCopilot.Application.Tests.Diagnoses;
using DevOpsCopilot.Application.Tests.Grounding;
using DevOpsCopilot.Application.Tests.Kql;
using DevOpsCopilot.Application.Tests.Reporting;
using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Kql;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Application.Tests.Pipeline;

public class DiagnosePipelineTests
{
    [Fact]
    public async Task RunAsync_WithoutAConfiguredModel_StillWritesAFullReport()
    {
        // Arrange
        var writer = new RecordingReportWriter();
        var pipeline = Pipeline(writer, new RecordingDiagnosisModel { IsConfigured = false });
        string[] headings =
        [
            "# Incident report:",
            "## Impact",
            "## Likely cause",
            "## Evidence",
            "## Similar incidents",
            "## Suggested steps",
            "## Open questions",
        ];

        // Act
        var result = await pipeline.RunAsync(ErrorEvent());

        // Assert
        Assert.NotNull(result);
        var report = Assert.Single(writer.Drafts);
        var position = -1;
        foreach (var heading in headings)
        {
            var next = report.IndexOf(heading, position + 1, StringComparison.Ordinal);
            Assert.True(next > position, heading);
            position = next;
        }

        Assert.Contains("confidence: low", report, StringComparison.Ordinal);
        Assert.Contains("model was unavailable", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithAWellFormedModelDiagnosis_UsesItInTheReport()
    {
        // Arrange
        var writer = new RecordingReportWriter();
        var model = new RecordingDiagnosisModel
        {
            Proposed = new Diagnosis(
                EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
                "Checkout times out on the payment gateway",
                "The payment gateway stopped answering.",
                Confidence.Medium,
                new Impact(["Checkout.Api"], [], 1, "2026-10-19T12:00:00.0000000+00:00"),
                [],
                [],
                ["Check the payment gateway's health."],
                ["Was the gateway deployed in this window?"]),
        };
        var pipeline = Pipeline(writer, model);

        // Act
        var result = await pipeline.RunAsync(ErrorEvent());

        // Assert
        Assert.NotNull(result);
        var report = Assert.Single(writer.Drafts);
        Assert.Contains("Checkout times out on the payment gateway", report, StringComparison.Ordinal);
        Assert.Contains("The payment gateway stopped answering.", report, StringComparison.Ordinal);
        Assert.Contains("Check the payment gateway's health.", report, StringComparison.Ordinal);
        Assert.Contains("Was the gateway deployed in this window?", report, StringComparison.Ordinal);
        Assert.DoesNotContain("model was unavailable", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenTheModelFails_StillCheckpointsTheEvent()
    {
        // Arrange
        var observer = new RecordingProcessingObserver();
        var checkpoints = 0;
        var source = new SingleEventSource(ErrorEvent(), () => checkpoints++);
        var pipeline = Pipeline(
            new RecordingReportWriter(),
            new RecordingDiagnosisModel { Throws = new InvalidOperationException("the endpoint refused the call") });
        var processor = new ProcessLogEvents(source, pipeline, NullLogger<ProcessLogEvents>.Instance, observer);

        // Act
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpoints);
        Assert.Equal(0, observer.Failed);
        Assert.Equal(1, observer.Checkpointed);
    }

    private static RunIncidentPipeline Pipeline(IReportWriter writer, IDiagnosisModel model) =>
        new(
            new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
            new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
            writer,
            new Diagnose(model, TestTimeouts.Generous));

    private static LogEvent ErrorEvent() =>
        new(
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            "Error",
            "Checkout.Api",
            "checkout failed",
            "System.TimeoutException: checkout timed out",
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7");

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

    private sealed class RecordingProcessingObserver : IProcessingObserver
    {
        public int Failed { get; private set; }

        public int Checkpointed { get; private set; }

        public void OnEventRead()
        {
        }

        public void OnEventSkipped()
        {
        }

        public void OnCheckpointed() => Checkpointed++;

        public void OnEventFailed() => Failed++;
    }
}
