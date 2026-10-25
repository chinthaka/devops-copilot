using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Health of the Event Hubs connection the consumer reads from. Unconfigured or unreachable is
/// unhealthy; the check never throws.
/// </summary>
public sealed class EventHubsHealthCheck : IHealthCheck
{
    private const string EventHubName = "logs";

    private readonly EventHubSettings _settings;

    public EventHubsHealthCheck(EventHubSettings settings)
    {
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.EventHubConnectionString))
        {
            return HealthCheckResult.Unhealthy("The Event Hubs connection is not configured.");
        }

        try
        {
            var properties = EventHubsConnectionStringProperties.Parse(_settings.EventHubConnectionString);
            await using var producer = string.IsNullOrWhiteSpace(properties.EventHubName)
                ? new EventHubProducerClient(_settings.EventHubConnectionString, EventHubName)
                : new EventHubProducerClient(_settings.EventHubConnectionString);
            await producer.GetEventHubPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The Event Hubs connection failed.", exception);
        }
    }
}

/// <summary>Health of the Blob Storage account that holds the consumer's checkpoints.</summary>
public sealed class CheckpointStorageHealthCheck : IHealthCheck
{
    private readonly EventHubSettings _settings;

    public CheckpointStorageHealthCheck(EventHubSettings settings)
    {
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.CheckpointConnectionString))
        {
            return HealthCheckResult.Unhealthy("The checkpoint storage connection is not configured.");
        }

        try
        {
            var service = new BlobServiceClient(_settings.CheckpointConnectionString);
            await service.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The checkpoint storage connection failed.", exception);
        }
    }
}
