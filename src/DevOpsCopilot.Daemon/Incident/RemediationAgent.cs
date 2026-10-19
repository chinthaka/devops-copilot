using System.Globalization;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class RemediationAgent
{
    public const string AgentName = "RemediationAgent";

    private static readonly string[] ChangeVerbs =
    [
        "scale",
        "update",
        "increase",
        "decrease",
        "restart",
        "resize",
        "enable",
        "disable",
    ];

    private readonly IncidentOptions _options;

    public RemediationAgent(IncidentOptions options, IChatClient? chatClient = null)
    {
        _options = options;
        if (chatClient is not null)
        {
            ChatAgent = new ChatClientAgent(
                chatClient,
                "Format the draft from the handoffs only. Do not call Kusto or search, and do not name a resource a finding did not name.",
                AgentName,
                "Writes the Slack, Teams, and Bicep draft.");
        }
    }

    public AIAgent? ChatAgent { get; }

    public IncidentWorkflowResult Write(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var timestamp = KustoQueryTool.ParseEventId(diagnostic.EventId);
        var directory = _options.DraftsDirectory;
        Directory.CreateDirectory(directory);
        var day = timestamp.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, day + "-" + diagnostic.EventId + ".md");
        File.WriteAllText(path, Draft(diagnostic, grounding));
        return new IncidentWorkflowResult(diagnostic, grounding, path);
    }

    public static string Draft(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var title = Title(diagnostic);
        var builder = new StringBuilder();
        builder.Append("# Incident draft: ").Append(title).AppendLine();
        builder.AppendLine();
        builder.AppendLine("- Status: Draft");
        builder.Append("- Event id: ").AppendLine(diagnostic.EventId);
        builder.Append("- Trace id: ").AppendLine(string.IsNullOrWhiteSpace(diagnostic.TraceId) ? "(missing)" : diagnostic.TraceId);
        builder.AppendLine("- Hub: logs");
        builder.AppendLine();
        builder.AppendLine("## Diagnosis");
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
            builder.AppendLine("Kusto was unavailable. No rows were returned.");
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
        builder.AppendLine("## Slack");
        builder.AppendLine();
        builder.AppendLine(Slack(diagnostic, grounding));
        builder.AppendLine("## Teams");
        builder.AppendLine();
        builder.AppendLine(Teams(diagnostic, grounding));
        builder.AppendLine("## Bicep");
        builder.AppendLine();
        builder.AppendLine(Bicep(diagnostic.Findings));
        builder.AppendLine("## Open questions");
        builder.AppendLine();
        builder.Append(OpenQuestions(diagnostic, grounding));
        return builder.ToString();
    }

    private static string Title(DiagnosticHandoff diagnostic)
    {
        var source = FirstLine(diagnostic.Exception);
        if (string.IsNullOrWhiteSpace(source))
        {
            source = diagnostic.Category;
        }

        var trimmed = source.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static string Slack(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var builder = new StringBuilder();
        builder.Append("*Draft* ").AppendLine(diagnostic.EventId);
        AppendFindings(builder, diagnostic, "Kusto was unavailable.");
        AppendHits(builder, grounding, "Search was unavailable.");
        return builder.ToString().TrimEnd();
    }

    private static string Teams(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var builder = new StringBuilder();
        builder.Append("**Draft** ").AppendLine(diagnostic.EventId);
        builder.AppendLine();
        AppendFindings(builder, diagnostic, "Kusto was unavailable.");
        AppendHits(builder, grounding, "Search was unavailable.");
        return builder.ToString().TrimEnd();
    }

    private static void AppendFindings(StringBuilder builder, DiagnosticHandoff diagnostic, string unavailable)
    {
        if (diagnostic.Findings.Count == 0)
        {
            builder.AppendLine(unavailable);
            return;
        }

        foreach (var finding in diagnostic.Findings)
        {
            builder.Append("• ").AppendLine(finding);
        }
    }

    private static void AppendHits(StringBuilder builder, GroundingHandoff grounding, string unavailable)
    {
        if (grounding.Hits.Count == 0)
        {
            builder.AppendLine(unavailable);
            return;
        }

        foreach (var hit in grounding.Hits)
        {
            builder.Append("• ").Append(hit.Title).Append(": ").AppendLine(hit.Excerpt);
        }
    }

    private static string Bicep(IReadOnlyList<string> findings)
    {
        var named = findings.Where(NamesResourceChange).ToArray();
        if (named.Length == 0)
        {
            return "No template. No finding names a resource change.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Proposal only. This draft does not deploy the template.");
        builder.AppendLine("```bicep");
        foreach (var finding in named)
        {
            builder.Append("// ").AppendLine(finding.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal));
        }

        builder.Append("```");
        return builder.ToString();
    }

    private static bool NamesResourceChange(string finding)
    {
        var namesResource = finding.Contains("/subscriptions/", StringComparison.OrdinalIgnoreCase)
            || finding.Contains("Microsoft.", StringComparison.Ordinal);
        return namesResource && ChangeVerbs.Any(verb => finding.Contains(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static string OpenQuestions(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        var lines = new List<string>();
        if (diagnostic.KustoStatus != "ok")
        {
            lines.Add("- Kusto did not return rows for this event.");
        }

        if (grounding.SearchStatus != "ok")
        {
            lines.Add("- Azure AI Search did not return overlapping historical post-mortems.");
        }

        if (string.IsNullOrWhiteSpace(diagnostic.TraceId))
        {
            lines.Add("- Trace id is missing.");
        }

        if (lines.Count == 0)
        {
            lines.Add("- None.");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
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
