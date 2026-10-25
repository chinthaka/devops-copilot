using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>The pair of handoffs the pipeline carries from grounding into the report stage.</summary>
public sealed record AgentHandoff(DiagnosticHandoff Diagnostic, GroundingHandoff Grounding);
