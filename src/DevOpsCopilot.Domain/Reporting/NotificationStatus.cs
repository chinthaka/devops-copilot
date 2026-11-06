namespace DevOpsCopilot.Domain.Reporting;

/// <summary>The outcome of one channel for one report: sent, unavailable or failed and nothing else.</summary>
public sealed record NotificationStatus
{
    public static readonly NotificationStatus Sent = new("sent");
    public static readonly NotificationStatus Unavailable = new("unavailable");
    public static readonly NotificationStatus Failed = new("failed");

    private NotificationStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
