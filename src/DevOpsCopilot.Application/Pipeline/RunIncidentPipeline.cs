using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Pipeline;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Application.Pipeline;

/// <summary>
/// The stage order for one log event: gather evidence, ground it in history, diagnose, then write
/// the report, then notify every registered channel. The report is always written before any
/// channel is called, so a channel outage never costs the report. An event that does not start an incident runs no stage at all.
/// </summary>
public sealed class RunIncidentPipeline
{
    private readonly GatherEvidence _evidence;
    private readonly GroundInHistory _grounding;
    private readonly IReportWriter _reportWriter;
    private readonly Diagnose _diagnose;
    private readonly NotifyChannels _notify;
    private readonly ITemplateSource? _templates;

    public RunIncidentPipeline(
        GatherEvidence evidence,
        GroundInHistory grounding,
        IReportWriter reportWriter,
        Diagnose? diagnose = null,
        NotifyChannels? notify = null,
        ITemplateSource? templates = null)
    {
        _evidence = evidence;
        _grounding = grounding;
        _reportWriter = reportWriter;
        // The fallbacks never wait: an unconfigured model is never called and no channel is
        // notified, so neither timeout is ever started.
        _diagnose = diagnose ?? new Diagnose(UnconfiguredDiagnosisModel.Instance, Timeout.InfiniteTimeSpan);
        _notify = notify ?? new NotifyChannels([], Timeout.InfiniteTimeSpan);
        _templates = templates;
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
        var template = await ReportTemplateAsync(cancellationToken);
        var draft = IncidentReport.Render(handoff.Diagnosis, handoff.Diagnostic, handoff.Grounding, template);
        var path = await _reportWriter.WriteAsync(handoff.Diagnostic, handoff.Grounding, draft, cancellationToken);
        var notification = IncidentNotification.From(handoff.Diagnosis, handoff.Diagnostic, handoff.Grounding, path);
        var deliveries = await _notify.NotifyAsync(notification, cancellationToken);
        return new IncidentWorkflowResult(handoff.Diagnostic, handoff.Grounding, path) { Deliveries = deliveries };
    }

    private async Task<string?> ReportTemplateAsync(CancellationToken cancellationToken)
    {
        if (_templates is null)
        {
            return null;
        }

        try
        {
            return await _templates.GetAsync(TemplateNames.Report, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The source should not throw; if it does, the default template still produces the report.
            return null;
        }
    }
}
