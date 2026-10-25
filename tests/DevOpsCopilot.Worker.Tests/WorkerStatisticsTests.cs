using DevOpsCopilot.Worker;

namespace DevOpsCopilot.Worker.Tests;

public class WorkerStatisticsTests
{
    [Fact]
    public void An_event_read_increments_events_read_and_sets_last_processed()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
        var statistics = new WorkerStatistics(clock);

        // Act
        statistics.OnEventRead();

        // Assert
        var snapshot = statistics.Snapshot();
        Assert.Equal(1, snapshot.EventsRead);
        Assert.Equal(0, snapshot.EventsSkipped);
        Assert.Equal(0, snapshot.BatchesCheckpointed);
        Assert.Equal(0, snapshot.Errors);
        Assert.Equal(clock.GetUtcNow(), snapshot.LastProcessedAt);
    }

    [Fact]
    public void A_skipped_event_increments_only_the_skipped_count()
    {
        // Arrange
        var statistics = new WorkerStatistics(TimeProvider.System);

        // Act
        statistics.OnEventSkipped();

        // Assert
        Assert.Equal(1, statistics.Snapshot().EventsSkipped);
        Assert.Equal(0, statistics.Snapshot().EventsRead);
    }

    [Fact]
    public void A_checkpoint_increments_only_the_checkpoint_count()
    {
        // Arrange
        var statistics = new WorkerStatistics(TimeProvider.System);

        // Act
        statistics.OnCheckpointed();

        // Assert
        Assert.Equal(1, statistics.Snapshot().BatchesCheckpointed);
    }

    [Fact]
    public void A_failed_event_increments_only_the_error_count()
    {
        // Arrange
        var statistics = new WorkerStatistics(TimeProvider.System);

        // Act
        statistics.OnEventFailed();

        // Assert
        Assert.Equal(1, statistics.Snapshot().Errors);
    }

    [Fact]
    public void Before_any_event_the_last_processed_time_is_null()
    {
        // Arrange & Act
        var statistics = new WorkerStatistics(TimeProvider.System);

        // Assert
        Assert.Null(statistics.Snapshot().LastProcessedAt);
    }

    [Fact]
    public void Uptime_grows_from_the_start_time()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
        var uptime = new WorkerUptime(clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), uptime.Elapsed);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero), uptime.StartedAt);
    }

    [Fact]
    public async Task Counters_are_exact_under_parallel_increments()
    {
        // Arrange
        const int increments = 10_000;
        var statistics = new WorkerStatistics(TimeProvider.System);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < increments; i++)
            {
                statistics.OnEventRead();
                statistics.OnEventSkipped();
                statistics.OnCheckpointed();
                statistics.OnEventFailed();
            }
        })));

        // Assert
        var snapshot = statistics.Snapshot();
        Assert.Equal(4 * increments, snapshot.EventsRead);
        Assert.Equal(4 * increments, snapshot.EventsSkipped);
        Assert.Equal(4 * increments, snapshot.BatchesCheckpointed);
        Assert.Equal(4 * increments, snapshot.Errors);
    }

    /// <summary>A clock the tests move by hand.</summary>
    internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
