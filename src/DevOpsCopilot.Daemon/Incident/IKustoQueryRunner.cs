namespace DevOpsCopilot.Daemon.Incident;

public interface IKustoQueryRunner
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken);
}
