using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// The stage order for one log event: gather evidence, ground it in history, diagnose, then write
/// the report. An event that does not start an incident runs no stage at all.
/// </summary>
public sealed class RunIncidentPipeline
{
    private readonly GatherEvidence _evidence;
    private readonly GroundInHistory _grounding;
    private readonly IReportWriter _reportWriter;
    private readonly Diagnose _diagnose;

    public RunIncidentPipeline(
        GatherEvidence evidence,
        GroundInHistory grounding,
        IReportWriter reportWriter,
        Diagnose? diagnose = null)
    {
        _evidence = evidence;
        _grounding = grounding;
        _reportWriter = reportWriter;
        _diagnose = diagnose ?? new Diagnose(UnconfiguredDiagnosisModel.Instance);
    }

    public async Task<IncidentWorkflowResult?> RunAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        if (!logEvent.StartsIncident())
        {
            return null;
        }

        var diagnostic = await _evidence.DiagnoseAsync(logEvent, cancellationToken);
        var grounding = await _grounding.GroundAsync(diagnostic, cancellationToken);
        var diagnosis = await _diagnose.DiagnoseAsync(diagnostic, grounding, cancellationToken);
        var handoff = new AgentHandoff(diagnostic, grounding, diagnosis);
        var draft = IncidentReport.Render(handoff.Diagnosis, handoff.Diagnostic, handoff.Grounding);
        var path = await _reportWriter.WriteAsync(handoff.Diagnostic, handoff.Grounding, draft, cancellationToken);
        return new IncidentWorkflowResult(handoff.Diagnostic, handoff.Grounding, path);
    }
}
