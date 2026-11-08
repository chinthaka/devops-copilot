using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using System.Globalization;

namespace DevOpsCopilot.Domain.Reporting;

/// <summary>
/// The channel-neutral content of one notification about one report. Built once from the
/// diagnosis and the two handoffs, so every channel renders the same facts and none can add its
/// own. An unavailable tool is stated, exactly as <see cref="IncidentReport"/> states it, and
/// nothing is listed for it.
/// </summary>
public sealed record IncidentNotification(
    string IncidentId,
    string Title,
    string LikelyCause,
    Confidence Confidence,
    string Impact,
    IReadOnlyList<string> Evidence,
    string? EvidenceUnavailable,
    IReadOnlyList<string> SimilarIncidents,
    string? SimilarIncidentsUnavailable,
    IReadOnlyList<string> SuggestedSteps,
    IReadOnlyList<string> OpenQuestions,
    string ReportLocation)
{
    public static IncidentNotification From(
        Diagnosis diagnosis,
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string reportPath)
    {
        var evidenceAvailable = diagnostic.KustoStatus == "ok" && diagnostic.Findings.Count > 0;
        var searchAvailable = grounding.SearchStatus == "ok" && grounding.Hits.Count > 0;

        return new IncidentNotification(
            diagnosis.IncidentId,
            diagnosis.Title,
            diagnosis.LikelyCause,
            diagnosis.Confidence,
            ImpactLine(diagnosis.Impact),
            evidenceAvailable ? diagnostic.Findings.ToArray() : [],
            evidenceAvailable ? null : IncidentReport.KqlUnavailable,
            searchAvailable ? grounding.Hits.Select(hit => $"{hit.Title}: \"{hit.Excerpt}\"").ToArray() : [],
            searchAvailable ? null : IncidentReport.SearchUnavailable,
            diagnosis.SuggestedSteps.ToArray(),
            diagnosis.OpenQuestions.ToArray(),
            reportPath);
    }

    private static string ImpactLine(Impact impact)
    {
        var count = impact.EventCount.ToString(CultureInfo.InvariantCulture);
        return $"Services: {NamesOrNone(impact.Services)}. Resources: {NamesOrNone(impact.Resources)}. "
            + $"Event count: {count}. Window: {impact.Window}.";
    }

    private static string NamesOrNone(IReadOnlyList<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);
}
