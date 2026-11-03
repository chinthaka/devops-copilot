namespace DevOpsCopilot.Worker.Status;

public sealed record WorkerStatusResponse(
    bool TelemetryEnabled,
    DateTimeOffset StartedAt,
    double UptimeSeconds,
    WorkerStatisticsSnapshot? Statistics,
    IReadOnlyList<DependencyHealthResponse> Dependencies);
