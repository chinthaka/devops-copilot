using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Pipeline;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Application.Tests.Grounding;
using DevOpsCopilot.Application.Tests.Kql;
using DevOpsCopilot.Application.Tests.Reporting;
using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Tests.Pipeline;

public class ReportTemplatePipelineTests
{
    [Fact]
    public async Task RunAsync_WithACustomReportTemplate_RendersTheReportThroughIt()
    {
        // Arrange
        var writer = new RecordingReportWriter();
        var source = new FakeTemplateSource { Report = "Incident {{IncidentId}}: {{Confidence}}" };

        // Act
        await Pipeline(writer, source).RunAsync(ErrorEvent());

        // Assert
        var draft = Assert.Single(writer.Drafts);
        Assert.StartsWith("Incident ", draft, StringComparison.Ordinal);
        Assert.EndsWith(": low", draft, StringComparison.Ordinal);
        Assert.Equal([TemplateNames.Report], source.Requested);
    }

    [Fact]
    public async Task RunAsync_WhenTheTemplateSourceReturnsNothing_RendersTheDefaultReport()
    {
        // Arrange
        var withSource = new RecordingReportWriter();
        var withoutSource = new RecordingReportWriter();

        // Act
        await Pipeline(withSource, new FakeTemplateSource()).RunAsync(ErrorEvent());
        await Pipeline(withoutSource, templates: null).RunAsync(ErrorEvent());

        // Assert
        Assert.Equal(Assert.Single(withoutSource.Drafts), Assert.Single(withSource.Drafts));
    }

    [Fact]
    public async Task RunAsync_WhenTheTemplateSourceThrows_StillWritesTheDefaultReport()
    {
        // Arrange
        var expected = new RecordingReportWriter();
        var writer = new RecordingReportWriter();

        // Act
        await Pipeline(expected, templates: null).RunAsync(ErrorEvent());
        await Pipeline(writer, new FakeTemplateSource { Throws = true }).RunAsync(ErrorEvent());

        // Assert
        Assert.Equal(Assert.Single(expected.Drafts), Assert.Single(writer.Drafts));
    }

    private static RunIncidentPipeline Pipeline(IReportWriter writer, ITemplateSource? templates) =>
        new(
            new GatherEvidence(new RecordingLogQuery { IsConfigured = false }, LogQueryTargets.Unconfigured),
            new GroundInHistory(new RecordingKnowledgeSearch { IsConfigured = false }),
            writer,
            templates: templates);

    private static LogEvent ErrorEvent() =>
        new(
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            "Error",
            "Checkout.Api",
            "checkout failed",
            "System.TimeoutException: checkout timed out",
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7");

    private sealed class FakeTemplateSource : ITemplateSource
    {
        public string? Report { get; init; }

        public bool Throws { get; init; }

        public List<string> Requested { get; } = [];

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
        {
            Requested.Add(name);
            if (Throws)
            {
                throw new IOException("share unavailable");
            }

            return Task.FromResult(name == TemplateNames.Report ? Report : null);
        }
    }
}
