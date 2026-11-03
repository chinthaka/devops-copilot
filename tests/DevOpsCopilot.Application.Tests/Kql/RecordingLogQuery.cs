using DevOpsCopilot.Application.Interfaces;

namespace DevOpsCopilot.Application.Tests.Kql;

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
