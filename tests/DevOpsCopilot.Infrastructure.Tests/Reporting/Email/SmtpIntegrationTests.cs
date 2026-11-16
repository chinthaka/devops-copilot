using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Email;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// The one test that hands a real email to an SMTP server (AC-16). Use whichever server you like:
/// a local test server of your choosing or a real relay. Configuration comes from user secrets or
/// environment variables and never from the repository. For example:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:Smtp:Host" "localhost"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:Smtp:Port" "&lt;port&gt;"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:Smtp:Security" "None"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:SenderAddress" "devopscopilot@example.test"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:Recipients:0" "oncall@example.test"
///
/// A real relay also needs :Smtp:Username and :Smtp:Password, and usually StartTls on port 587.
/// </summary>
[Trait("Category", "RequiresSmtp")]
public class SmtpIntegrationTests
{
    [SmtpRequiredFact]
    public async Task SendAsync_AgainstAnSmtpServer_DeliversOneMessage()
    {
        // Arrange
        var notifier = new EmailNotifier(SmtpConfiguration.Settings!, new SmtpEmailTransport(SmtpConfiguration.Smtp!));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), timeout.Token);

        // Assert
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.False(string.IsNullOrWhiteSpace(delivery.MessageId));
    }
}
