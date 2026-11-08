using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Reporting;
using System.Text;

namespace DevOpsCopilot.Infrastructure.Reporting.Teams;

/// <summary>
/// Delivers one notification to Microsoft Teams by posting the Adaptive Card to the Workflows
/// trigger URL. The flow answers 202 Accepted with no body and posts the card asynchronously, so
/// a sent delivery carries no message id or permalink (requirement D3). Any other status is a
/// failed delivery whose reason names the status and never the URL, because the URL carries the
/// flow's shared-access signature.
/// </summary>
public sealed class TeamsNotifier : INotifier
{
    public const string ChannelName = "teams";

    private readonly TeamsSettings _settings;
    private readonly HttpClient _httpClient;

    public TeamsNotifier(TeamsSettings settings, HttpClient httpClient)
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

        var body = TeamsAdaptiveCard.Build(notification);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(new Uri(_settings.WebhookUrl), content, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or the stage's timeout; NotifyChannels tells the two apart.
            throw;
        }
        catch (Exception exception)
        {
            // The exception message can carry the trigger URL, so only the type is reported.
            return NotificationDelivery.Failed(
                ChannelName,
                $"The POST to the Teams workflow failed with {exception.GetType().Name}.");
        }

        using (response)
        {
            return response.IsSuccessStatusCode
                ? NotificationDelivery.Sent(ChannelName)
                : NotificationDelivery.Failed(
                    ChannelName,
                    $"The Teams workflow answered {(int)response.StatusCode} {response.StatusCode}.");
        }
    }
}
