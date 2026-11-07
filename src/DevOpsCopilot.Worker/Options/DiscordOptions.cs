namespace DevOpsCopilot.Worker.Options;

/// <summary>The <c>Incident:Notifications:Discord</c> section: the channel webhook URL, a secret.</summary>
public sealed class DiscordOptions
{
    public string WebhookUrl { get; set; } = "";
}
