using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Stage 5. Searches the historical post-mortems for the diagnosed event, keeps only the hits that
/// overlap it, and caps them. A search that returns nothing overlapping is reported as unavailable.
/// </summary>
public sealed class GroundInHistory
{
    private readonly IKnowledgeSearch _search;

    public GroundInHistory(IKnowledgeSearch search)
    {
        _search = search;
    }

    public async Task<GroundingHandoff> GroundAsync(DiagnosticHandoff diagnostic, CancellationToken cancellationToken = default)
    {
        var terms = PostmortemOverlap.Terms(diagnostic);
        var query = string.Join(' ', terms.Where(term => term.Length > 0));
        IReadOnlyList<string> queries = [query];
        if (!_search.IsConfigured)
        {
            return new GroundingHandoff(diagnostic.EventId, "unavailable", PostmortemOverlap.IndexName, queries, []);
        }

        var returned = await _search.SearchAsync(query, cancellationToken);
        var hits = returned.Where(hit => PostmortemOverlap.Overlaps(hit, terms)).Take(PostmortemOverlap.MaxHits).ToArray();
        var status = hits.Length > 0 ? "ok" : "unavailable";
        return new GroundingHandoff(diagnostic.EventId, status, PostmortemOverlap.IndexName, queries, hits);
    }
}
