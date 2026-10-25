using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

/// <summary>A log store that counts its executions and returns whatever it was given.</summary>
internal sealed class RecordingLogQuery : ILogQuery
{
    public bool IsConfigured { get; init; }

    public int Executions { get; private set; }

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } = [];

    public List<string> Queries { get; } = [];

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken)
    {
        Executions++;
        Queries.Add(kql);
        return Task.FromResult(Rows);
    }
}

/// <summary>A knowledge search that counts its executions and returns a fixed set of hits.</summary>
internal sealed class RecordingKnowledgeSearch : IKnowledgeSearch
{
    public bool IsConfigured { get; init; }

    public IReadOnlyList<SearchHit> Hits { get; init; } = [];

    public int Executions { get; private set; }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        Executions++;
        return Task.FromResult(Hits);
    }
}

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
