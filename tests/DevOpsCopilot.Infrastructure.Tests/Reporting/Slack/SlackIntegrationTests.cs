using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Slack;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Slack;

/// <summary>
/// The one test that posts to a real Slack channel. It needs a Slack app with the chat:write bot
/// scope, installed to the workspace, with the bot invited to the target channel. Configuration
/// comes from user secrets or environment variables and never from the repository. Run it with:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Slack:BotToken" "xoxb-…"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Slack:ChannelId" "C…"
/// </summary>
[Trait("Category", "RequiresSlack")]
public class SlackIntegrationTests
{
    [SlackRequiredFact]
    public async Task SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var notifier = new SlackNotifier(SlackConfiguration.Settings!, httpClient);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), timeout.Token);

        // Assert
        Assert.True(delivery.Status == NotificationStatus.Sent, $"Slack delivery was {delivery.Status}: {delivery.Reason}");
        Assert.False(string.IsNullOrWhiteSpace(delivery.MessageId));
    }
}
