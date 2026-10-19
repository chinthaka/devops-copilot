namespace DevOpsCopilot.Daemon.Incident;

public sealed class PostmortemSearchTool
{
    public const string IndexName = "historical-postmortems";
    public const int MaxHits = 5;

    private static readonly string[] ServiceKeys =
    [
        "ServiceName",
        "Service",
        "RoleName",
        "cloud_RoleName",
        "Target",
        "DependencyName",
    ];

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

    public static IReadOnlyList<string> OverlapTerms(DiagnosticHandoff diagnostic)
    {
        var terms = new List<string>();
        Add(terms, FirstLine(diagnostic.Exception));
        Add(terms, diagnostic.Category);
        foreach (var row in diagnostic.Rows)
        {
            foreach (var pair in row)
            {
                if (ServiceKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) || pair.Key.Contains("service", StringComparison.OrdinalIgnoreCase))
                {
                    Add(terms, pair.Value);
                }
            }
        }

        return terms;
    }

    public static bool Overlaps(SearchHit hit, IReadOnlyList<string> terms)
    {
        var text = hit.Title + "\n" + hit.Excerpt;
        return terms.Any(term => term.Length >= 3 && text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
    }

    private static void Add(List<string> terms, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 0 && !terms.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            terms.Add(trimmed);
        }
    }
}
