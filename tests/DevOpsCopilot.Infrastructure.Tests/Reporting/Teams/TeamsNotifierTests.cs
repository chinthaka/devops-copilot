using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Teams;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Net;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Teams;

public class TeamsNotifierTests
{
    private const string TriggerUrl =
        "https://prod-27.westus.logic.azure.com/workflows/abc123/triggers/manual/paths/invoke?api-version=2016-06-01&sig=sig-secret-value";

    [Fact]
    public async Task SendAsync_WithAConfiguredWebhook_PostsTheCardToTheTriggerUrl()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var notifier = Notifier(handler);
        var notification = Notifications.Complete();

        // Act
        await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(TriggerUrl), request.Uri);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal(TeamsAdaptiveCard.Build(notification), request.Body);
    }

    [Fact]
    public async Task SendAsync_WhenTheWorkflowAccepts_RecordsItSentWithoutAMessageId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.Accepted);
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal("teams", delivery.Channel);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Null(delivery.MessageId);
        Assert.Null(delivery.Url);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    [InlineData(HttpStatusCode.NotFound, "404")]
    public async Task SendAsync_WhenTheWorkflowRejects_RecordsItFailedWithTheStatusAndNotTheUrl(
        HttpStatusCode status,
        string expectedCode)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(status, """{"error":{"code":"DirectApiAuthorizationRequired"}}""");
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(expectedCode, delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("logic.azure.com", delivery.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig-secret-value", delivery.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestThrows_RecordsItFailedWithTheTypeAndNotTheUrl()
    {
        // Arrange
        var notifier = new TeamsNotifier(
            new TeamsSettings(TriggerUrl),
            new HttpClient(new ThrowingHandler()));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(nameof(HttpRequestException), delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("logic.azure.com", delivery.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig-secret-value", delivery.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WithoutAWebhookUrl_ReportsItselfUnconfigured(string webhookUrl)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var notifier = new TeamsNotifier(new TeamsSettings(webhookUrl), new HttpClient(handler));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.False(notifier.IsConfigured);
        Assert.Equal(NotificationStatus.Unavailable, delivery.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerIsCancelled_PropagatesTheCancellation()
    {
        // Arrange
        var notifier = Notifier(new StubHttpMessageHandler());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => notifier.SendAsync(Notifications.Complete(), cancelled.Token));
    }

    private static TeamsNotifier Notifier(StubHttpMessageHandler handler) =>
        new(new TeamsSettings(TriggerUrl), new HttpClient(handler));

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException(
                $"No such host is known. ({request.RequestUri?.Host}:443)");
    }
}
