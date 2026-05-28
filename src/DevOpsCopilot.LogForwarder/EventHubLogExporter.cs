using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DevOpsCopilot.LogForwarder
{
	public class EventHubLogExporter : BaseExporter<LogRecord>
	{
		private readonly EventHubProducerClient _producer;

		public EventHubLogExporter(EventHubProducerClient producer)
		{
			_producer = producer;
		}

		public override ExportResult Export(in Batch<LogRecord> batch)
		{
			var events = new List<EventData>();

			foreach(var record in batch)
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

				var json = JsonSerializer.Serialize(payload);
				events.Add(new EventData(Encoding.UTF8.GetBytes(json)));
			}

			try
			{
				_producer.SendAsync(events).GetAwaiter().GetResult();

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
}
