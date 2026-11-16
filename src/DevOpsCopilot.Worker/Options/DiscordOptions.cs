namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The <c>Incident:Notifications:Discord</c> section: the channel webhook URL, a secret, and the
/// longest Retry-After the single 429 retry will honour.
/// </summary>
public sealed class DiscordOptions
{
    public string WebhookUrl { get; set; } = "";

    public int MaxRetryDelaySeconds { get; set; }
}
