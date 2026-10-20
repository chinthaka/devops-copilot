namespace DevOpsCopilot.Daemon;

public sealed record EventHubLogMessage(
    ReadOnlyMemory<byte> Body,
    Func<CancellationToken, Task> CheckpointAsync);
