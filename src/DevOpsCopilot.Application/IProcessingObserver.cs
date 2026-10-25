namespace DevOpsCopilot.Application;

/// <summary>
/// Receives processing outcomes as notifications so a host can keep statistics without the
/// pipeline knowing about telemetry. Implementations must be cheap and must not throw; they are
/// called on the event-handling path.
/// </summary>
public interface IProcessingObserver
{
    /// <summary>An event was received from the stream.</summary>
    void OnEventRead();

    /// <summary>The event did not start an incident and ran no stage.</summary>
    void OnEventSkipped();

    /// <summary>The event's position was checkpointed after handling.</summary>
    void OnCheckpointed();

    /// <summary>Handling the event threw; the failure still propagates to the caller.</summary>
    void OnEventFailed();
}

/// <summary>The default observer: ignores every notification.</summary>
public sealed class NullProcessingObserver : IProcessingObserver
{
    public static NullProcessingObserver Instance { get; } = new();

    private NullProcessingObserver()
    {
    }

    public void OnEventRead()
    {
    }

    public void OnEventSkipped()
    {
    }

    public void OnCheckpointed()
    {
    }

    public void OnEventFailed()
    {
    }
}
