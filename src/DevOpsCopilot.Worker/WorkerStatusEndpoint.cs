using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsCopilot.Worker;

/// <summary>
/// The worker's own status: telemetry state, uptime, statistics and the health of its
/// dependencies. The server reads this to build the dashboard's status document.
/// </summary>
public static class WorkerStatusEndpoint
{
    /// <summary>Tag for the health checks the status endpoint reports as dependencies.</summary>
    public const string DependencyTag = "dependency";

    public static IEndpointRouteBuilder MapWorkerStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status", async (
            WorkerUptime uptime,
            HealthCheckService health,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var statistics = services.GetService<WorkerStatistics>();
            var report = await health.CheckHealthAsync(
                registration => registration.Tags.Contains(DependencyTag),
                cancellationToken);
            var dependencies = report.Entries
                .Select(entry => new DependencyHealthResponse(entry.Key, Describe(entry.Value.Status)))
                .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(new WorkerStatusResponse(
                TelemetryEnabled: statistics is not null,
                StartedAt: uptime.StartedAt,
                UptimeSeconds: uptime.Elapsed.TotalSeconds,
                Statistics: statistics?.Snapshot(),
                Dependencies: dependencies));
        });

        return app;
    }

    private static string Describe(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy",
    };
}

public sealed record WorkerStatusResponse(
    bool TelemetryEnabled,
    DateTimeOffset StartedAt,
    double UptimeSeconds,
    WorkerStatisticsSnapshot? Statistics,
    IReadOnlyList<DependencyHealthResponse> Dependencies);

public sealed record DependencyHealthResponse(string Name, string Status);
