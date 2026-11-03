using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Domain.Pipeline;

public sealed record IncidentWorkflowResult(
    DiagnosticHandoff Diagnostic,
    GroundingHandoff Grounding,
    string DraftPath);
