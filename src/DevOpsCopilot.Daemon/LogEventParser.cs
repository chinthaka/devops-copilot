using System.Text.Json;
using DevOpsCopilot.Daemon.Incident;

namespace DevOpsCopilot.Daemon;

public static class LogEventParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static LogEvent Parse(ReadOnlyMemory<byte> body)
    {
        var logEvent = JsonSerializer.Deserialize<LogEvent>(body.Span, Options)
            ?? throw new JsonException("The Event Hubs message did not contain a log event.");

        if (logEvent.Timestamp == default
            || string.IsNullOrWhiteSpace(logEvent.Level)
            || string.IsNullOrWhiteSpace(logEvent.Category)
            || logEvent.Message is null)
        {
            throw new JsonException("The Event Hubs message is missing required log event fields.");
        }

        return logEvent;
    }
}
