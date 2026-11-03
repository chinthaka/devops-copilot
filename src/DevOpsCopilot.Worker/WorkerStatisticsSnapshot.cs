namespace DevOpsCopilot.Worker;

public sealed record WorkerStatisticsSnapshot(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);
