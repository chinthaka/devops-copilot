namespace DevOpsCopilot.Worker;

public sealed record WorkerStatusResponse(
    bool TelemetryEnabled,
    DateTimeOffset StartedAt,
    double UptimeSeconds,
    WorkerStatisticsSnapshot? Statistics,
    IReadOnlyList<DependencyHealthResponse> Dependencies);
