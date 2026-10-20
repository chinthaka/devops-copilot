using Azure.Messaging.EventHubs;
using DevOpsCopilot.LogForwarder;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;
using System.Text.Json;

namespace DevOpsCopilot.LogForwarder.Tests;

public class EventHubLogExporterTests
{
	[Fact]
	public void Export_returns_success_when_the_sender_accepts_the_batch()
	{
		// Arrange
		var sender = new FakeSender();
		var exporter = new EventHubLogExporter(sender);
		ExportResult result = default;

		// Act
		LogRecordBuilder.Emit(
			logger => logger.LogError("failed"),
			record => result = exporter.Export(BatchOf(record)));

		// Assert
		Assert.Equal(ExportResult.Success, result);
		Assert.Single(sender.Sent);
	}

	[Fact]
	public void Export_returns_failure_when_the_sender_throws()
	{
		// Arrange
		var sender = new FakeSender { Error = new InvalidOperationException("hub down") };
		var exporter = new EventHubLogExporter(sender);
		ExportResult result = default;

		// Act
		LogRecordBuilder.Emit(
			logger => logger.LogError("failed"),
			record => result = exporter.Export(BatchOf(record)));

		// Assert
		Assert.Equal(ExportResult.Failure, result);
		Assert.Empty(sender.Sent);
	}

	[Fact]
	public void Export_preserves_w3c_trace_context_in_the_event_body()
	{
		// Arrange
		var sender = new FakeSender();
		var exporter = new EventHubLogExporter(sender);
		using var activity = new Activity("checkout")
			.SetIdFormat(ActivityIdFormat.W3C)
			.SetParentId("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")
			.Start();

		// Act
		LogRecordBuilder.Emit(
			logger => logger.LogError("checkout failed"),
			record => exporter.Export(BatchOf(record)));

		// Assert
		using var payload = JsonDocument.Parse(sender.Sent.Single().EventBody);
		Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", payload.RootElement.GetProperty("TraceId").GetString());
		Assert.Equal(activity.SpanId.ToString(), payload.RootElement.GetProperty("SpanId").GetString());
	}

	private static Batch<LogRecord> BatchOf(LogRecord record)
	{
		var records = new[] { record };
		return new Batch<LogRecord>(records, records.Length);
	}

	private sealed class FakeSender : IEventDataSender
	{
		public List<EventData> Sent { get; } = [];

		public Exception? Error { get; init; }

		public Task SendAsync(IReadOnlyCollection<EventData> events)
		{
			if (Error is not null)
			{
				throw Error;
			}

			Sent.AddRange(events);
			return Task.CompletedTask;
		}
	}
}
