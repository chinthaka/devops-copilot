namespace DevOpsCopilot.Server.Status;

public sealed record WorkerStatus(
    string Status,
    bool TelemetryEnabled,
    double UptimeSeconds,
    WorkerStatisticsDocument? Statistics);
