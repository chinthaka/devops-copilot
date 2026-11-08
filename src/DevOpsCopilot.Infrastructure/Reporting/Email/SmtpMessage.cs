using MimeKit;
using MimeKit.Utils;

namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// Maps an <see cref="OutgoingEmail"/> to the MIME message the SMTP transport sends (requirement
/// AC-16): every recipient in To (D6), and a <c>multipart/alternative</c> body with the plain text
/// first and the HTML second, least preferred first as RFC 2046 asks. Each message gets a fresh
/// <c>Message-ID</c>, which is also the delivery's message id (D17). Pure, so it is unit tested
/// without a server.
/// </summary>
public static class SmtpMessage
{
    public static MimeMessage Build(OutgoingEmail email)
    {
        var message = new MimeMessage
        {
            Subject = email.Subject,
            MessageId = MimeUtils.GenerateMessageId(),
        };
        message.From.Add(MailboxAddress.Parse(email.Sender));
        foreach (var address in email.To)
        {
            message.To.Add(MailboxAddress.Parse(address));
        }

        message.Body = new BodyBuilder { TextBody = email.PlainText, HtmlBody = email.Html }.ToMessageBody();
        return message;
    }
}
