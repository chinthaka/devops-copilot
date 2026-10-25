using System.Net.Http.Json;
using System.Text.Json;

namespace DevOpsCopilot.Server;

/// <summary>
/// Reads the worker's /status endpoint and maps the answer to the status document. The base
/// address is http://worker, resolved by Aspire service discovery. When the worker cannot be
/// read the document reports not-running with zeroed statistics and unknown dependency health;
/// this is host-to-host plumbing, deliberately not an Application interface.
/// </summary>
public sealed class WorkerStatusClient(HttpClient http, TimeProvider time)
{
    private const string Running = "running";
    private const string NotRunning = "not-running";
    private const string Unknown = "unknown";

    /// <summary>The services the document enumerates besides the worker, in display order.</summary>
    private static readonly string[] DependencyNames = ["eventhubs", "storage"];

    private static readonly string[] KnownHealthStates = ["healthy", "degraded", "unhealthy", Unknown];

    public async Task<StatusDocument> GetStatusDocumentAsync(CancellationToken cancellationToken)
    {
        var worker = await ReadWorkerAsync(cancellationToken);
        return worker is null ? NotRunningDocument() : RunningDocument(worker);
    }

    private async Task<WorkerStatusResponse?> ReadWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await http.GetFromJsonAsync<WorkerStatusResponse>("/status", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException
                or JsonException or InvalidOperationException)
        {
            // Not started, unreachable, timed out, an unresolvable service name or a malformed
            // answer all mean the same to the dashboard: the worker is not running.
            return null;
        }
    }

    private StatusDocument RunningDocument(WorkerStatusResponse worker)
    {
        var dependencies = DependencyNames.Select(name => new ServiceHealth(
            name,
            worker.Dependencies?.FirstOrDefault(dependency =>
                string.Equals(dependency.Name, name, StringComparison.Ordinal)) is { Status: { } status }
                && KnownHealthStates.Contains(status)
                    ? status
                    : Unknown));

        return new StatusDocument(
            new WorkerStatus(
                Running,
                worker.TelemetryEnabled,
                worker.UptimeSeconds,
                worker.Statistics is null
                    ? null
                    : new WorkerStatisticsDocument(
                        worker.Statistics.EventsRead,
                        worker.Statistics.EventsSkipped,
                        worker.Statistics.BatchesCheckpointed,
                        worker.Statistics.Errors,
                        worker.Statistics.LastProcessedAt)),
            [new ServiceHealth("worker", "healthy"), .. dependencies],
            time.GetUtcNow());
    }

    private StatusDocument NotRunningDocument() =>
        new(
            // Zeroed statistics rather than null: telemetry is only reported off when the worker
            // itself says so, and the dashboard renders zero counters for a worker that is down.
            new WorkerStatus(NotRunning, TelemetryEnabled: true, UptimeSeconds: 0,
                new WorkerStatisticsDocument(0, 0, 0, 0, LastProcessedAt: null)),
            [
                new ServiceHealth("worker", "unhealthy"),
                .. DependencyNames.Select(name => new ServiceHealth(name, Unknown)),
            ],
            time.GetUtcNow());
}

/// <summary>The worker's /status answer; mirrors WorkerStatusResponse in DevOpsCopilot.Worker.</summary>
internal sealed record WorkerStatusResponse(
    bool TelemetryEnabled,
    double UptimeSeconds,
    WorkerStatisticsResponse? Statistics,
    WorkerDependencyResponse[]? Dependencies);

internal sealed record WorkerStatisticsResponse(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);

internal sealed record WorkerDependencyResponse(string? Name, string? Status);
