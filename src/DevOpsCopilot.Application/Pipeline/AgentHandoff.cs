using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Pipeline;

/// <summary>What the pipeline carries from grounding, through the diagnose stage, into the report.</summary>
public sealed record AgentHandoff(DiagnosticHandoff Diagnostic, GroundingHandoff Grounding, Diagnosis Diagnosis);
