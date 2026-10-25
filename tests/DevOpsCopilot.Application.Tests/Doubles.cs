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

/// <summary>
/// A diagnosis model that returns, refuses, throws or waits, so the stage's four fallback paths
/// can be driven from a test.
/// </summary>
internal sealed class RecordingDiagnosisModel : IDiagnosisModel
{
    public bool IsConfigured { get; init; } = true;

    public Diagnosis? Proposed { get; init; }

    public Exception? Throws { get; init; }

    public bool WaitsForCancellation { get; init; }

    public int Proposals { get; private set; }

    public async Task<Diagnosis?> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken)
    {
        Proposals++;
        if (Throws is not null)
        {
            throw Throws;
        }

        if (WaitsForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return Proposed;
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
