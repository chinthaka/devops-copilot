using Microsoft.Agents.AI.Workflows;

namespace DevOpsCopilot.Daemon.Incident;

internal sealed class GroundingExecutor : Executor<DiagnosticHandoff, AgentHandoff>
{
    private readonly RagGroundingAgent _agent;

    public GroundingExecutor(RagGroundingAgent agent)
        : base("rag-grounding-agent", ExecutorOptions.Default, declareCrossRunShareable: true)
    {
        _agent = agent;
    }

    public override async ValueTask<AgentHandoff> HandleAsync(
        DiagnosticHandoff message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var grounding = await _agent.GroundAsync(message, cancellationToken);
        return new AgentHandoff(message, grounding);
    }
}
