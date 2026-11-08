using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting.Slack;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Net;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Slack;

public class SlackNotifierTests
{
    private const string BotToken = "xoxb-secret-token";
    private const string ChannelId = "C0123456789";
    private const string PostMessageUrl = "https://slack.example.test/api/chat.postMessage";

    [Fact]
    public async Task SendAsync_WithAConfiguredBotToken_PostsTheBlocksToTheConfiguredPostMessageUrl()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, """{"ok":true,"ts":"1730970000.123456"}""");
        var notifier = Notifier(handler);
        var notification = Notifications.Complete();

        // Act
        await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(PostMessageUrl), request.Uri);
        Assert.Equal($"Bearer {BotToken}", request.Authorization);
        Assert.Equal("application/json", request.ContentType);
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal(ChannelId, body["channel"]!.GetValue<string>());
        var expected = JsonNode.Parse(SlackBlocks.Build(notification))!;
        Assert.Equal(expected["blocks"]!.ToJsonString(), body["blocks"]!.ToJsonString());
        Assert.Equal(expected["text"]!.GetValue<string>(), body["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendAsync_WhenSlackAnswersOk_ReportsTheTimestampAsTheMessageId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, """{"ok":true,"channel":"C0123456789","ts":"1730970000.123456"}""");
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal("slack", delivery.Channel);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal("1730970000.123456", delivery.MessageId);
    }

    [Fact]
    public async Task SendAsync_WhenSlackAnswersOkFalse_RecordsItFailedWithSlacksErrorCode()
    {
        // Arrange: a 200 is not a success here — the body decides.
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, """{"ok":false,"error":"channel_not_found"}""");
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("channel_not_found", delivery.Reason, StringComparison.Ordinal);
        Assert.Null(delivery.MessageId);
    }

    [Fact]
    public async Task SendAsync_WhenAnIncomingWebhookAnswersPlainTextOk_RecordsItFailedPointingAtThePostMessageUrl()
    {
        // Arrange: an incoming-webhook URL configured in place of chat.postMessage answers a bare
        // "ok" as text/html, which is not the JSON chat.postMessage returns.
        const string webhookUrl = "https://hooks.slack.com/services/T000/B000/webhook-secret-path";
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "ok", mediaType: "text/html");
        var notifier = new SlackNotifier(new SlackSettings(BotToken, ChannelId, webhookUrl), new HttpClient(handler));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("not JSON", delivery.Reason, StringComparison.Ordinal);
        Assert.Contains("text/html", delivery.Reason, StringComparison.Ordinal);
        Assert.Contains("chat.postMessage", delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("webhook-secret-path", delivery.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenSlackAnswersMalformedJson_RecordsItFailedSayingTheBodyIsNotJson()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{\"ok\":");
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("not JSON", delivery.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "429")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    public async Task SendAsync_WhenSlackAnswersANonOkStatus_RecordsItFailedWithTheStatus(
        HttpStatusCode status,
        string expectedCode)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Respond(status);
        var notifier = Notifier(handler);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(expectedCode, delivery.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestThrows_RecordsItFailedWithTheTypeAndNotTheToken()
    {
        // Arrange
        var notifier = new SlackNotifier(
            new SlackSettings(BotToken, ChannelId, PostMessageUrl),
            new HttpClient(new ThrowingHandler()));

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(nameof(HttpRequestException), delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(BotToken, delivery.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", ChannelId)]
    [InlineData("   ", ChannelId)]
    [InlineData(BotToken, "")]
    [InlineData(BotToken, "   ")]
    [InlineData("", "")]
    public async Task SendAsync_WithoutABotTokenOrChannel_ReportsItselfUnconfigured(string botToken, string channelId)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var notifier = new SlackNotifier(new SlackSettings(botToken, channelId, PostMessageUrl), new HttpClient(handler));

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

    private static SlackNotifier Notifier(StubHttpMessageHandler handler) =>
        new(new SlackSettings(BotToken, ChannelId, PostMessageUrl), new HttpClient(handler));

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException($"Connection refused ({request.Headers.Authorization}).");
    }
}
