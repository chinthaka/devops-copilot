using MailKit.Net.Smtp;
using MailKit.Security;

namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// Sends through an SMTP server with MailKit (requirements D14 and D15). One connection per
/// report — reports are rare, and a pooled connection would need its own idle and health
/// handling. Connect, login and send all take the caller's token, so the stage's per-channel
/// timeout bounds them together and SMTP has no timing setting of its own. The login happens only
/// when both a username and a password are set. The message id is the message's
/// <c>Message-ID</c> (D17). Cancellation propagates; every other failure becomes an
/// <see cref="EmailTransportException"/> with a safe reason (D16).
/// </summary>
public sealed class SmtpEmailTransport : IEmailTransport
{
    private readonly SmtpSettings _settings;

    public SmtpEmailTransport(SmtpSettings settings)
    {
        _settings = settings;
    }

    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>
    /// Maps the configured security to MailKit's option. <see cref="SmtpSecurity.StartTls"/> is
    /// MailKit's required STARTTLS, never <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.
    /// </summary>
    public static SecureSocketOptions SecureSocketOption(SmtpSecurity security) =>
        security switch
        {
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurity.None => SecureSocketOptions.None,
            _ => throw new ArgumentOutOfRangeException(nameof(security), security, "Unknown SMTP security mode."),
        };

    public async Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var message = SmtpMessage.Build(email);
        var messageId = message.MessageId
            ?? throw new InvalidOperationException("The built message has no Message-ID.");
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOption(_settings.Security), cancellationToken);
            if (_settings.HasCredentials)
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            return messageId;
        }
        catch (OperationCanceledException)
        {
            // Shutdown or the stage's timeout; NotifyChannels tells the two apart.
            throw;
        }
        catch (Exception exception)
        {
            throw new EmailTransportException(SmtpFailure.Describe(exception));
        }
    }
}
