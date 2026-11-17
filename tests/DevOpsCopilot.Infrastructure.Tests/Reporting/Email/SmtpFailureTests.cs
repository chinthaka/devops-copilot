using DevOpsCopilot.Infrastructure.Reporting.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net.Sockets;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// AC-18, D16: an SMTP failure becomes a reason that names the reply code, the error category or
/// the exception type, and never the server's free text, the username or the password.
/// </summary>
public class SmtpFailureTests
{
    private const string Username = "relay-user@contoso.com";
    private const string Password = "smtp-secret-password";
    private const string Recipient = "oncall-a@contoso.com";

    [Fact]
    public void Describe_WithACommandRejection_NamesTheReplyCodeAndCategoryOnly()
    {
        // Arrange
        var exception = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            MailboxAddress.Parse(Recipient),
            $"5.1.1 <{Recipient}>: Recipient address rejected: User unknown");

        // Act
        var reason = SmtpFailure.Describe(exception);

        // Assert
        Assert.Contains("550", reason, StringComparison.Ordinal);
        Assert.Contains(nameof(SmtpErrorCode.RecipientNotAccepted), reason, StringComparison.Ordinal);
        Assert.DoesNotContain(Recipient, reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User unknown", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WithARejectedLogin_SaysTheLoginWasRejected()
    {
        // Arrange
        var exception = new AuthenticationException($"535: Authentication failed for {Username} with {Password}");

        // Act
        var reason = SmtpFailure.Describe(exception);

        // Assert
        Assert.Contains("login", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Username, reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WithATlsFailure_SaysTheHandshakeFailed()
    {
        // Arrange
        var exception = new SslHandshakeException("The remote certificate is invalid for smtp.contoso.com");

        // Act
        var reason = SmtpFailure.Describe(exception);

        // Assert
        Assert.Contains("TLS", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp.contoso.com", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WithAnyOtherException_NamesOnlyTheType()
    {
        // Arrange
        var exception = new SocketException((int)SocketError.ConnectionRefused);

        // Act
        var reason = SmtpFailure.Describe(exception);

        // Assert
        Assert.Equal($"The SMTP send failed with {nameof(SocketException)}.", reason);
    }
}
