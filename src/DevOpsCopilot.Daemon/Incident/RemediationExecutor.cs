using Microsoft.Agents.AI.Workflows;

namespace DevOpsCopilot.Daemon.Incident;

internal sealed class RemediationExecutor : Executor<AgentHandoff, IncidentWorkflowResult>
{
    private readonly RemediationAgent _agent;

    public RemediationExecutor(RemediationAgent agent)
        : base("remediation-agent", ExecutorOptions.Default, declareCrossRunShareable: true)
    {
        _agent = agent;
    }

    public override ValueTask<IncidentWorkflowResult> HandleAsync(
        AgentHandoff message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_agent.Write(message.Diagnostic, message.Grounding));
    }
}
