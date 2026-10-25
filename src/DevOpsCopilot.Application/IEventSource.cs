namespace DevOpsCopilot.Application;

/// <summary>
/// Delivers events from the stream until cancelled. The handler is awaited for each event, so an
/// implementation must not checkpoint on the caller's behalf.
/// </summary>
public interface IEventSource
{
    Task RunAsync(
        Func<ReceivedEvent, CancellationToken, Task> handleEvent,
        CancellationToken cancellationToken);
}
