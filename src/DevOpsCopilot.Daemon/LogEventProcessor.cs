using DevOpsCopilot.Daemon.Incident;

namespace DevOpsCopilot.Daemon;

public sealed class LogEventProcessor(
    IEventHubLogConsumer consumer,
    IncidentWorkflow workflow,
    ILogger<LogEventProcessor> logger)
{
    public Task RunAsync(CancellationToken cancellationToken) =>
        consumer.RunAsync(HandleEventAsync, cancellationToken);

    private async Task HandleEventAsync(EventHubLogMessage message, CancellationToken cancellationToken)
    {
        var logEvent = LogEventParser.Parse(message.Body);
        logger.LogInformation(
            "Handling Event Hubs log event {Level} from {Category} with trace {TraceId} and span {SpanId}.",
            logEvent.Level,
            logEvent.Category,
            logEvent.TraceId,
            logEvent.SpanId);

        await workflow.RunAsync(logEvent, cancellationToken);
        await message.CheckpointAsync(cancellationToken);
    }
}
