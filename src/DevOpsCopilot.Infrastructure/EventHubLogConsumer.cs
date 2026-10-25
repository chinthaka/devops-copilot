using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using DevOpsCopilot.Application;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Reads hub <c>logs</c> with consumer group <c>devops-copilot</c>, checkpointing into Blob
/// Storage. Each body is parsed here, so the layers above see domain events only.
/// </summary>
public sealed class EventHubLogConsumer : IEventSource
{
    private const string EventHubName = "logs";
    private const string ConsumerGroupName = "devops-copilot";
    private const string CheckpointContainerName = "devops-copilot-checkpoints";

    private readonly EventProcessorClient _processor;
    private readonly BlobContainerClient _checkpointContainer;
    private readonly ILogger<EventHubLogConsumer> _logger;

    public EventHubLogConsumer(EventHubSettings settings, ILogger<EventHubLogConsumer> logger)
    {
        var eventHubConnectionString = Required(settings.EventHubConnectionString, "eventhubs");
        var storageConnectionString = StripAspireBlobName(
            Required(settings.CheckpointConnectionString, "checkpoints"));

        _checkpointContainer = new BlobServiceClient(storageConnectionString)
            .GetBlobContainerClient(CheckpointContainerName);
        var eventHubProperties = EventHubsConnectionStringProperties.Parse(eventHubConnectionString);
        _processor = string.IsNullOrWhiteSpace(eventHubProperties.EventHubName)
            ? new EventProcessorClient(_checkpointContainer, ConsumerGroupName, eventHubConnectionString, EventHubName)
            : new EventProcessorClient(_checkpointContainer, ConsumerGroupName, eventHubConnectionString);
        _logger = logger;
    }

    public async Task RunAsync(
        Func<ReceivedEvent, CancellationToken, Task> handleEvent,
        CancellationToken cancellationToken)
    {
        await _checkpointContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        _processor.ProcessEventAsync += args => handleEvent(
            new ReceivedEvent(LogEventParser.Parse(args.Data.EventBody), args.UpdateCheckpointAsync),
            args.CancellationToken);
        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(
                args.Exception,
                "Event Hubs processor failed for {PartitionId} during {Operation}.",
                args.PartitionId,
                args.Operation);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken);
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _processor.StopProcessingAsync(CancellationToken.None);
        }
    }

    private static string Required(string connectionString, string name)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"The '{name}' connection string is required by the Event Hubs consumer.");
        }

        return connectionString;
    }

    private static string StripAspireBlobName(string connectionString) =>
        string.Join(
            ';',
            connectionString.Split(';').Where(
                part => !part.StartsWith("BlobName=", StringComparison.OrdinalIgnoreCase)));
}
