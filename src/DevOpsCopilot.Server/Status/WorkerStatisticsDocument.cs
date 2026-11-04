namespace DevOpsCopilot.Server.Status;

public sealed record WorkerStatisticsDocument(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);
