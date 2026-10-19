using System.Diagnostics;
using System.Text.Json;
using DevOpsCopilot.LogForwarder;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.LogForwarder.Tests;

public class LogEventMapperTests
{
	[Fact]
	public void ToJson_serializes_the_log_event_fields()
	{
		// Arrange
		using var activity = new Activity("test");
		activity.SetIdFormat(ActivityIdFormat.W3C);
		activity.Start();
		string? jsonText = null;

		// Act
		LogRecordBuilder.Emit(
			logger => logger.Log(
				LogLevel.Error,
				new EventId(),
				new List<KeyValuePair<string, object?>> { new("service", "api") },
				new InvalidOperationException("boom"),
				(_, _) => "request failed"),
			record => jsonText = LogEventMapper.ToJson(record));

		// Assert
		using var json = JsonDocument.Parse(jsonText ?? throw new InvalidOperationException("The mapper did not return JSON."));
		var root = json.RootElement;
		Assert.Equal("Error", root.GetProperty("Level").GetString());
		Assert.Equal("DevOpsCopilot.Api", root.GetProperty("Category").GetString());
		Assert.Equal("request failed", root.GetProperty("Message").GetString());
		Assert.Contains("boom", root.GetProperty("Exception").GetString(), StringComparison.Ordinal);
		Assert.Equal(activity.TraceId.ToString(), root.GetProperty("TraceId").GetString());
		Assert.Equal(activity.SpanId.ToString(), root.GetProperty("SpanId").GetString());
		Assert.Equal("api", root.GetProperty("Attribute").GetProperty("service").GetString());
		Assert.True(root.TryGetProperty("Timestamp", out _));
	}

	[Fact]
	public void ToJson_keeps_null_exception_and_attributes()
	{
		// Arrange
		string? jsonText = null;

		// Act
		LogRecordBuilder.Emit(
			logger => logger.LogCritical("down"),
			record =>
			{
				record.Exception = null;
				record.Attributes = null;
				jsonText = LogEventMapper.ToJson(record);
			});

		// Assert
		using var json = JsonDocument.Parse(jsonText ?? throw new InvalidOperationException("The mapper did not return JSON."));
		var root = json.RootElement;
		Assert.Equal(JsonValueKind.Null, root.GetProperty("Exception").ValueKind);
		Assert.Equal(JsonValueKind.Null, root.GetProperty("Attribute").ValueKind);
	}
}
