namespace DevOpsCopilot.Daemon.Incident;

public sealed record LogEvent(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? TraceId,
    string? SpanId,
    IReadOnlyDictionary<string, string>? Attribute = null);
