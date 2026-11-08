using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Net;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class DiscordNotifierTests
{
    private const string WebhookUrl = "https://discord.com/api/webhooks/123456/webhook-secret-token";

    [Fact]
    public async Task SendAsync_WithAConfiguredWebhook_PostsWithWaitTrueAndReportsTheMessageId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, """{"id":"1300000000000000001","channel_id":"42"}""");
        var notifier = Notifier(handler, new RecordingDelay());
        var notification = Notifications.Complete();

        // Act
        var delivery = await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(WebhookUrl + "?wait=true"), request.Uri);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal(DiscordEmbed.Build(notification), request.Body);
        Assert.Equal("discord", delivery.Channel);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal("1300000000000000001", delivery.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithAWebhookUrlThatHasAQuery_KeepsItAndAddsWaitTrue()
    {
        // Arrange: a thread_id query is how a webhook targets a forum thread.
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, """{"id":"1"}""");
        var notifier = new DiscordNotifier(
            new DiscordSettings(WebhookUrl + "?thread_id=99", MaxRetryDelaySeconds: 10),
            new HttpClient(handler),
            new RecordingDelay().DelayAsync);

        // Act
        await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(new Uri(WebhookUrl + "?thread_id=99&wait=true"), Assert.Single(handler.Requests).Uri);
    }

    [Fact]
    public async Task SendAsync_WhenDiscordRateLimits_RetriesOnceAfterRetryAfterThenSucceeds()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.TooManyRequests, """{"retry_after":1.5}""", retryAfterSeconds: 2);
        handler.Respond(HttpStatusCode.OK, """{"id":"7"}""");
        var delay = new RecordingDelay();
        var notifier = Notifier(handler, delay);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(2)], delay.Waits);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal("7", delivery.MessageId);
    }

    [Fact]
    public async Task SendAsync_WhenDiscordRateLimitsTwice_RecordsItFailedWithoutRetryingAgain()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: 1);
        handler.Respond(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: 1);
        handler.Respond(HttpStatusCode.OK, """{"id":"never"}""");
        var delay = new RecordingDelay();
        var notifier = Notifier(handler, delay);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(delay.Waits);
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("429", delivery.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenDiscordRateLimitsWithoutRetryAfter_RecordsItFailedWithoutRetrying()
    {
        // Arrange: D8 retries only when the header says how long to wait.
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.TooManyRequests, "{}");
        var delay = new RecordingDelay();
        var notifier = Notifier(handler, delay);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Empty(delay.Waits);
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
    }

    [Fact]
    public async Task SendAsync_WhenRetryAfterExceedsTheCap_RecordsItFailedWithoutWaiting()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: 3600);
        var delay = new RecordingDelay();
        var notifier = Notifier(handler, delay);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Empty(delay.Waits);
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("3600", delivery.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    [InlineData(HttpStatusCode.NotFound, "404")]
    [InlineData(HttpStatusCode.BadRequest, "400")]
    public async Task SendAsync_WhenDiscordRejects_RecordsItFailedWithTheStatusAndNotTheUrl(
        HttpStatusCode status,
        string expectedCode)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(status, """{"message":"Unknown Webhook","code":10015}""");
        var notifier = Notifier(handler, new RecordingDelay());

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(expectedCode, delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("webhook-secret-token", delivery.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestThrows_RecordsItFailedWithTheTypeAndNotTheUrl()
    {
        // Arrange
        var notifier = new DiscordNotifier(
            new DiscordSettings(WebhookUrl, MaxRetryDelaySeconds: 10),
            new HttpClient(new ThrowingHandler()),
            new RecordingDelay().DelayAsync);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(nameof(HttpRequestException), delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("webhook-secret-token", delivery.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WithoutAWebhookUrl_ReportsItselfUnconfigured(string webhookUrl)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var notifier = new DiscordNotifier(new DiscordSettings(webhookUrl, MaxRetryDelaySeconds: 10), new HttpClient(handler));

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
        var notifier = Notifier(new StubHttpMessageHandler(), new RecordingDelay());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => notifier.SendAsync(Notifications.Complete(), cancelled.Token));
    }

    private static DiscordNotifier Notifier(StubHttpMessageHandler handler, RecordingDelay delay) =>
        new(new DiscordSettings(WebhookUrl, MaxRetryDelaySeconds: 10), new HttpClient(handler), delay.DelayAsync);

    /// <summary>Records each requested wait instead of sleeping, so the retry tests run instantly.</summary>
    private sealed class RecordingDelay
    {
        public List<TimeSpan> Waits { get; } = [];

        public Task DelayAsync(TimeSpan wait, CancellationToken cancellationToken)
        {
            Waits.Add(wait);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException($"Connection refused ({request.RequestUri}).");
    }
}
