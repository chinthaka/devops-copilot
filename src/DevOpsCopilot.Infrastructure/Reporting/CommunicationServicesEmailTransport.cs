using Azure;
using Azure.Communication.Email;
using Azure.Identity;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Sends through Azure Communication Services Email with <see cref="WaitUntil.Started"/>: the
/// pipeline never blocks on delivery polling, and a queued message is a success (requirement D6).
/// </summary>
public sealed class CommunicationServicesEmailTransport : IEmailTransport
{
    private readonly EmailClient _client;

    public CommunicationServicesEmailTransport(EmailClient client)
    {
        _client = client;
    }

    /// <summary>The connection string's access key when set; otherwise the endpoint with managed identity.</summary>
    public static CommunicationServicesEmailTransport Create(EmailSettings settings) =>
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
        var operation = await _client.SendAsync(WaitUntil.Started, message, cancellationToken);
        return operation.Id;
    }
}
