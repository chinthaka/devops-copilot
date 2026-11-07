namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The bot token and target channel for Slack's chat.postMessage (requirement D4). The token is a
/// credential and never printed; the channel id is addressing and is. Blank either means the
/// channel is unconfigured.
/// </summary>
public sealed record SlackSettings(string BotToken, string ChannelId)
{
    public static SlackSettings Unconfigured { get; } = new("", "");

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChannelId);

    /// <summary>Keeps the bot token out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("BotToken = ").Append(string.IsNullOrWhiteSpace(BotToken) ? "(none)" : "(set)")
            .Append(", ChannelId = ").Append(ChannelId);
        return true;
    }
}
