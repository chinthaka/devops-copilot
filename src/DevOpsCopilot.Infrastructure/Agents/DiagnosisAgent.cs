using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Domain.Pipeline;
using DevOpsCopilot.Domain.Reporting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure.Agents;

/// <summary>
/// The Microsoft Agent Framework surface over the diagnose-and-report path. The diagnosis comes
/// from the Diagnose stage and the document from the domain renderer, so the agent writes the same
/// seven-section report as the pipeline and can neither invent evidence nor change the shape.
/// </summary>
public sealed class DiagnosisAgent
{
    public const string AgentName = "DiagnosisAgent";

    public const string Description = "Diagnoses one error event and writes the seven-section report.";

    public const string Instructions =
        "Diagnose from the handoffs only: cite only their findings, rows and hits, and say when a " +
        "tool was unavailable instead of inventing evidence. Never produce Bicep, ARM, Terraform " +
        "or any script or template that changes resources; write suggested steps for an engineer " +
        "to carry out.";

    private readonly Diagnose _diagnose;
    private readonly IReportWriter _reportWriter;

    public DiagnosisAgent(Diagnose diagnose, IReportWriter reportWriter, IChatClient? chatClient = null)
    {
        _diagnose = diagnose;
        _reportWriter = reportWriter;
        if (chatClient is not null)
        {
            ChatAgent = new ChatClientAgent(chatClient, Instructions, AgentName, Description);
        }
    }

    public AIAgent? ChatAgent { get; }

    public async Task<IncidentWorkflowResult> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken = default)
    {
        var diagnosis = await _diagnose.DiagnoseAsync(diagnostic, grounding, cancellationToken);
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);
        var path = await _reportWriter.WriteAsync(diagnostic, grounding, report, cancellationToken);
        return new IncidentWorkflowResult(diagnostic, grounding, path);
    }
}
