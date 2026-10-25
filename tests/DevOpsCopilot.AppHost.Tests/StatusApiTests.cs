using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// The status API against the real AppHost: server, worker and the emulators, composed exactly
/// as `aspire run` composes them.
/// </summary>
public class StatusApiTests
{
    private static readonly string[] HealthStates = ["healthy", "degraded", "unhealthy", "unknown"];

    [DockerRequiredFact]
    public async Task StatusApi_AgainstAppHost_ReturnsOneDocument()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DevOpsCopilot_AppHost>(cancellation.Token);
        await using var app = await appHost.BuildAsync(cancellation.Token);
        await app.StartAsync(cancellation.Token);
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
}
