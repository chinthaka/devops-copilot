using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Application.Interfaces;

/// <summary>
/// Delivers one notification to one channel, or reports itself unconfigured. Implementations
/// return a failed delivery rather than throw, and never retry beyond their own budget.
/// </summary>
public interface INotifier
{
    string Channel { get; }

    bool IsConfigured { get; }

    Task<NotificationDelivery> SendAsync(IncidentNotification notification, CancellationToken cancellationToken);
}
