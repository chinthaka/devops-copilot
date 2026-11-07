namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The channel webhook Discord posts through (requirement D5). Anyone holding the URL can post to
/// the channel, so it is a credential and never printed. Blank means the channel is unconfigured.
/// </summary>
public sealed record DiscordSettings(string WebhookUrl)
{
    public static DiscordSettings Unconfigured { get; } = new("");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(WebhookUrl);

    /// <summary>Keeps the webhook URL out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("WebhookUrl = ").Append(IsConfigured ? "(set)" : "(none)");
        return true;
    }
}
