using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure.Agents;

/// <summary>
/// The Microsoft Agent Framework surface over the report stage. Rendering is the domain's, writing
/// is the report writer's; this type only holds the agent the chat model will use.
/// </summary>
public sealed class RemediationAgent
{
    public const string AgentName = "RemediationAgent";

    private readonly IReportWriter _reportWriter;

    public RemediationAgent(IReportWriter reportWriter, IChatClient? chatClient = null)
    {
        _reportWriter = reportWriter;
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

    public async Task<IncidentWorkflowResult> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken = default)
    {
        var draft = IncidentDraft.Render(diagnostic, grounding);
        var path = await _reportWriter.WriteAsync(diagnostic, grounding, draft, cancellationToken);
        return new IncidentWorkflowResult(diagnostic, grounding, path);
    }
}
