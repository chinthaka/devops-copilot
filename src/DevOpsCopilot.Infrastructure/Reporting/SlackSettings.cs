namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The bot token, target channel and chat.postMessage URL for Slack (requirement D4). The token is
/// a credential and never printed; the channel id and URL are addressing and are. Blank any of
/// them means the channel is unconfigured.
/// </summary>
public sealed record SlackSettings(string BotToken, string ChannelId, string PostMessageUrl)
{
    public static SlackSettings Unconfigured { get; } = new("", "", "");

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken)
        && !string.IsNullOrWhiteSpace(ChannelId)
        && !string.IsNullOrWhiteSpace(PostMessageUrl);

    /// <summary>Keeps the bot token out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("BotToken = ").Append(string.IsNullOrWhiteSpace(BotToken) ? "(none)" : "(set)")
            .Append(", ChannelId = ").Append(ChannelId)
            .Append(", PostMessageUrl = ").Append(PostMessageUrl);
        return true;
    }
}
