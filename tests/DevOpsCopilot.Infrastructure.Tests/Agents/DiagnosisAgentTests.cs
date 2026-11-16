using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Pipeline;
using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Infrastructure.Agents;
using DevOpsCopilot.Infrastructure.Tests.Diagnoses;

namespace DevOpsCopilot.Infrastructure.Tests.Agents;

public class DiagnosisAgentTests
{
    [Fact]
    public async Task WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections()
    {
        // Arrange
        var writer = new RecordingReportWriter();
        var agent = new DiagnosisAgent(new Diagnose(UnconfiguredDiagnosisModel.Instance, TimeSpan.FromSeconds(10)), writer);
        var diagnostic = UnavailableDiagnostic();
        var grounding = UnavailableGrounding(diagnostic.EventId);
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
        var result = await agent.WriteAsync(diagnostic, grounding, CancellationToken.None);

        // Assert
        var report = Assert.Single(writer.Reports);
        var position = -1;
        foreach (var heading in headings)
        {
            var next = report.IndexOf(heading, position + 1, StringComparison.Ordinal);
            Assert.True(next > position, heading);
            position = next;
            Assert.Equal(next, report.LastIndexOf(heading, StringComparison.Ordinal));
        }

        Assert.DoesNotContain("## Slack", report, StringComparison.Ordinal);
        Assert.DoesNotContain("## Teams", report, StringComparison.Ordinal);
        Assert.Equal("written://report.md", result.DraftPath);
    }

    [Fact]
    public async Task WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites()
    {
        // Arrange
        var pipelineWriter = new RecordingReportWriter();
        var pipeline = new RunIncidentPipeline(
            new GatherEvidence(new UnconfiguredLogQuery(), LogQueryTargets.Unconfigured),
            new GroundInHistory(new UnconfiguredKnowledgeSearch()),
            pipelineWriter,
            new Diagnose(UnconfiguredDiagnosisModel.Instance, TimeSpan.FromSeconds(10)));
        var agentWriter = new RecordingReportWriter();
        var agent = new DiagnosisAgent(new Diagnose(UnconfiguredDiagnosisModel.Instance, TimeSpan.FromSeconds(10)), agentWriter);

        // Act
        var result = await pipeline.RunAsync(ErrorEvent());
        Assert.NotNull(result);
        await agent.WriteAsync(result.Diagnostic, result.Grounding, CancellationToken.None);

        // Assert
        Assert.Equal(Assert.Single(pipelineWriter.Reports), Assert.Single(agentWriter.Reports));
    }

    [Fact]
    public void DiagnosisAgent_WithAChatClient_ForbidsResourceChangesInItsInstructions()
    {
        // Arrange
        using var chatClient = new FakeChatClient("");

        // Act
        var agent = new DiagnosisAgent(
            new Diagnose(UnconfiguredDiagnosisModel.Instance, TimeSpan.FromSeconds(10)),
            new RecordingReportWriter(),
            chatClient);

        // Assert
        Assert.NotNull(agent.ChatAgent);
        Assert.Equal("DiagnosisAgent", DiagnosisAgent.AgentName);
        Assert.Contains("handoffs", DiagnosisAgent.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("changes a resource", DiagnosisAgent.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("script", DiagnosisAgent.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("engineer", DiagnosisAgent.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Slack", DiagnosisAgent.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teams", DiagnosisAgent.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static LogEvent ErrorEvent()
    {
        return new LogEvent(
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            "Error",
            "Checkout.Api",
            "checkout failed",
            "TimeoutException: slow",
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7");
    }

    private static DiagnosticHandoff UnavailableDiagnostic()
    {
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            "TimeoutException: slow",
            "unavailable",
            ["ExceptionsTable | where TraceId == \"\""],
            [],
            []);
    }

    private static GroundingHandoff UnavailableGrounding(string eventId)
    {
        return new GroundingHandoff(eventId, "unavailable", PostmortemOverlap.IndexName, ["Checkout.Api"], []);
    }

    private sealed class RecordingReportWriter : IReportWriter
    {
        public List<string> Reports { get; } = [];

        public Task<string> WriteAsync(
            DiagnosticHandoff diagnostic,
            GroundingHandoff grounding,
            string draft,
            CancellationToken cancellationToken)
        {
            Reports.Add(draft);
            return Task.FromResult("written://report.md");
        }
    }

    private sealed class UnconfiguredLogQuery : ILogQuery
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An unconfigured log query must not be executed.");
    }

    private sealed class UnconfiguredKnowledgeSearch : IKnowledgeSearch
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An unconfigured search must not be executed.");
    }
}
