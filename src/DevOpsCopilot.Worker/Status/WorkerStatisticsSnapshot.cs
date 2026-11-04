namespace DevOpsCopilot.Worker.Status;

public sealed record WorkerStatisticsSnapshot(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);
