namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The <c>Incident:Notifications</c> configuration section. Every channel value is empty by
/// default, and empty means that channel is unconfigured and records its deliveries unavailable.
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>The per-channel send timeout (requirement D8); the value lives in appsettings.json.</summary>
    public int TimeoutSeconds { get; set; }

    public TeamsOptions Teams { get; set; } = new();

    public SlackOptions Slack { get; set; } = new();

    public DiscordOptions Discord { get; set; } = new();

    public EmailOptions Email { get; set; } = new();
}
