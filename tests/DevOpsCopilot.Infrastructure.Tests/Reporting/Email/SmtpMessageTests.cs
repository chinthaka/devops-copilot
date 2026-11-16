using DevOpsCopilot.Infrastructure.Reporting.Email;
using MimeKit;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>AC-16, D6, D17: the MIME message the SMTP transport hands to the server.</summary>
public class SmtpMessageTests
{
    private static readonly OutgoingEmail Email = new(
        "DoNotReply@incidents.contoso.com",
        ["oncall-a@contoso.com", "oncall-b@contoso.com", "oncall-c@contoso.com"],
        "[DevOpsCopilot] high confidence: Checkout fails",
        "<p>Checkout fails</p>",
        "Checkout fails");

    [Fact]
    public void Build_WithAnOutgoingEmail_ProducesAMultipartAlternativeWithTextThenHtml()
    {
        // Act
        var message = SmtpMessage.Build(Email);

        // Assert
        var alternative = Assert.IsType<MultipartAlternative>(message.Body);
        Assert.Equal(2, alternative.Count);
        var text = Assert.IsType<TextPart>(alternative[0]);
        var html = Assert.IsType<TextPart>(alternative[1]);
        Assert.True(text.IsPlain);
        Assert.True(html.IsHtml);
        Assert.Equal(Email.PlainText, text.Text);
        Assert.Equal(Email.Html, html.Text);
    }

    [Fact]
    public void Build_WithThreeRecipients_AddressesAllOfThemInTo()
    {
        // Act
        var message = SmtpMessage.Build(Email);

        // Assert
        Assert.Equal(Email.To, message.To.Mailboxes.Select(mailbox => mailbox.Address));
        Assert.Empty(message.Cc);
        Assert.Empty(message.Bcc);
    }

    [Fact]
    public void Build_WithAnOutgoingEmail_SetsTheSenderSubjectAndAMessageId()
    {
        // Act
        var message = SmtpMessage.Build(Email);

        // Assert
        Assert.Equal(Email.Sender, Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal(Email.Subject, message.Subject);
        Assert.False(string.IsNullOrWhiteSpace(message.MessageId));
    }

    [Fact]
    public void Build_WithTheSameEmailTwice_GivesEachMessageItsOwnMessageId()
    {
        // Act
        var first = SmtpMessage.Build(Email);
        var second = SmtpMessage.Build(Email);

        // Assert
        Assert.NotEqual(first.MessageId, second.MessageId);
    }
}
