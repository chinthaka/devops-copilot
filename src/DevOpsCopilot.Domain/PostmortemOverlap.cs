namespace DevOpsCopilot.Domain;

/// <summary>
/// The grounding rules: which index is searched, how many hits are kept, and what counts as a hit
/// overlapping the diagnosed event. A hit that overlaps nothing is dropped rather than reported.
/// </summary>
public static class PostmortemOverlap
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

    public static IReadOnlyList<string> Terms(DiagnosticHandoff diagnostic)
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
