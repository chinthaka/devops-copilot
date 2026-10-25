using System.Text;

namespace DevOpsCopilot.Domain;

/// <summary>
/// Renders one diagnosis into the seven report sections of pipeline.md § 7, in order: header,
/// impact, likely cause with confidence, evidence, similar incidents, suggested steps and open
/// questions. An unavailable tool is stated in its section and nothing is listed for it.
/// </summary>
public static class IncidentReport
{
    public static string Render(Diagnosis diagnosis, DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var builder = new StringBuilder();
        builder.Append("# Incident report: ").AppendLine(diagnosis.Title);
        builder.AppendLine();
        builder.AppendLine("- Status: Reported");
        builder.Append("- Incident id: ").AppendLine(diagnosis.IncidentId);
        builder.Append("- Trace id: ").AppendLine(string.IsNullOrWhiteSpace(diagnostic.TraceId) ? "(missing)" : diagnostic.TraceId);
        builder.AppendLine("- Hub: logs");
        builder.AppendLine();

        builder.AppendLine("## Impact");
        builder.AppendLine();
        builder.Append("- Services: ").AppendLine(NamesOrNone(diagnosis.Impact.Services));
        builder.Append("- Resources: ").AppendLine(NamesOrNone(diagnosis.Impact.Resources));
        builder.Append("- Event count: ").AppendLine(diagnosis.Impact.EventCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("- Window: ").AppendLine(diagnosis.Impact.Window);
        builder.AppendLine();

        builder.AppendLine("## Likely cause");
        builder.AppendLine();
        builder.Append(diagnosis.LikelyCause).Append(" (confidence: ").Append(diagnosis.Confidence.Value).AppendLine(")");
        builder.AppendLine();

        builder.AppendLine("## Evidence");
        builder.AppendLine();
        if (diagnostic.KustoStatus == "ok" && diagnostic.Findings.Count > 0)
        {
            foreach (var finding in diagnostic.Findings)
            {
                builder.Append("- ").AppendLine(finding);
            }
        }
        else
        {
            builder.AppendLine("KQL was unavailable. No rows were returned.");
        }

        builder.AppendLine();
        builder.AppendLine("## Similar incidents");
        builder.AppendLine();
        if (grounding.SearchStatus == "ok" && grounding.Hits.Count > 0)
        {
            foreach (var hit in grounding.Hits)
            {
                builder.Append("- ").Append(hit.Title).Append(": \"").Append(hit.Excerpt).AppendLine("\"");
            }
        }
        else
        {
            builder.AppendLine("Azure AI Search was unavailable. No historical post-mortems were returned.");
        }

        builder.AppendLine();
        builder.AppendLine("## Suggested steps");
        builder.AppendLine();
        AppendListOrNone(builder, diagnosis.SuggestedSteps);
        builder.AppendLine();
        builder.AppendLine("## Open questions");
        builder.AppendLine();
        AppendListOrNone(builder, diagnosis.OpenQuestions);
        return builder.ToString();
    }

    private static string NamesOrNone(IReadOnlyList<string> names)
    {
        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    private static void AppendListOrNone(StringBuilder builder, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            builder.AppendLine("- None.");
            return;
        }

        foreach (var line in lines)
        {
            builder.Append("- ").AppendLine(line);
        }
    }
}
