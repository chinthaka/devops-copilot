namespace DevOpsCopilot.Daemon.Incident;

public sealed record AgentHandoff(DiagnosticHandoff Diagnostic, GroundingHandoff Grounding);
