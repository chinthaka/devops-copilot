using Azure.Messaging.EventHubs;
using DevOpsCopilot.LogForwarder;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

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
