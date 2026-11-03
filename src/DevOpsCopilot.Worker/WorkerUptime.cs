namespace DevOpsCopilot.Worker;

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
