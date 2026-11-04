using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System.Text;
using System.Text.Json;

namespace DevOpsCopilot.AppHost.Tests.Common;

/// <summary>Produces test events to hub <c>logs</c> through the Event Hubs emulator.</summary>
internal static class LogsHub
{
    public const string EventHubName = "logs";

    public static async Task<EventHubProducerClient> CreateProducerAsync(
        DistributedApplication app, CancellationToken cancellationToken)
    {
        var connectionString = await app.GetConnectionStringAsync("eventhubs", cancellationToken)
            ?? throw new InvalidOperationException("The AppHost did not supply an 'eventhubs' connection string.");

        // The Aspire connection string names the hub only sometimes; supply it when it does not.
        var properties = EventHubsConnectionStringProperties.Parse(connectionString);
        return string.IsNullOrWhiteSpace(properties.EventHubName)
            ? new EventHubProducerClient(connectionString, EventHubName)
            : new EventHubProducerClient(connectionString);
    }

    /// <summary>One event in the LogForwarder JSON shape the worker parses in local runs.</summary>
    public static EventData LogEvent(string level, string message, string? exception)
    {
        var body = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            category = "DevOpsCopilot.EndToEnd",
            message,
            exception,
            traceId = Guid.NewGuid().ToString("n"),
            spanId = Guid.NewGuid().ToString("n")[..16],
        });

        return new EventData(Encoding.UTF8.GetBytes(body));
    }
}
