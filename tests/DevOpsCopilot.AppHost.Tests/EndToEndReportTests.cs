using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using DevOpsCopilot.AppHost.Tests.Common;

namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// The straight-through path against the real AppHost: an error event produced to hub
/// <c>logs</c> ends as a report file. With no Kusto, search or Azure OpenAI configured, the
/// report comes from the deterministic diagnosis and says what was unavailable. Needs Docker.
/// </summary>
[Trait("Category", "RequiresDocker")]
[Collection(AppHostCollection.Name)]
public class EndToEndReportTests(AppHostFixture fixture)
{
    [DockerRequiredFact]
    public async Task EndToEnd_AnErrorEvent_WritesASevenSectionReport()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var app = await fixture.StartAsync(cancellation.Token);
        await app.ResourceNotifications.WaitForResourceAsync(
            "worker", KnownResourceStates.Running, cancellation.Token);
        var marker = Guid.NewGuid().ToString("n");
        string[] headings =
        [
            "# Incident report:",
            "## Impact",
            "## Likely cause",
            "## Evidence",
            "## Similar incidents",
            "## Suggested steps",
            "## Open questions",
        ];

        // Act
        await using (var producer = await LogsHub.CreateProducerAsync(app, cancellation.Token))
        {
            using var batch = await producer.CreateBatchAsync(cancellation.Token);
            Assert.True(batch.TryAdd(LogsHub.LogEvent(
                level: "Error",
                message: "Checkout failed while charging the card.",
                exception: $"System.InvalidOperationException: end-to-end {marker}")));
            await producer.SendAsync(batch, cancellation.Token);
        }

        var report = await PollForReportAsync(marker, cancellation.Token);

        // Assert
        var position = -1;
        foreach (var heading in headings)
        {
            var next = report.IndexOf(heading, position + 1, StringComparison.Ordinal);
            Assert.True(next > position, heading);
            position = next;
        }

        Assert.Contains("(confidence: low)", report, StringComparison.Ordinal);
        Assert.Contains("KQL was unavailable.", report, StringComparison.Ordinal);
        Assert.Contains("Azure AI Search was unavailable.", report, StringComparison.Ordinal);
        Assert.Contains("The diagnosis model was unavailable.", report, StringComparison.Ordinal);
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
}
