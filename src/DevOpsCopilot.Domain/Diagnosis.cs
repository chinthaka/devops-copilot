using System.Globalization;

namespace DevOpsCopilot.Domain;

/// <summary>A pointer into the diagnostic handoff: the KQL entry and the row a claim rests on.</summary>
public sealed record EvidenceCitation(int Kql, int Row);

/// <summary>What one incident touched: its services, resources, event count and time window.</summary>
public sealed record Impact(
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Resources,
    int EventCount,
    string Window);

/// <summary>
/// The structured result of the diagnose stage. Until incidents exist (M12), the incident id is
/// the event id. The deterministic form is built from the handoffs alone and invents nothing.
/// </summary>
public sealed record Diagnosis(
    string IncidentId,
    string Title,
    string LikelyCause,
    Confidence Confidence,
    Impact Impact,
    IReadOnlyList<EvidenceCitation> Evidence,
    IReadOnlyList<string> SimilarIncidents,
    IReadOnlyList<string> SuggestedSteps,
    IReadOnlyList<string> OpenQuestions)
{
    public static Diagnosis Deterministic(DiagnosticHandoff diagnostic, GroundingHandoff grounding, string reason)
    {
        var window = EventIdentity.ParseEventId(diagnostic.EventId)
            .ToString("O", CultureInfo.InvariantCulture);
        var impact = new Impact(
            string.IsNullOrWhiteSpace(diagnostic.Category) ? [] : [diagnostic.Category],
            [],
            1,
            window);

        var steps = new List<string>
        {
            $"Inspect the exception and surrounding logs for {diagnostic.Category} around {window}.",
        };
        if (grounding.Hits.Count > 0)
        {
            steps.Add("Compare with the similar incidents listed in this report.");
        }

        var openQuestions = new List<string>();
        if (diagnostic.KustoStatus != "ok")
        {
            openQuestions.Add("KQL did not return rows for this event.");
        }

        if (grounding.SearchStatus != "ok")
        {
            openQuestions.Add("Azure AI Search did not return overlapping historical post-mortems.");
        }

        if (string.IsNullOrWhiteSpace(diagnostic.TraceId))
        {
            openQuestions.Add("Trace id is missing.");
        }

        openQuestions.Add(reason);

        return new Diagnosis(
            diagnostic.EventId,
            TitleFor(diagnostic),
            "Not determined from the available evidence.",
            Confidence.Low,
            impact,
            Citations(diagnostic),
            grounding.Hits.Select(hit => hit.Id).ToArray(),
            steps,
            openQuestions);
    }

    private static IReadOnlyList<EvidenceCitation> Citations(DiagnosticHandoff diagnostic)
    {
        var citations = new List<EvidenceCitation>();
        for (var row = 0; row < diagnostic.Rows.Count; row++)
        {
            var kql = 0;
            if (row < diagnostic.Findings.Count)
            {
                var named = diagnostic.Kql
                    .Select((query, index) => (query, index))
                    .Where(entry => entry.query.Length > 0
                        && diagnostic.Findings[row].Contains(entry.query, StringComparison.Ordinal))
                    .Select(entry => (int?)entry.index)
                    .FirstOrDefault();
                kql = named ?? 0;
            }

            citations.Add(new EvidenceCitation(kql, row));
        }

        return citations;
    }

    private static string TitleFor(DiagnosticHandoff diagnostic)
    {
        var source = FirstLine(diagnostic.Exception);
        if (string.IsNullOrWhiteSpace(source))
        {
            source = diagnostic.Category;
        }

        var trimmed = source.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
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
}
