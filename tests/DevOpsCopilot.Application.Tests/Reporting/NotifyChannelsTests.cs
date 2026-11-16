using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Application.Tests.Common;
using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Application.Tests.Reporting;

public class NotifyChannelsTests
{
    private static readonly string[] ContractOrder = ["teams", "slack", "discord", "email"];

    [Fact]
    public async Task NotifyAsync_WithFourConfiguredChannels_CallsEachOnceInTheContractOrder()
    {
        // Arrange
        var calls = new List<string>();
        var notifiers = ContractOrder.Select(name => new FakeNotifier(name, calls)).ToArray();
        var stage = new NotifyChannels(notifiers, TestTimeouts.Generous);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(ContractOrder, calls);
        Assert.All(notifiers, notifier => Assert.Single(notifier.Sent));
        Assert.Equal(ContractOrder, deliveries.Select(delivery => delivery.Channel));
        Assert.All(deliveries, delivery => Assert.Equal(NotificationStatus.Sent, delivery.Status));
    }

    [Fact]
    public async Task NotifyAsync_WithNoConfiguredChannel_RecordsEveryChannelUnavailableWithoutCallingIt()
    {
        // Arrange
        var notifiers = ContractOrder.Select(name => new FakeNotifier(name) { IsConfigured = false }).ToArray();
        var stage = new NotifyChannels(notifiers, TestTimeouts.Generous);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(4, deliveries.Count);
        Assert.All(deliveries, delivery => Assert.Equal(NotificationStatus.Unavailable, delivery.Status));
        Assert.All(notifiers, notifier => Assert.Empty(notifier.Sent));
    }

    [Fact]
    public async Task NotifyAsync_WhenAChannelThrows_RecordsItFailedAndStillCallsTheRest()
    {
        // Arrange
        var notifiers = new[]
        {
            new FakeNotifier("teams") { Throws = new HttpRequestException("connection refused") },
            new FakeNotifier("slack"),
            new FakeNotifier("discord"),
            new FakeNotifier("email"),
        };
        var stage = new NotifyChannels(notifiers, TestTimeouts.Generous);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(NotificationStatus.Failed, deliveries[0].Status);
        Assert.Contains("connection refused", deliveries[0].Reason, StringComparison.Ordinal);
        Assert.All(notifiers.Skip(1), notifier => Assert.Single(notifier.Sent));
        Assert.All(deliveries.Skip(1), delivery => Assert.Equal(NotificationStatus.Sent, delivery.Status));
    }

    [Fact]
    public async Task NotifyAsync_WhenAChannelAnswersWithAFailure_RecordsItFailedAndStillCallsTheRest()
    {
        // Arrange
        var notifiers = new[]
        {
            new FakeNotifier("teams") { Throws = new InvalidOperationException("boom") },
            new FakeNotifier("slack") { FailsWith = "HTTP 500" },
            new FakeNotifier("discord"),
            new FakeNotifier("email"),
        };
        var stage = new NotifyChannels(notifiers, TestTimeouts.Generous);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(
            ["failed", "failed", "sent", "sent"],
            deliveries.Select(delivery => delivery.Status.Value));
        Assert.Equal("HTTP 500", deliveries[1].Reason);
    }

    [Fact]
    public async Task NotifyAsync_WhenAChannelTimesOut_RecordsItFailedWithTheTimeoutAsTheReason()
    {
        // Arrange
        var hanging = new FakeNotifier("teams") { Hangs = true };
        var next = new FakeNotifier("slack");
        var stage = new NotifyChannels([hanging, next], TimeSpan.Zero);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(NotificationStatus.Failed, deliveries[0].Status);
        Assert.Contains("timeout", deliveries[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(next.Sent);
    }

    [Fact]
    public async Task NotifyAsync_WhenShutdownIsRequestedWhileAChannelSends_PropagatesTheCancellation()
    {
        // Arrange
        var stage = new NotifyChannels([new FakeNotifier("teams") { Hangs = true }], TestTimeouts.Generous);
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => stage.NotifyAsync(Notification(), shutdown.Token));
    }

    [Fact]
    public async Task NotifyAsync_WhenAChannelFails_LogsTheReasonWithoutTheCredential()
    {
        // Arrange
        const string credential = "xoxb-secret-token";
        var logger = new RecordingLogger<NotifyChannels>();
        var stage = new NotifyChannels(
            [new FakeNotifier("slack") { FailsWith = "channel_not_found" }],
            TestTimeouts.Generous,
            logger);

        // Act
        await stage.NotifyAsync(Notification());

        // Assert
        var line = Assert.Single(logger.Lines);
        Assert.Contains("slack", line, StringComparison.Ordinal);
        Assert.Contains("failed", line, StringComparison.Ordinal);
        Assert.Contains("channel_not_found", line, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotifyAsync_WithAnAdditionalNotifierRegistered_CallsItWithoutAnyChangeToTheStage()
    {
        // Arrange
        var fifth = new FakeNotifier("pager");
        var notifiers = ContractOrder.Select(name => new FakeNotifier(name)).Append(fifth).ToArray();
        var stage = new NotifyChannels(notifiers, TestTimeouts.Generous);

        // Act
        var deliveries = await stage.NotifyAsync(Notification());

        // Assert
        Assert.Equal(5, deliveries.Count);
        Assert.Equal("pager", deliveries[4].Channel);
        Assert.Single(fifth.Sent);
    }

    private static IncidentNotification Notification() =>
        new(
            "20261019T120000Z",
            "Checkout times out",
            "Not determined from the available evidence.",
            Confidence.Low,
            "Services: Checkout.Api. Resources: (none). Event count: 1. Window: 2026-10-19.",
            [],
            "KQL was unavailable. No rows were returned.",
            [],
            "Azure AI Search was unavailable. No historical post-mortems were returned.",
            [],
            [],
            "memory://report.md");
}
