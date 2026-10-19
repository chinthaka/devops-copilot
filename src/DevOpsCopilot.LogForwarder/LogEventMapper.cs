using System.Text.Json;
using OpenTelemetry.Logs;

namespace DevOpsCopilot.LogForwarder;

public static class LogEventMapper
{
	public static string ToJson(LogRecord record)
	{
		var payload = new
		{
			Timestamp = record.Timestamp,
			Level = record.LogLevel.ToString(),
			Category = record.CategoryName,
			Message = record.FormattedMessage,
			Exception = record.Exception?.ToString(),
			TraceId = record.TraceId.ToString(),
			SpanId = record.SpanId.ToString(),
			Attribute = record.Attributes?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString())
		};

		return JsonSerializer.Serialize(payload);
	}
}
