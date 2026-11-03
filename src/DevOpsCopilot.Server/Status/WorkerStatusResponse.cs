namespace DevOpsCopilot.Server.Status;

/// <summary>The worker's /status answer; mirrors WorkerStatusResponse in DevOpsCopilot.Worker.</summary>
internal sealed record WorkerStatusResponse(
    bool TelemetryEnabled,
    double UptimeSeconds,
    WorkerStatisticsResponse? Statistics,
    WorkerDependencyResponse[]? Dependencies);
