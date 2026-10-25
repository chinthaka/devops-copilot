using DevOpsCopilot.Application;

namespace DevOpsCopilot.Worker;

/// <summary>
/// The host's in-memory processing statistics. Thread-safe; counters reset when the process
/// restarts. Registered as the <see cref="IProcessingObserver"/> only when host telemetry is
/// enabled.
/// </summary>
public sealed class WorkerStatistics : IProcessingObserver
{
    private readonly TimeProvider _time;

    private long _eventsRead;
    private long _eventsSkipped;
    private long _batchesCheckpointed;
    private long _errors;
    private long _lastProcessedTicks;

    public WorkerStatistics(TimeProvider time)
    {
        _time = time;
    }

    public void OnEventRead()
    {
        Interlocked.Increment(ref _eventsRead);
        Interlocked.Exchange(ref _lastProcessedTicks, _time.GetUtcNow().UtcTicks);
    }

    public void OnEventSkipped() => Interlocked.Increment(ref _eventsSkipped);

    public void OnCheckpointed() => Interlocked.Increment(ref _batchesCheckpointed);

    public void OnEventFailed() => Interlocked.Increment(ref _errors);

    public WorkerStatisticsSnapshot Snapshot()
    {
        var lastProcessedTicks = Interlocked.Read(ref _lastProcessedTicks);
        return new WorkerStatisticsSnapshot(
            Interlocked.Read(ref _eventsRead),
            Interlocked.Read(ref _eventsSkipped),
            Interlocked.Read(ref _batchesCheckpointed),
            Interlocked.Read(ref _errors),
            lastProcessedTicks == 0 ? null : new DateTimeOffset(lastProcessedTicks, TimeSpan.Zero));
    }
}

public sealed record WorkerStatisticsSnapshot(
    long EventsRead,
    long EventsSkipped,
    long BatchesCheckpointed,
    long Errors,
    DateTimeOffset? LastProcessedAt);

/// <summary>When the worker started, kept separately so uptime is known even with telemetry off.</summary>
public sealed class WorkerUptime
{
    private readonly TimeProvider _time;

    public WorkerUptime(TimeProvider time)
    {
        _time = time;
        StartedAt = time.GetUtcNow();
    }

    public DateTimeOffset StartedAt { get; }

    public TimeSpan Elapsed => _time.GetUtcNow() - StartedAt;
}
