using DevOpsCopilot.Application;

namespace DevOpsCopilot.Worker;

public class Worker(ProcessLogEvents processor) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        processor.RunAsync(stoppingToken);
}
