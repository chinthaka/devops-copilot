using System.Text;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Turns the two handoffs into the text the model is given. The evidence is listed by index, so a
/// citation can be checked afterwards, and an unavailable tool is stated rather than left out, so
/// the model is never tempted to fill the gap.
/// </summary>
internal static class DiagnosisPrompt
{
    public const string System = """
        You are the diagnosis agent of an incident assistant. You diagnose; you never act.

        Rules:
        - Use only the evidence given below. Never invent a KQL row, a search hit, a service, a
          resource or a log line. If the evidence does not support a cause, say so and lower the
          confidence.
        - Every claim must cite the evidence it rests on. Cite a KQL row as the pair of indexes
          given below ("kql N", "row N"). Cite a past incident by the hit id given below.
        - Confidence is exactly one of "low", "medium" or "high". Use "low" when the evidence is
          thin or a tool was unavailable.
        - Suggested steps are written for a human engineer to carry out by hand. Never produce
          Bicep, ARM, Terraform, Kubernetes manifests, CLI commands or any script or template that
          would change a resource. Never suggest that the assistant itself change anything.
        - Answer with the JSON object only.
        """;

    public static string User(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Event");
        builder.AppendLine();
        builder.Append("- Event id: ").AppendLine(diagnostic.EventId);
        builder.Append("- Trace id: ").AppendLine(string.IsNullOrWhiteSpace(diagnostic.TraceId) ? "(missing)" : diagnostic.TraceId);
        builder.Append("- Category: ").AppendLine(diagnostic.Category);
        builder.Append("- Exception: ").AppendLine(string.IsNullOrWhiteSpace(diagnostic.Exception) ? "(none)" : diagnostic.Exception);
        builder.AppendLine();

        builder.AppendLine("# Evidence from Log Analytics");
        builder.AppendLine();
        if (diagnostic.KustoStatus != "ok")
        {
            builder.AppendLine("KQL was unavailable. No rows were returned. Do not cite any row.");
        }
        else
        {
            for (var index = 0; index < diagnostic.Kql.Count; index++)
            {
                builder.Append("- kql ").Append(index).Append(": ").AppendLine(diagnostic.Kql[index]);
            }

            builder.AppendLine();
            for (var index = 0; index < diagnostic.Rows.Count; index++)
            {
                builder.Append("- row ").Append(index).Append(": ").AppendLine(FormatRow(diagnostic.Rows[index]));
            }

            builder.AppendLine();
            foreach (var finding in diagnostic.Findings)
            {
                builder.Append("- finding: ").AppendLine(finding);
            }
        }

        builder.AppendLine();
        builder.AppendLine("# Similar past incidents");
        builder.AppendLine();
        if (grounding.SearchStatus != "ok" || grounding.Hits.Count == 0)
        {
            builder.AppendLine("Azure AI Search was unavailable. No past incidents were returned. Do not cite one.");
        }
        else
        {
            foreach (var hit in grounding.Hits)
            {
                builder.Append("- hit ").Append(hit.Id).Append(": ").Append(hit.Title).Append(" — \"").Append(hit.Excerpt).AppendLine("\"");
            }
        }

        builder.AppendLine();
        builder.AppendLine("# Task");
        builder.AppendLine();
        builder.AppendLine("Diagnose this one event. The impact covers exactly this event, so eventCount is 1.");
        builder.AppendLine("Cite every claim against the indexes above. Leave a list empty rather than filling it.");
        return builder.ToString();
    }

    private static string FormatRow(IReadOnlyDictionary<string, string> row) =>
        string.Join(", ", row.Select(pair => pair.Key + "=" + pair.Value));
}
