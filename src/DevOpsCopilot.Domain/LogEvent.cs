namespace DevOpsCopilot.Domain;

public sealed record LogEvent(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? TraceId,
    string? SpanId,
    IReadOnlyDictionary<string, string>? Attribute = null)
{
    /// <summary>
    /// Whether this event starts an incident. An event with an exception always does; otherwise
    /// only <c>Error</c> and <c>Critical</c> do. A <c>Warning</c> on its own never does.
    /// </summary>
    public bool StartsIncident()
    {
        if (!string.IsNullOrWhiteSpace(Exception))
        {
            return true;
        }

        return Level.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || Level.Equals("Critical", StringComparison.OrdinalIgnoreCase);
    }
}
