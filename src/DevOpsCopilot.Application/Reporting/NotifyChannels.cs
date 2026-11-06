using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace DevOpsCopilot.Application.Reporting;

/// <summary>
/// Stage 7, second half. Notifies every registered channel once, sequentially and in registration
/// order, each bounded by its own timeout. An unconfigured channel is unavailable and is not
/// called; a channel that throws, times out or answers with a failure is failed, and the next
/// channel still runs. Only a cancelled caller propagates, because that is shutdown.
/// </summary>
public sealed class NotifyChannels
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly IReadOnlyList<INotifier> _notifiers;
    private readonly TimeSpan _timeout;
    private readonly ILogger<NotifyChannels> _logger;

    public NotifyChannels(
        IEnumerable<INotifier> notifiers,
        ILogger<NotifyChannels>? logger = null,
        TimeSpan? timeout = null)
    {
        _notifiers = notifiers.ToArray();
        _logger = logger ?? NullLogger<NotifyChannels>.Instance;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<IReadOnlyList<NotificationDelivery>> NotifyAsync(
        IncidentNotification notification,
        CancellationToken cancellationToken = default)
    {
        var deliveries = new List<NotificationDelivery>(_notifiers.Count);
        foreach (var notifier in _notifiers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var delivery = await DeliverAsync(notifier, notification, cancellationToken);
            Log(notification, delivery);
            deliveries.Add(delivery);
        }

        return deliveries;
    }

    private async Task<NotificationDelivery> DeliverAsync(
        INotifier notifier,
        IncidentNotification notification,
        CancellationToken cancellationToken)
    {
        var channel = notifier.Channel;
        if (!notifier.IsConfigured)
        {
            return NotificationDelivery.Unavailable(channel);
        }

        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var delivery = await notifier.SendAsync(notification, linked.Token);
            if (delivery is null)
            {
                return NotificationDelivery.Failed(channel, "The channel returned no delivery.");
            }

            return delivery with { Channel = channel };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var seconds = _timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
            return NotificationDelivery.Failed(channel, $"The channel did not answer within the {seconds} second timeout.");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return NotificationDelivery.Failed(channel, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void Log(IncidentNotification notification, NotificationDelivery delivery)
    {
        // Only the delivery's own fields are logged; the stage never sees a channel's settings.
        _logger.LogInformation(
            "Notification for incident {IncidentId} to channel {Channel}: {Status}. Message {MessageId}, url {Url}, reason {Reason}.",
            notification.IncidentId,
            delivery.Channel,
            delivery.Status.Value,
            delivery.MessageId ?? "(none)",
            delivery.Url ?? "(none)",
            delivery.Reason ?? "(none)");
    }
}
