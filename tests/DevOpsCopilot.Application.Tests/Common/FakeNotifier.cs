using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Application.Tests.Common;

/// <summary>A notifier that records its calls and succeeds, throws, hangs, fails or is unconfigured on request.</summary>
internal sealed class FakeNotifier(string channel, List<string>? calls = null) : INotifier
{
    public string Channel { get; } = channel;

    public bool IsConfigured { get; init; } = true;

    public Exception? Throws { get; init; }

    public bool Hangs { get; init; }

    public string? FailsWith { get; init; }

    public List<IncidentNotification> Sent { get; } = [];

    public async Task<NotificationDelivery> SendAsync(IncidentNotification notification, CancellationToken cancellationToken)
    {
        Sent.Add(notification);
        calls?.Add(Channel);
        if (Throws is not null)
        {
            throw Throws;
        }

        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return FailsWith is null
            ? NotificationDelivery.Sent(Channel, $"{Channel}-message")
            : NotificationDelivery.Failed(Channel, FailsWith);
    }
}
