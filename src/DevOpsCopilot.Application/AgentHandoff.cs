using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>What the pipeline carries from grounding, through the diagnose stage, into the report.</summary>
public sealed record AgentHandoff(DiagnosticHandoff Diagnostic, GroundingHandoff Grounding, Diagnosis Diagnosis);
