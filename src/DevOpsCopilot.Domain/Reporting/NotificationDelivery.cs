namespace DevOpsCopilot.Domain.Reporting;

/// <summary>
/// The delivery of one notification to one channel. The message id and permalink are returned
/// where the channel gives them, so an update can find the message once incidents exist (M12);
/// nothing stores them yet.
/// </summary>
public sealed record NotificationDelivery(
    string Channel,
    NotificationStatus Status,
    string? MessageId,
    string? Url,
    string? Reason)
{
    public static NotificationDelivery Sent(string channel, string? messageId = null, string? url = null) =>
        new(channel, NotificationStatus.Sent, messageId, url, null);

    public static NotificationDelivery Unavailable(string channel, string reason = "The channel is not configured.") =>
        new(channel, NotificationStatus.Unavailable, null, null, reason);

    public static NotificationDelivery Failed(string channel, string reason) =>
        new(channel, NotificationStatus.Failed, null, null, reason);
}
