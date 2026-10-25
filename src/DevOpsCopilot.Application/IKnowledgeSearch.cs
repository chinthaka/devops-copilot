using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Searches the historical post-mortems for hits related to a diagnosed event. When search is not
/// configured the caller records the query that would have been run and gets no hits.
/// </summary>
public interface IKnowledgeSearch
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken);
}
