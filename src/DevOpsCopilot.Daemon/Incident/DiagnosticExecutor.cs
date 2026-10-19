using Microsoft.Agents.AI.Workflows;

namespace DevOpsCopilot.Daemon.Incident;

internal sealed class DiagnosticExecutor : Executor<LogEvent, DiagnosticHandoff>
{
    private readonly DiagnosticAgent _agent;

    public DiagnosticExecutor(DiagnosticAgent agent)
        : base("diagnostic-agent", ExecutorOptions.Default, declareCrossRunShareable: true)
    {
        _agent = agent;
    }

    public override async ValueTask<DiagnosticHandoff> HandleAsync(
        LogEvent message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // The fixed tool produces the handoff. A chat model may summarize that text later and must not invent rows.
        return await _agent.DiagnoseAsync(message, cancellationToken);
    }
}
