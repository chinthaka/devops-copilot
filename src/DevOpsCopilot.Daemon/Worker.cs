namespace DevOpsCopilot.Daemon;

public class Worker(LogEventProcessor processor) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        processor.RunAsync(stoppingToken);
}
