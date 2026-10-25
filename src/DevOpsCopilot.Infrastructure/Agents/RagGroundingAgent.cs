using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure.Agents;

/// <summary>
/// The Microsoft Agent Framework surface over the grounding stage. The tool keeps only overlapping
/// hits, so a model can quote them but cannot add an incident that search did not return.
/// </summary>
public sealed class RagGroundingAgent
{
    public const string AgentName = "RagGroundingAgent";

    private readonly GroundInHistory _grounding;

    public RagGroundingAgent(GroundInHistory grounding, IChatClient? chatClient = null)
    {
        _grounding = grounding;
        SearchTool = AIFunctionFactory.Create(
            (DiagnosticHandoff diagnostic, CancellationToken cancellationToken) => GroundAsync(diagnostic, cancellationToken),
            "search_postmortems",
            "Searches the historical-postmortems index and keeps overlapping hits. Does not invent incidents.");
        if (chatClient is not null)
        {
            ChatAgent = new ChatClientAgent(
                chatClient,
                "Call search_postmortems. Quote excerpts from the tool. Do not add hits.",
                AgentName,
                "Grounds a diagnosed event in historical post-mortems.",
                new List<AITool> { SearchTool });
        }
    }

    public AIFunction SearchTool { get; }

    public AIAgent? ChatAgent { get; }

    public Task<GroundingHandoff> GroundAsync(DiagnosticHandoff diagnostic, CancellationToken cancellationToken) =>
        _grounding.GroundAsync(diagnostic, cancellationToken);
}
