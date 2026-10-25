namespace DevOpsCopilot.Server;

/// <summary>
/// The dashboard's status document: one JSON answer combining the worker's own status with the
/// enumerated health of the services the dashboard shows. Shape defined in
/// docs/specs/2026-10-25-status-dashboard/requirement.md.
/// </summary>
public sealed record StatusDocument(
    WorkerStatus Worker,
    IReadOnlyList<ServiceHealth> Services,
    DateTimeOffset GeneratedAt);

public sealed record WorkerStatus(
    string Status,
    bool TelemetryEnabled,
    double UptimeSeconds,
    WorkerStatisticsDocument? Statistics);

public sealed record WorkerStatisticsDocument(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);

public sealed record ServiceHealth(string Name, string Status);
