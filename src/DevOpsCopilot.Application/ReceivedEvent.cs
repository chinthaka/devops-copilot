using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// One received event and the callback that checkpoints the stream up to and including it. The
/// event arrives parsed: the payload shape of the stream never reaches this layer.
/// </summary>
public sealed record ReceivedEvent(LogEvent Event, Func<CancellationToken, Task> CheckpointAsync);
