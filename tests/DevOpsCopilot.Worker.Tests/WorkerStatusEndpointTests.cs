using System.Net.Http.Json;
using System.Text.Json;
using DevOpsCopilot.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsCopilot.Worker.Tests;

/// <summary>
/// The status endpoint on an in-process test server, with stub dependency health checks instead
/// of the Azure connections. Mirrors how Program.cs wires the endpoint.
/// </summary>
public class WorkerStatusEndpointTests
{
    [Fact]
    public async Task The_status_endpoint_returns_statistics_and_dependency_health()
    {
        // Arrange
        await using var app = await StartAsync(telemetryEnabled: true, storageStatus: HealthStatus.Unhealthy);
        var statistics = app.Services.GetRequiredService<WorkerStatistics>();
        statistics.OnEventRead();
        statistics.OnEventSkipped();

        // Act
        var status = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/status");

        // Assert
        Assert.True(status.GetProperty("telemetryEnabled").GetBoolean());
        Assert.Equal(1, status.GetProperty("statistics").GetProperty("eventsRead").GetInt64());
        Assert.Equal(1, status.GetProperty("statistics").GetProperty("eventsSkipped").GetInt64());
        Assert.Equal(0, status.GetProperty("statistics").GetProperty("batchesCheckpointed").GetInt64());
        Assert.Equal(0, status.GetProperty("statistics").GetProperty("errors").GetInt64());
        Assert.True(status.GetProperty("uptimeSeconds").GetDouble() >= 0);
        var dependencies = status.GetProperty("dependencies").EnumerateArray()
            .ToDictionary(
                dependency => dependency.GetProperty("name").GetString(),
                dependency => dependency.GetProperty("status").GetString());
        Assert.Equal(new Dictionary<string?, string?> { ["eventhubs"] = "healthy", ["storage"] = "unhealthy" }, dependencies);
    }

    [Fact]
    public async Task With_telemetry_disabled_the_endpoint_reports_disabled_without_statistics()
    {
        // Arrange
        await using var app = await StartAsync(telemetryEnabled: false, storageStatus: HealthStatus.Healthy);

        // Act
        var status = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/status");

        // Assert
        Assert.False(status.GetProperty("telemetryEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("statistics").ValueKind);
        Assert.Equal(2, status.GetProperty("dependencies").GetArrayLength());
    }

    /// <summary>Builds the endpoint host the way Program.cs does, minus the Azure wiring.</summary>
    private static async Task<WebApplication> StartAsync(bool telemetryEnabled, HealthStatus storageStatus)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkerUptime>();
        if (telemetryEnabled)
        {
            builder.Services.AddSingleton<WorkerStatistics>();
        }

        builder.Services.AddHealthChecks()
            .AddCheck(
                "eventhubs",
                () => HealthCheckResult.Healthy(),
                tags: [WorkerStatusEndpoint.DependencyTag])
            .AddCheck(
                "storage",
                () => new HealthCheckResult(storageStatus),
                tags: [WorkerStatusEndpoint.DependencyTag]);

        var app = builder.Build();
        app.MapWorkerStatus();
        await app.StartAsync();
        return app;
    }
}
