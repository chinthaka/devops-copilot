using DevOpsCopilot.Domain;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Application;

/// <summary>
/// Stage 1. Subscribes to the stream and, for each event, runs the pipeline and only then
/// checkpoints. The order matters: a checkpoint before the report would lose the event on restart.
/// </summary>
public sealed class ProcessLogEvents
{
    private readonly IEventSource _source;
    private readonly RunIncidentPipeline _pipeline;
    private readonly ILogger<ProcessLogEvents> _logger;

    public ProcessLogEvents(IEventSource source, RunIncidentPipeline pipeline, ILogger<ProcessLogEvents> logger)
    {
        _source = source;
        _pipeline = pipeline;
        _logger = logger;
    }

    public Task RunAsync(CancellationToken cancellationToken) =>
        _source.RunAsync(HandleEventAsync, cancellationToken);

    private async Task HandleEventAsync(ReceivedEvent received, CancellationToken cancellationToken)
    {
        var logEvent = received.Event;
        _logger.LogInformation(
            "Handling Event Hubs log event {Level} from {Category} with trace {TraceId} and span {SpanId}.",
            logEvent.Level,
            logEvent.Category,
            logEvent.TraceId,
            logEvent.SpanId);

        await _pipeline.RunAsync(logEvent, cancellationToken);
        await received.CheckpointAsync(cancellationToken);
    }
}
