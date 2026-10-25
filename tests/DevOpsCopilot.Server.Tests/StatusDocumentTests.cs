using System.Net;
using System.Text;
using DevOpsCopilot.Server;

namespace DevOpsCopilot.Server.Tests;

/// <summary>
/// The mapping from the worker's status answer to the dashboard's status document, against a
/// stubbed worker HTTP handler. No network, no service discovery.
/// </summary>
public class StatusDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StatusDocument_MapsWorkerStatusAndServiceHealth()
    {
        // Arrange
        var client = Client(WorkerResponds("""
            {
              "telemetryEnabled": true,
              "startedAt": "2026-10-25T11:54:38.5Z",
              "uptimeSeconds": 321.5,
              "statistics": {
                "eventsRead": 5,
                "eventsSkipped": 2,
                "batchesCheckpointed": 3,
                "errors": 1,
                "lastProcessedAt": "2026-10-25T11:59:00Z"
              },
              "dependencies": [
                { "name": "eventhubs", "status": "healthy" },
                { "name": "storage", "status": "healthy" }
              ]
            }
            """));

        // Act
        var document = await client.GetStatusDocumentAsync(CancellationToken.None);

        // Assert
        Assert.Equal("running", document.Worker.Status);
        Assert.True(document.Worker.TelemetryEnabled);
        Assert.Equal(321.5, document.Worker.UptimeSeconds);
        Assert.NotNull(document.Worker.Statistics);
        Assert.Equal(5, document.Worker.Statistics.EventsRead);
        Assert.Equal(2, document.Worker.Statistics.EventsSkipped);
        Assert.Equal(3, document.Worker.Statistics.BatchesCheckpointed);
        Assert.Equal(1, document.Worker.Statistics.Errors);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 11, 59, 0, TimeSpan.Zero), document.Worker.Statistics.LastProcessedAt);
        Assert.Equal(
            [new("worker", "healthy"), new("eventhubs", "healthy"), new("storage", "healthy")],
            document.Services.ToArray());
        Assert.Equal(Now, document.GeneratedAt);
    }

    [Fact]
    public async Task StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros()
    {
        // Arrange
        var client = Client(new ThrowingHandler(new HttpRequestException("connection refused")));

        // Act
        var document = await client.GetStatusDocumentAsync(CancellationToken.None);

        // Assert
        Assert.Equal("not-running", document.Worker.Status);
        Assert.Equal(0, document.Worker.UptimeSeconds);
        Assert.NotNull(document.Worker.Statistics);
        Assert.Equal(0, document.Worker.Statistics.EventsRead);
        Assert.Equal(0, document.Worker.Statistics.EventsSkipped);
        Assert.Equal(0, document.Worker.Statistics.BatchesCheckpointed);
        Assert.Equal(0, document.Worker.Statistics.Errors);
        Assert.Null(document.Worker.Statistics.LastProcessedAt);
        Assert.Equal(
            [new("worker", "unhealthy"), new("eventhubs", "unknown"), new("storage", "unknown")],
            document.Services.ToArray());
        Assert.Equal(Now, document.GeneratedAt);
    }

    [Fact]
    public async Task StatusDocument_WhenWorkerDependencyUnhealthy_PropagatesThatHealth()
    {
        // Arrange
        var client = Client(WorkerResponds("""
            {
              "telemetryEnabled": true,
              "startedAt": "2026-10-25T11:54:38.5Z",
              "uptimeSeconds": 10,
              "statistics": {
                "eventsRead": 0,
                "eventsSkipped": 0,
                "batchesCheckpointed": 0,
                "errors": 0,
                "lastProcessedAt": null
              },
              "dependencies": [
                { "name": "eventhubs", "status": "healthy" },
                { "name": "storage", "status": "unhealthy" }
              ]
            }
            """));

        // Act
        var document = await client.GetStatusDocumentAsync(CancellationToken.None);

        // Assert
        Assert.Equal(
            [new("worker", "healthy"), new("eventhubs", "healthy"), new("storage", "unhealthy")],
            document.Services.ToArray());
    }

    private static WorkerStatusClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://worker") }, new FixedClock(Now));

    private static StubHandler WorkerResponds(string json) => new(json);

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
