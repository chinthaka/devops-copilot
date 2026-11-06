namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// A fact that needs a real Teams Workflows trigger URL. Without one the test is skipped, not
/// failed, so CI and local runs with no secrets stay green.
/// </summary>
public sealed class TeamsRequiredFactAttribute : FactAttribute
{
    public TeamsRequiredFactAttribute()
    {
        if (TeamsConfiguration.Settings is null)
        {
            Skip = "Requires a Teams Workflows trigger URL. Set Incident:Notifications:Teams:WebhookUrl in user secrets.";
        }
    }
}
