using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class RemediationAgent
{
    public const string AgentName = "RemediationAgent";

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

    public static string Draft(DiagnosticHandoff diagnostic, GroundingHandoff grounding) =>
        IncidentDraft.Render(diagnostic, grounding);
}
