namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The channel webhook Discord posts through (requirement D5). Anyone holding the URL can post to
/// the channel, so it is a credential and never printed. Blank means the channel is unconfigured.
/// <see cref="MaxRetryDelaySeconds"/> caps how long a 429's Retry-After may ask the single retry to
/// wait (requirement D8); keep it within the per-channel timeout or the retry can never finish.
/// </summary>
public sealed record DiscordSettings(string WebhookUrl, int MaxRetryDelaySeconds)
{
    public static DiscordSettings Unconfigured { get; } = new("", 0);

    public TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(MaxRetryDelaySeconds);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(WebhookUrl);

    /// <summary>Keeps the webhook URL out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("WebhookUrl = ").Append(IsConfigured ? "(set)" : "(none)")
            .Append(", MaxRetryDelaySeconds = ").Append(MaxRetryDelaySeconds);
        return true;
    }
}
