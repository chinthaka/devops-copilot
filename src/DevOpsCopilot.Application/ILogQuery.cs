using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Reads live evidence from the log store with one read-only query. When the store is not
/// configured the caller records the call that would have been made and gets nothing back.
/// </summary>
public interface ILogQuery
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken);
}
