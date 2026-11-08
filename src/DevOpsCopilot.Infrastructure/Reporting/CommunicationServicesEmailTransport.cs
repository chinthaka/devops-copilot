using Azure;
using Azure.Communication.Email;
using Azure.Identity;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Sends through Azure Communication Services Email (requirement D6): one message with every
/// recipient in To, started with <see cref="WaitUntil.Started"/> so the pipeline never blocks on
/// delivery polling; the operation id is the message id. A service error becomes an
/// <see cref="EmailTransportException"/> naming its status and error code, and nothing else, so
/// the connection string cannot reach a reason (D16).
/// </summary>
public sealed class CommunicationServicesEmailTransport : IEmailTransport
{
    private readonly EmailClient _client;

    public CommunicationServicesEmailTransport(EmailClient client)
    {
        _client = client;
    }

    /// <summary>Built only from configured settings; the composition uses the unconfigured transport otherwise.</summary>
    public bool IsConfigured => true;

    /// <summary>An access key from the connection string, otherwise managed identity on the endpoint.</summary>
    public static CommunicationServicesEmailTransport Create(CommunicationServicesEmailSettings settings) =>
        new(string.IsNullOrWhiteSpace(settings.ConnectionString)
            ? new EmailClient(new Uri(settings.Endpoint), new DefaultAzureCredential())
            : new EmailClient(settings.ConnectionString));

    public async Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            email.Sender,
            new EmailRecipients(email.To.Select(address => new EmailAddress(address)).ToList()),
            new Azure.Communication.Email.EmailContent(email.Subject)
            {
                Html = email.Html,
                PlainText = email.PlainText,
            });
        try
        {
            var operation = await _client.SendAsync(WaitUntil.Started, message, cancellationToken);
            return operation.Id;
        }
        catch (RequestFailedException exception)
        {
            var code = string.IsNullOrWhiteSpace(exception.ErrorCode) ? "no error code" : exception.ErrorCode;
            throw new EmailTransportException($"Communication Services answered {exception.Status} ({code}).");
        }
    }
}
