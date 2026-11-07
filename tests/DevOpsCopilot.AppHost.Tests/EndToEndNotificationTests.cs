using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using DevOpsCopilot.AppHost.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// Stage 7's second half against the real AppHost: with no channel configured, an error event
/// still ends as a report file, and the worker records every channel unavailable instead of
/// failing the run (AC-2, AC-3). Needs Docker.
/// </summary>
[Trait("Category", "RequiresDocker")]
[Collection(AppHostCollection.Name)]
public class EndToEndNotificationTests(AppHostFixture fixture)
{
    private static readonly string[] Channels = ["teams", "slack", "discord", "email"];

    [DockerRequiredFact]
    public async Task EndToEnd_AnErrorEventWithNoChannelConfigured_WritesTheReportAndRecordsEveryChannelUnavailable()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var app = await fixture.StartAsync(cancellation.Token);
        await app.ResourceNotifications.WaitForResourceAsync(
            "worker", KnownResourceStates.Running, cancellation.Token);
        var marker = Guid.NewGuid().ToString("n");

        // Act
        await using (var producer = await LogsHub.CreateProducerAsync(app, cancellation.Token))
        {
            using var batch = await producer.CreateBatchAsync(cancellation.Token);
            Assert.True(batch.TryAdd(LogsHub.LogEvent(
                level: "Error",
                message: "Checkout failed while notifying nobody.",
                exception: $"System.TimeoutException: notification end-to-end {marker}")));
            await producer.SendAsync(batch, cancellation.Token);
        }

        var report = await PollForReportAsync(marker, cancellation.Token);
        var deliveries = await PollForDeliveryLogsAsync(app, cancellation.Token);

        // Assert
        Assert.Contains(marker, report, StringComparison.Ordinal);
        foreach (var channel in Channels)
        {
            var line = Assert.Single(deliveries, logged => logged.Contains($"to channel {channel}:", StringComparison.Ordinal));
            Assert.Contains("unavailable", line, StringComparison.Ordinal);
        }
    }

    /// <summary>Polls the redirected drafts directory until this event's report file appears.</summary>
    private async Task<string> PollForReportAsync(string marker, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);

        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (var path in Directory.EnumerateFiles(fixture.ReportsDirectory, "*.md"))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(path, cancellationToken);
                    if (content.Contains(marker, StringComparison.Ordinal))
                    {
                        return content;
                    }
                }
                catch (IOException)
                {
                    // The worker is still writing this file; the next poll reads it whole.
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        Assert.Fail($"No report containing '{marker}' appeared in {fixture.ReportsDirectory} within 3 minutes.");
        return "";
    }

    /// <summary>Watches the worker's logs until one delivery line per channel has been seen.</summary>
    private static async Task<IReadOnlyList<string>> PollForDeliveryLogsAsync(
        DistributedApplication app,
        CancellationToken cancellationToken)
    {
        var logs = app.Services.GetRequiredService<ResourceLoggerService>();
        var found = new List<string>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            await foreach (var batch in logs.WatchAsync("worker").WithCancellation(timeout.Token))
            {
                found.AddRange(batch
                    .Select(line => line.Content)
                    .Where(content => content.Contains("Notification for incident", StringComparison.Ordinal)));
                if (Channels.All(channel => found.Any(line =>
                        line.Contains($"to channel {channel}:", StringComparison.Ordinal))))
                {
                    return found;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"The worker logged {found.Count} delivery lines within 3 minutes; four were expected.");
        }

        return found;
    }
}
