using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Reporting;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Delivers one notification to Discord by executing the channel webhook with <c>wait=true</c>, so
/// Discord answers with the created message and its id (requirement D5). A 429 carrying a
/// Retry-After header is retried exactly once after that delay, if the delay is within
/// <see cref="MaxRetryDelay"/>; a second 429, a 429 without the header or a longer delay is a
/// failed delivery (requirement D8). No reason ever carries the webhook URL, because it is the
/// credential.
/// </summary>
public sealed class DiscordNotifier : INotifier
{
    public const string ChannelName = "discord";

    /// <summary>Matches the default per-channel timeout; a longer wait could never finish in time.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    private readonly DiscordSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public DiscordNotifier(
        DiscordSettings settings,
        HttpClient httpClient,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _settings = settings;
        _httpClient = httpClient;
        _delay = delay ?? Task.Delay;
    }

    public string Channel => ChannelName;

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<NotificationDelivery> SendAsync(
        IncidentNotification notification,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return NotificationDelivery.Unavailable(ChannelName);
        }

        var uri = WaitUri(_settings.WebhookUrl);
        var body = DiscordEmbed.Build(notification);
        try
        {
            using var first = await PostAsync(uri, body, cancellationToken);
            if (first.StatusCode != HttpStatusCode.TooManyRequests)
            {
                return await InterpretAsync(first, cancellationToken);
            }

            var wait = first.Headers.RetryAfter?.Delta;
            if (wait is null)
            {
                return NotificationDelivery.Failed(ChannelName, "Discord answered 429 TooManyRequests without Retry-After.");
            }

            if (wait.Value > MaxRetryDelay)
            {
                return NotificationDelivery.Failed(
                    ChannelName,
                    $"Discord answered 429 TooManyRequests and asked to wait {wait.Value.TotalSeconds:0} seconds, longer than the retry budget.");
            }

            await _delay(wait.Value, cancellationToken);
            using var second = await PostAsync(uri, body, cancellationToken);
            return await InterpretAsync(second, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or the stage's timeout; NotifyChannels tells the two apart.
            throw;
        }
        catch (Exception exception)
        {
            // The exception message can carry the webhook URL, so only the type is reported.
            return NotificationDelivery.Failed(
                ChannelName,
                $"The POST to the Discord webhook failed with {exception.GetType().Name}.");
        }
    }

    private async Task<HttpResponseMessage> PostAsync(Uri uri, string body, CancellationToken cancellationToken)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync(uri, content, cancellationToken);
    }

    private static async Task<NotificationDelivery> InterpretAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return NotificationDelivery.Failed(
                ChannelName,
                $"Discord answered {(int)response.StatusCode} {response.StatusCode}.");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        string? id = null;
        try
        {
            id = JsonNode.Parse(text)?["id"]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // Accepted without a readable message; still sent.
        }

        return NotificationDelivery.Sent(ChannelName, id);
    }

    private static Uri WaitUri(string webhookUrl)
    {
        var builder = new UriBuilder(webhookUrl);
        var query = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrEmpty(query) ? "wait=true" : query + "&wait=true";
        return builder.Uri;
    }
}
