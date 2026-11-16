using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Discord;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Discord;

/// <summary>
/// The one test that posts to a real Discord channel through its webhook. Configuration comes
/// from user secrets or environment variables and never from the repository. Run it with:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:Notifications:Discord:WebhookUrl" "https://discord.com/api/webhooks/…"
/// </summary>
[Trait("Category", "RequiresDiscord")]
public class DiscordIntegrationTests
{
    [DiscordRequiredFact]
    public async Task SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var notifier = new DiscordNotifier(DiscordConfiguration.Settings!, httpClient);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), timeout.Token);

        // Assert
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.False(string.IsNullOrWhiteSpace(delivery.MessageId));
    }
}
