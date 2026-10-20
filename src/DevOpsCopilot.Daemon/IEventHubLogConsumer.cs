namespace DevOpsCopilot.Daemon;

public interface IEventHubLogConsumer
{
    Task RunAsync(
        Func<EventHubLogMessage, CancellationToken, Task> handleEvent,
        CancellationToken cancellationToken);
}
