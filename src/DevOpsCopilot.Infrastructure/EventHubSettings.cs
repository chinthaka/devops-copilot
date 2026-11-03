namespace DevOpsCopilot.Infrastructure;

/// <summary>Where the stream and its checkpoints live. Built by the host from configuration.</summary>
public sealed record EventHubSettings(string EventHubConnectionString, string CheckpointConnectionString);
