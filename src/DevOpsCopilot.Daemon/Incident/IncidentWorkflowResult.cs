namespace DevOpsCopilot.Daemon.Incident;

public sealed record IncidentWorkflowResult(
    DiagnosticHandoff Diagnostic,
    GroundingHandoff Grounding,
    string DraftPath);
