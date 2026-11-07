using Azure;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Delivers one notification as one email with every configured address in To (requirement D6).
/// The subject and both bodies come from the email templates — each a configured file when the
/// template source has one, otherwise the embedded default (requirement D13). The delivery
/// carries the send operation's id. A Communication Services error is a failed delivery naming
/// its status and error code; any other failure names only the exception type, so no connection
/// string can reach a reason.
/// </summary>
public sealed class EmailNotifier : INotifier
{
    public const string ChannelName = "email";

    private readonly EmailSettings _settings;
    private readonly IEmailTransport _transport;
    private readonly ITemplateSource? _templates;

    public EmailNotifier(EmailSettings settings, IEmailTransport transport, ITemplateSource? templates = null)
    {
        _settings = settings;
        _transport = transport;
        _templates = templates;
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

        var defaults = EmailTemplates.Default;
        var templates = new EmailTemplates(
            await TemplateAsync(TemplateNames.EmailSubject, cancellationToken) ?? defaults.Subject,
            await TemplateAsync(TemplateNames.EmailHtml, cancellationToken) ?? defaults.Html,
            await TemplateAsync(TemplateNames.EmailText, cancellationToken) ?? defaults.PlainText);
        var content = EmailBody.Build(notification, templates);
        var email = new OutgoingEmail(_settings.SenderAddress.Trim(), _settings.To, content.Subject, content.Html, content.PlainText);

        try
        {
            var operationId = await _transport.SendAsync(email, cancellationToken);
            return NotificationDelivery.Sent(ChannelName, operationId);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or the stage's timeout; NotifyChannels tells the two apart.
            throw;
        }
        catch (RequestFailedException exception)
        {
            var code = string.IsNullOrWhiteSpace(exception.ErrorCode) ? "no error code" : exception.ErrorCode;
            return NotificationDelivery.Failed(
                ChannelName,
                $"Communication Services answered {exception.Status} ({code}).");
        }
        catch (Exception exception)
        {
            return NotificationDelivery.Failed(
                ChannelName,
                $"The email send failed with {exception.GetType().Name}.");
        }
    }

    private async Task<string?> TemplateAsync(string name, CancellationToken cancellationToken)
    {
        if (_templates is null)
        {
            return null;
        }

        try
        {
            var template = await _templates.GetAsync(name, cancellationToken);
            return string.IsNullOrWhiteSpace(template) ? null : template;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The source should not throw; if it does, the embedded default still sends the email.
            return null;
        }
    }
}
