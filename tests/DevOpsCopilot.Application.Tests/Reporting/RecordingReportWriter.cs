using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Tests.Reporting;

/// <summary>A report writer that keeps the drafts in memory.</summary>
internal sealed class RecordingReportWriter : IReportWriter
{
    public List<string> Drafts { get; } = [];

    public Task<string> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string draft,
        CancellationToken cancellationToken)
    {
        Drafts.Add(draft);
        return Task.FromResult($"memory://{diagnostic.EventId}.md");
    }
}
