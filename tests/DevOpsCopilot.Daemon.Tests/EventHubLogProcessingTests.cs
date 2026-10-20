using System.Text;
using System.Text.Json;
using DevOpsCopilot.Daemon;
using DevOpsCopilot.Daemon.Incident;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Daemon.Tests;

public class EventHubLogProcessingTests
{
    [Fact]
    public void Parse_reads_the_log_fields_and_w3c_trace_context()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Timestamp = DateTimeOffset.Parse("2026-10-20T10:00:00Z"),
            Level = "Error",
            Category = "Checkout.Api",
            Message = "checkout failed",
            Exception = "TimeoutException: timed out",
            TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
            SpanId = "00f067aa0ba902b7",
            Attribute = new Dictionary<string, string> { ["operation"] = "checkout" },
        }));

        // Act
        var logEvent = LogEventParser.Parse(body);

        // Assert
        Assert.Equal("Error", logEvent.Level);
        Assert.Equal("Checkout.Api", logEvent.Category);
        Assert.Equal("checkout failed", logEvent.Message);
        Assert.Equal("TimeoutException: timed out", logEvent.Exception);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", logEvent.TraceId);
        Assert.Equal("00f067aa0ba902b7", logEvent.SpanId);
        Assert.Equal("checkout", logEvent.Attribute!["operation"]);
    }

    [Fact]
    public async Task Restart_resumes_after_the_last_handled_checkpoint()
    {
        // Arrange
        using var drafts = new DraftDirectory();
        var firstEvent = SerializeEvent(
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero),
            "4bf92f3577b34da6a3ce929d0e0e4736");
        var secondEvent = SerializeEvent(
            new DateTimeOffset(2026, 10, 20, 10, 1, 0, TimeSpan.Zero),
            "1bf92f3577b34da6a3ce929d0e0e4736");
        var consumer = new ResumableFakeConsumer([firstEvent, secondEvent], drafts.Path);
        var processor = new LogEventProcessor(
            consumer,
            CreateWorkflow(drafts.Path),
            NullLogger<LogEventProcessor>.Instance);

        // Act
        await processor.RunAsync(CancellationToken.None);
        await processor.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal([0, 1], consumer.DeliveredIndexes);
        Assert.Equal(1, consumer.CheckpointedIndex);
        Assert.All(consumer.CheckpointWasAfterDraft, Assert.True);
        Assert.Equal(2, Directory.GetFiles(drafts.Path, "*.md").Length);
    }

    private static byte[] SerializeEvent(DateTimeOffset timestamp, string traceId)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Timestamp = timestamp,
            Level = "Error",
            Category = "Checkout.Api",
            Message = "checkout failed",
            Exception = "TimeoutException: timed out",
            TraceId = traceId,
            SpanId = "00f067aa0ba902b7",
            Attribute = new Dictionary<string, string>(),
        }));
    }

    private static IncidentWorkflow CreateWorkflow(string draftsDirectory)
    {
        var options = new IncidentOptions { DraftsDirectory = draftsDirectory };
        var kusto = new RecordingKustoRunner();
        var search = new RecordingSearch();
        return new IncidentWorkflow(
            new DiagnosticAgent(new KustoQueryTool(kusto, options)),
            new RagGroundingAgent(new PostmortemSearchTool(search)),
            new RemediationAgent(options));
    }

    private sealed class ResumableFakeConsumer(IReadOnlyList<byte[]> events, string draftsDirectory) : IEventHubLogConsumer
    {
        public List<int> DeliveredIndexes { get; } = [];

        public List<bool> CheckpointWasAfterDraft { get; } = [];

        public int CheckpointedIndex { get; private set; } = -1;

        public async Task RunAsync(
            Func<EventHubLogMessage, CancellationToken, Task> handleEvent,
            CancellationToken cancellationToken)
        {
            var eventIndex = CheckpointedIndex + 1;
            if (eventIndex >= events.Count)
            {
                return;
            }

            DeliveredIndexes.Add(eventIndex);
            await handleEvent(
                new EventHubLogMessage(
                    events[eventIndex],
                    _ =>
                    {
                        CheckpointWasAfterDraft.Add(Directory.GetFiles(draftsDirectory, "*.md").Length > eventIndex);
                        CheckpointedIndex = eventIndex;
                        return Task.CompletedTask;
                    }),
                cancellationToken);
        }
    }

    private sealed class RecordingKustoRunner : IKustoQueryRunner
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(
            string kql,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
    }

    private sealed class RecordingSearch : IPostmortemSearch
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchHit>>([]);
    }

    private sealed class DraftDirectory : IDisposable
    {
        public DraftDirectory()
        {
            Path = Directory.CreateTempSubdirectory("devops-copilot-hub-events").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
