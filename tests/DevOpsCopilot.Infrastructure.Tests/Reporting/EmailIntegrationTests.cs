using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// The one test that queues a real email through Azure Communication Services. It needs a
/// resource with a verified sender domain. Configuration comes from user secrets or environment
/// variables and never from the repository. Run it with:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:ConnectionString" "endpoint=…;accesskey=…"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:SenderAddress" "DoNotReply@…"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Email:Recipients:0" "you@…"
/// </summary>
[Trait("Category", "RequiresEmail")]
public class EmailIntegrationTests
{
    [EmailRequiredFact]
    public async Task SendAsync_AgainstARealCommunicationServicesResource_QueuesOneEmail()
    {
        // Arrange
        var settings = EmailConfiguration.Settings!;
        var notifier = new EmailNotifier(settings, CommunicationServicesEmailTransport.Create(settings));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), timeout.Token);

        // Assert
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.False(string.IsNullOrWhiteSpace(delivery.MessageId));
    }
}
