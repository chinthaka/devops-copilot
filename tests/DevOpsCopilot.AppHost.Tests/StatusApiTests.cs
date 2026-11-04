using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using DevOpsCopilot.AppHost.Tests.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// The status API against the real AppHost: server, worker and the emulators, composed exactly
/// as <c>aspire run</c> composes them. Needs Docker.
/// </summary>
[Trait("Category", "RequiresDocker")]
[Collection(AppHostCollection.Name)]
public class StatusApiTests(AppHostFixture fixture)
{
    private static readonly string[] HealthStates = ["healthy", "degraded", "unhealthy", "unknown"];

    [DockerRequiredFact]
    public async Task GetApiStatus_AgainstTheRealAppHost_ReturnsOneStatusDocument()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var app = await fixture.StartAsync(cancellation.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("server", cancellation.Token);

        // Act
        using var server = app.CreateHttpClient("server");
        var document = await server.GetFromJsonAsync<JsonElement>("/api/status", cancellation.Token);

        // Assert
        var worker = document.GetProperty("worker");
        Assert.Contains(worker.GetProperty("status").GetString(), new[] { "running", "not-running" });
        Assert.NotEqual(JsonValueKind.Undefined, worker.GetProperty("telemetryEnabled").ValueKind);
        var services = document.GetProperty("services").EnumerateArray().ToArray();
        Assert.Equal(
            ["worker", "eventhubs", "storage"],
            services.Select(service => service.GetProperty("name").GetString() ?? "").ToArray());
        Assert.All(services, service =>
            Assert.Contains(service.GetProperty("status").GetString(), HealthStates));
        Assert.True(document.GetProperty("generatedAt").TryGetDateTimeOffset(out _));
    }

    [DockerRequiredFact]
    public async Task GetApiStatus_AfterEventsFlowThroughTheEmulator_ReportsLiveCounts()
    {
        // Arrange: one event that starts an incident and one Warning that does not.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var app = await fixture.StartAsync(cancellation.Token);
        await app.ResourceNotifications.WaitForResourceAsync(
            "worker", KnownResourceStates.Running, cancellation.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("server", cancellation.Token);

        // Act
        await ProduceAsync(app, cancellation.Token);
        using var server = app.CreateHttpClient("server");
        var statistics = await PollUntilEventsReadAsync(server, atLeast: 2, cancellation.Token);

        // Assert
        Assert.True(statistics.GetProperty("eventsRead").GetInt64() >= 2);
        Assert.True(statistics.GetProperty("eventsSkipped").GetInt64() >= 1);
        Assert.True(statistics.GetProperty("batchesCheckpointed").GetInt64() >= 1);
        Assert.Equal(JsonValueKind.String, statistics.GetProperty("lastProcessedAt").ValueKind);
    }

    /// <summary>Sends the two events to hub <c>logs</c> through the Event Hubs emulator.</summary>
    private static async Task ProduceAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        await using var producer = await LogsHub.CreateProducerAsync(app, cancellationToken);
        using var batch = await producer.CreateBatchAsync(cancellationToken);
        Assert.True(batch.TryAdd(LogsHub.LogEvent(
            level: "Error",
            message: "Checkout failed while charging the card.",
            exception: "System.InvalidOperationException: The payment gateway refused the call.")));
        Assert.True(batch.TryAdd(LogsHub.LogEvent(
            level: "Warning",
            message: "Retrying the payment gateway call.",
            exception: null)));

        await producer.SendAsync(batch, cancellationToken);
    }

    /// <summary>Polls the status API until the worker reports at least <paramref name="atLeast"/> events read.</summary>
    private static async Task<JsonElement> PollUntilEventsReadAsync(
        HttpClient server, int atLeast, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        JsonElement statistics = default;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var document = await server.GetFromJsonAsync<JsonElement>("/api/status", cancellationToken);
            var candidate = document.GetProperty("worker").GetProperty("statistics");
            if (candidate.ValueKind == JsonValueKind.Object)
            {
                statistics = candidate;
                if (candidate.GetProperty("eventsRead").GetInt64() >= atLeast)
                {
                    return statistics;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        Assert.Fail(
            $"The status document never reported {atLeast} events read. Last seen: {statistics}");
        return statistics;
    }}
