using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

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
