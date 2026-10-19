using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class RagGroundingAgent
{
    public const string AgentName = "RagGroundingAgent";

    private readonly PostmortemSearchTool _tool;

    public RagGroundingAgent(PostmortemSearchTool tool, IChatClient? chatClient = null)
    {
        _tool = tool;
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
        _tool.GroundAsync(diagnostic, cancellationToken);
}
