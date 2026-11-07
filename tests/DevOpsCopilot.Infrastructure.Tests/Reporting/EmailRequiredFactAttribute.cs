namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// A fact that needs a real Communication Services resource. Without one the test is skipped,
/// not failed, so CI and local runs with no secrets stay green.
/// </summary>
public sealed class EmailRequiredFactAttribute : FactAttribute
{
    public EmailRequiredFactAttribute()
    {
        if (EmailConfiguration.Settings is null)
        {
            Skip = "Requires a Communication Services resource. Set Incident:Notifications:Email:ConnectionString (or :Endpoint), :SenderAddress and :Recipients:0 in user secrets.";
        }
    }
}
