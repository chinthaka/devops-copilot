using DevOpsCopilot.Infrastructure.Reporting.Discord;
using DevOpsCopilot.Infrastructure.Reporting.Email;
using DevOpsCopilot.Infrastructure.Reporting.Slack;
using DevOpsCopilot.Infrastructure.Reporting.Teams;
using DevOpsCopilot.Infrastructure.Reporting;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// AC-10: no channel settings record can put its credential in a log line. Grows one theory row
/// per channel as groups 3 to 5 add their settings records.
/// </summary>
public class SettingsRedactionTests
{
    public static TheoryData<object, string> SettingsWithSecrets() =>
        new()
        {
            {
                new TeamsSettings("https://prod-27.westus.logic.azure.com/workflows/abc/triggers/manual/paths/invoke?sig=sig-secret-value"),
                "sig-secret-value"
            },
            {
                new SlackSettings("xoxb-secret-token", "C0123456789", "https://slack.example.test/api/chat.postMessage"),
                "xoxb-secret-token"
            },
            {
                new DiscordSettings("https://discord.com/api/webhooks/123456/webhook-secret-token", MaxRetryDelaySeconds: 10),
                "webhook-secret-token"
            },
            {
                new CommunicationServicesEmailSettings(
                    "endpoint=https://acs.communication.azure.com/;accesskey=secret-access-key",
                    ""),
                "secret-access-key"
            },
            {
                new SmtpSettings("smtp.contoso.com", 587, SmtpSecurity.StartTls, "relay-user", "smtp-secret-password"),
                "smtp-secret-password"
            },
            {
                new EmailTransportSettings(
                    EmailTransportKind.Smtp,
                    CommunicationServicesEmailSettings.Unconfigured,
                    new SmtpSettings("smtp.contoso.com", 587, SmtpSecurity.StartTls, "relay-user", "smtp-secret-password")),
                "smtp-secret-password"
            },
        };

    [Theory]
    [MemberData(nameof(SettingsWithSecrets))]
    public void ToString_WithASecret_DoesNotPrintIt(object settings, string secret)
    {
        // Act
        var printed = settings.ToString()!;

        // Assert
        Assert.DoesNotContain(secret, printed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(set)", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithoutAWebhookUrl_PrintsNone()
    {
        // Act and assert
        Assert.Contains("(none)", TeamsSettings.Unconfigured.ToString(), StringComparison.Ordinal);
    }
}
