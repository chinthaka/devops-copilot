using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Teams;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Teams;

/// <summary>
/// The one test that posts to a real Teams channel through its Workflows flow. Configuration
/// comes from user secrets or environment variables and never from the repository. Run it with:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Teams:WebhookUrl" "https://&lt;trigger-url&gt;"
///
/// A 202 only proves the flow accepted the card; the manual check in validation.md confirms the
/// card actually renders in the channel.
/// </summary>
[Trait("Category", "RequiresTeams")]
public class TeamsIntegrationTests
{
    [TeamsRequiredFact]
    public async Task SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var notifier = new TeamsNotifier(TeamsConfiguration.Settings!, httpClient);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), timeout.Token);

        // Assert
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Null(delivery.MessageId);
    }
}
