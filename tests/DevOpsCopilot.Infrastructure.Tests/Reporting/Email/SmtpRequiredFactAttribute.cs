namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// A fact that needs an SMTP server. Without one the test is skipped, not failed, so CI and local
/// runs with no settings stay green.
/// </summary>
public sealed class SmtpRequiredFactAttribute : FactAttribute
{
    public SmtpRequiredFactAttribute()
    {
        if (SmtpConfiguration.Settings is null)
        {
            Skip = "Requires an SMTP server. Set Incident:Notifications:Email:Smtp:Host (and :Port and :Security if they differ from appsettings.json), :SenderAddress and :Recipients:0 in user secrets.";
        }
    }
}
