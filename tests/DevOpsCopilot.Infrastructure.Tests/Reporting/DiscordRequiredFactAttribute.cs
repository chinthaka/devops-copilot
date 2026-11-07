namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// A fact that needs a real Discord webhook. Without one the test is skipped, not failed, so CI
/// and local runs with no secrets stay green.
/// </summary>
public sealed class DiscordRequiredFactAttribute : FactAttribute
{
    public DiscordRequiredFactAttribute()
    {
        if (DiscordConfiguration.Settings is null)
        {
            Skip = "Requires a Discord webhook. Set Incident:Notifications:Discord:WebhookUrl in user secrets.";
        }
    }
}
