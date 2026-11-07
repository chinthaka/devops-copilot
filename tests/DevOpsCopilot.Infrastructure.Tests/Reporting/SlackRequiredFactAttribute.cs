namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// A fact that needs a real Slack workspace. Without one the test is skipped, not failed, so CI
/// and local runs with no secrets stay green.
/// </summary>
public sealed class SlackRequiredFactAttribute : FactAttribute
{
    public SlackRequiredFactAttribute()
    {
        if (SlackConfiguration.Settings is null)
        {
            Skip = "Requires a Slack bot token and channel. Set Incident:Notifications:Slack:BotToken and :ChannelId in user secrets.";
        }
    }
}
