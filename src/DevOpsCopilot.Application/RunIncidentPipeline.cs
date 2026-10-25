using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// The stage order for one log event: gather evidence, then ground it in history, then write the
/// report. An event that does not start an incident runs no stage at all.
/// </summary>
public sealed class RunIncidentPipeline
{
    private readonly GatherEvidence _evidence;
    private readonly GroundInHistory _grounding;
    private readonly IReportWriter _reportWriter;

    public RunIncidentPipeline(GatherEvidence evidence, GroundInHistory grounding, IReportWriter reportWriter)
    {
        _evidence = evidence;
        _grounding = grounding;
        _reportWriter = reportWriter;
    }

    public async Task<IncidentWorkflowResult?> RunAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        if (!logEvent.StartsIncident())
        {
            return null;
        }

        var diagnostic = await _evidence.DiagnoseAsync(logEvent, cancellationToken);
        var grounding = await _grounding.GroundAsync(diagnostic, cancellationToken);
        var handoff = new AgentHandoff(diagnostic, grounding);

        // The diagnose stage (M5 task group 4) will choose between the model and this path.
        // Until it lands, every report is the deterministic diagnosis.
        var diagnosis = DiagnosisRules.ApplyConfidenceRule(
            Diagnosis.Deterministic(diagnostic, grounding, "The diagnosis model was unavailable."),
            diagnostic,
            grounding);
        var draft = IncidentReport.Render(diagnosis, handoff.Diagnostic, handoff.Grounding);
        var path = await _reportWriter.WriteAsync(handoff.Diagnostic, handoff.Grounding, draft, cancellationToken);
        return new IncidentWorkflowResult(handoff.Diagnostic, handoff.Grounding, path);
    }
}
