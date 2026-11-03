using DevOpsCopilot.Application.Interfaces;

namespace DevOpsCopilot.Application.Events;

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
