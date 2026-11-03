namespace DevOpsCopilot.Server.Status;

internal sealed record WorkerStatisticsResponse(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);
