using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsCopilot.Infrastructure.Events;

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
