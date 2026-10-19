using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace DevOpsCopilot.LogForwarder.Tests;

internal static class LogRecordBuilder
{
	public static void Emit(Action<ILogger> write, Action<LogRecord> useRecord)
	{
		using var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.AddOpenTelemetry(options =>
			{
				options.IncludeFormattedMessage = true;
				options.ParseStateValues = true;
				options.AddProcessor(new CaptureProcessor(useRecord));
			});
		});

		write(loggerFactory.CreateLogger("DevOpsCopilot.Api"));
	}

	private sealed class CaptureProcessor : BaseProcessor<LogRecord>
	{
		private readonly Action<LogRecord> _useRecord;

		public CaptureProcessor(Action<LogRecord> useRecord)
		{
			_useRecord = useRecord;
		}

		public override void OnEnd(LogRecord data)
		{
			_useRecord(data);
		}
	}
}
