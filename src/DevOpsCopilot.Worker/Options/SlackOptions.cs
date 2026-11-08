namespace DevOpsCopilot.Worker.Options;

/// <summary>The <c>Incident:Notifications:Slack</c> section. The bot token is a secret.</summary>
public sealed class SlackOptions
{
    public string BotToken { get; set; } = "";

    public string ChannelId { get; set; } = "";

    /// <summary>Slack's chat.postMessage endpoint; the value lives in appsettings.json.</summary>
    public string PostMessageUrl { get; set; } = "";
}
