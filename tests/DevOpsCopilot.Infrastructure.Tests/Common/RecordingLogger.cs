using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Infrastructure.Tests.Common;

/// <summary>A logger that keeps every formatted line and its level in memory.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Line)> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Lines.Add((logLevel, formatter(state, exception)));
    }
}
