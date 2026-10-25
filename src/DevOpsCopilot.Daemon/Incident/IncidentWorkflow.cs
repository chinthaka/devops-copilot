using Microsoft.Agents.AI.Workflows;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class IncidentWorkflow
{
    private readonly Workflow _workflow;

    public IncidentWorkflow(DiagnosticAgent diagnostic, RagGroundingAgent grounding, RemediationAgent remediation)
    {
        var diagnosticExecutor = new DiagnosticExecutor(diagnostic);
        var groundingExecutor = new GroundingExecutor(grounding);
        var remediationExecutor = new RemediationExecutor(remediation);
        _workflow = new WorkflowBuilder(diagnosticExecutor)
            .AddEdge(diagnosticExecutor, groundingExecutor)
            .AddEdge(groundingExecutor, remediationExecutor)
            .WithOutputFrom(remediationExecutor)
            .Build();
    }

    public static bool ShouldStart(LogEvent logEvent) => logEvent.StartsIncident();

    public async Task<IncidentWorkflowResult?> RunAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        if (!ShouldStart(logEvent))
        {
            return null;
        }

        await using var run = await InProcessExecution.Default.RunAsync(_workflow, logEvent, sessionId: null, cancellationToken);
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            if (workflowEvent is WorkflowOutputEvent output && output.Is<IncidentWorkflowResult>(out var result))
            {
                return result;
            }
        }

        throw new InvalidOperationException("The incident workflow finished without a draft.");
    }
}
