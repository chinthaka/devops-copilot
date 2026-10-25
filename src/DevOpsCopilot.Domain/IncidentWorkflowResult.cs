namespace DevOpsCopilot.Domain;

public sealed record IncidentWorkflowResult(
    DiagnosticHandoff Diagnostic,
    GroundingHandoff Grounding,
    string DraftPath);
