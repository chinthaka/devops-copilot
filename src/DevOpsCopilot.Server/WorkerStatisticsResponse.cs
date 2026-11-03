namespace DevOpsCopilot.Server;

internal sealed record WorkerStatisticsResponse(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);
