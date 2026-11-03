using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

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
