using MailKit.Net.Smtp;
using MailKit.Security;

namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// Turns an SMTP failure into a reason that is safe to record (requirement D16): the reply code
/// and MailKit's error category, a rejected login, a failed TLS handshake, or only the exception
/// type. Never the server's free text — it can echo addresses — nor the username or the password.
/// </summary>
public static class SmtpFailure
{
    public static string Describe(Exception exception) =>
        exception switch
        {
            SmtpCommandException command =>
                $"The SMTP server answered {(int)command.StatusCode} ({command.ErrorCode}).",
            AuthenticationException => "The SMTP server rejected the login.",
            SslHandshakeException => "The TLS handshake with the SMTP server failed.",
            _ => $"The SMTP send failed with {exception.GetType().Name}.",
        };
}
