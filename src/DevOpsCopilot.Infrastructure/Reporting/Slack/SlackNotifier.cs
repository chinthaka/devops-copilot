using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Reporting;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Reporting.Slack;

/// <summary>
/// Delivers one notification to Slack with chat.postMessage and a bot token (requirement D4).
/// The response body decides the outcome, not the status code: Slack answers 200 OK with
/// <c>"ok": false</c> on failure, so that is a failed delivery carrying Slack's error code, and
/// <c>"ok": true</c> is a sent delivery whose message id is <c>ts</c>.
/// </summary>
public sealed class SlackNotifier : INotifier
{
    public const string ChannelName = "slack";

    private readonly SlackSettings _settings;
    private readonly HttpClient _httpClient;

    public SlackNotifier(SlackSettings settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
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

        var payload = JsonNode.Parse(SlackBlocks.Build(notification))!.AsObject();
        payload["channel"] = _settings.ChannelId;
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_settings.PostMessageUrl)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.BotToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or the stage's timeout; NotifyChannels tells the two apart.
            throw;
        }
        catch (Exception exception)
        {
            // The exception message can carry the request's headers, so only the type is reported.
            return NotificationDelivery.Failed(
                ChannelName,
                $"The POST to Slack failed with {exception.GetType().Name}.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Interpret(response, body);
        }
    }

    private static NotificationDelivery Interpret(HttpResponseMessage response, string body)
    {
        var status = $"{(int)response.StatusCode} {response.StatusCode}";
        if (!TryParse(body, out var parsed))
        {
            // A failure status explains itself. A success status with a body that is not JSON is
            // almost always an incoming-webhook URL configured in place of chat.postMessage: the
            // webhook answers a bare "ok" as text/html. The URL is not named, because a webhook URL
            // is a credential.
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "no content type";
            return NotificationDelivery.Failed(
                ChannelName,
                response.IsSuccessStatusCode
                    ? $"Slack answered {status} with a {mediaType} body that is not JSON. PostMessageUrl must be the chat.postMessage endpoint, not an incoming webhook."
                    : $"Slack answered {status}.");
        }

        bool? ok = parsed?["ok"] is JsonValue value && value.TryGetValue<bool>(out var parsedOk) ? parsedOk : null;
        if (ok == true)
        {
            return NotificationDelivery.Sent(ChannelName, parsed?["ts"]?.GetValue<string>());
        }

        if (ok == false)
        {
            var error = parsed?["error"]?.GetValue<string>() ?? "an unnamed error";
            return NotificationDelivery.Failed(ChannelName, $"Slack answered {error}.");
        }

        return NotificationDelivery.Failed(
            ChannelName,
            response.IsSuccessStatusCode
                ? "Slack answered without an ok field."
                : $"Slack answered {status}.");
    }

    /// <summary>
    /// Parses the response body, answering false instead of throwing when it is not JSON, so the
    /// caller can say so in the delivery's reason rather than lose the cause.
    /// </summary>
    private static bool TryParse(string body, out JsonNode? parsed)
    {
        try
        {
            parsed = JsonNode.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            parsed = null;
            return false;
        }
    }
}
