using DevOpsCopilot.Daemon.Incident;

namespace DevOpsCopilot.Daemon;

public class Worker(ILogger<Worker> logger, IncidentWorkflow workflow) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Incident workflow {Workflow} is registered with {Diagnostic}, {Grounding}, and {Remediation}. It is not consuming hub logs.",
            workflow.GetType().Name,
            DiagnosticAgent.AgentName,
            RagGroundingAgent.AgentName,
            RemediationAgent.AgentName);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
