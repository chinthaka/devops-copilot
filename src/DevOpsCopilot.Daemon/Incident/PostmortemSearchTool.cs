namespace DevOpsCopilot.Daemon.Incident;

public sealed class PostmortemSearchTool
{
    public const string IndexName = PostmortemOverlap.IndexName;
    public const int MaxHits = PostmortemOverlap.MaxHits;

    private readonly IPostmortemSearch _search;

    public PostmortemSearchTool(IPostmortemSearch search)
    {
        _search = search;
    }

    public async Task<GroundingHandoff> GroundAsync(DiagnosticHandoff diagnostic, CancellationToken cancellationToken = default)
    {
        var terms = OverlapTerms(diagnostic);
        var query = string.Join(' ', terms.Where(term => term.Length > 0));
        IReadOnlyList<string> queries = [query];
        if (!_search.IsConfigured)
        {
            return new GroundingHandoff(diagnostic.EventId, "unavailable", IndexName, queries, []);
        }

        var returned = await _search.SearchAsync(query, cancellationToken);
        var hits = returned.Where(hit => Overlaps(hit, terms)).Take(MaxHits).ToArray();
        var status = hits.Length > 0 ? "ok" : "unavailable";
        return new GroundingHandoff(diagnostic.EventId, status, IndexName, queries, hits);
    }

    public static IReadOnlyList<string> OverlapTerms(DiagnosticHandoff diagnostic) =>
        PostmortemOverlap.Terms(diagnostic);

    public static bool Overlaps(SearchHit hit, IReadOnlyList<string> terms) =>
        PostmortemOverlap.Overlaps(hit, terms);
}
