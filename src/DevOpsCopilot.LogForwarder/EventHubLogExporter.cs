using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using DevOpsCopilot.LogForwarder.Interfaces;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Text;

namespace DevOpsCopilot.LogForwarder;

public class EventHubLogExporter : BaseExporter<LogRecord>
{
	private readonly IEventDataSender _sender;

	public EventHubLogExporter(EventHubProducerClient producer)
		: this(new EventHubEventDataSender(producer))
	{
	}

	public EventHubLogExporter(IEventDataSender sender)
	{
		_sender = sender;
	}

	public override ExportResult Export(in Batch<LogRecord> batch)
	{
		var events = new List<EventData>();

		foreach (var record in batch)
		{
			events.Add(new EventData(Encoding.UTF8.GetBytes(LogEventMapper.ToJson(record))));
		}

		try
		{
			_sender.SendAsync(events).GetAwaiter().GetResult();

			return ExportResult.Success;
		}
		catch
		{
			return ExportResult.Failure;
		}
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
	public bool IsEnabled(LogLevel logLevel) => true;
}
