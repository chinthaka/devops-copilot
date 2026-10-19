namespace DevOpsCopilot.Daemon.Incident;

public sealed record DiagnosticHandoff(
    string EventId,
    string TraceId,
    string SpanId,
    string Category,
    string Exception,
    string KustoStatus,
    IReadOnlyList<string> Kql,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    IReadOnlyList<string> Findings);
