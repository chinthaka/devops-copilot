using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;

namespace DevOpsCopilot.Daemon;

public sealed class EventHubLogConsumer : IEventHubLogConsumer
{
    private const string EventHubName = "logs";
    private const string ConsumerGroupName = "devops-copilot";
    private const string CheckpointContainerName = "devops-copilot-checkpoints";

    private readonly EventProcessorClient _processor;
    private readonly BlobContainerClient _checkpointContainer;
    private readonly ILogger<EventHubLogConsumer> _logger;

    public EventHubLogConsumer(IConfiguration configuration, ILogger<EventHubLogConsumer> logger)
    {
        var eventHubConnectionString = RequiredConnectionString(configuration, "eventhubs");
        var storageConnectionString = StripAspireBlobName(
            RequiredConnectionString(configuration, "checkpoints"));

        _checkpointContainer = new BlobServiceClient(storageConnectionString)
            .GetBlobContainerClient(CheckpointContainerName);
        var eventHubProperties = EventHubsConnectionStringProperties.Parse(eventHubConnectionString);
        _processor = string.IsNullOrWhiteSpace(eventHubProperties.EventHubName)
            ? new EventProcessorClient(_checkpointContainer, ConsumerGroupName, eventHubConnectionString, EventHubName)
            : new EventProcessorClient(_checkpointContainer, ConsumerGroupName, eventHubConnectionString);
        _logger = logger;
    }

    public async Task RunAsync(
        Func<EventHubLogMessage, CancellationToken, Task> handleEvent,
        CancellationToken cancellationToken)
    {
        await _checkpointContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        _processor.ProcessEventAsync += args => handleEvent(
            new EventHubLogMessage(args.Data.EventBody, args.UpdateCheckpointAsync),
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

    private static string RequiredConnectionString(IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);
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
