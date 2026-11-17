using Azure.Communication.Email;
using Azure;
using DevOpsCopilot.Infrastructure.Reporting.Email;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// D16: the Communication Services transport maps its own SDK failure to a transport-neutral
/// reason, so the notifier never sees an Azure exception.
/// </summary>
public class CommunicationServicesEmailTransportTests
{
    private const string ConnectionString = "endpoint=https://acs.communication.azure.com/;accesskey=secret-access-key";

    [Fact]
    public async Task SendAsync_WhenCommunicationServicesFails_ThrowsATransportFailureWithTheStatusAndErrorCode()
    {
        // Arrange
        var client = new ThrowingEmailClient(
            new RequestFailedException(400, "Sender domain not verified for " + ConnectionString, "DomainNotLinked", null));
        var transport = new CommunicationServicesEmailTransport(client);
        var email = new OutgoingEmail("DoNotReply@contoso.com", ["oncall@contoso.com"], "Subject", "<p>Body</p>", "Body");

        // Act
        var exception = await Assert.ThrowsAsync<EmailTransportException>(
            () => transport.SendAsync(email, CancellationToken.None));

        // Assert
        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DomainNotLinked", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-access-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsConfigured_ForATransportBuiltFromAClient_IsTrue()
    {
        // Arrange
        var transport = new CommunicationServicesEmailTransport(new ThrowingEmailClient(new InvalidOperationException()));

        // Act and assert
        Assert.True(transport.IsConfigured);
    }

    /// <summary>Azure SDK clients are mockable by design: a protected constructor and virtual calls.</summary>
    private sealed class ThrowingEmailClient(Exception exception) : EmailClient
    {
        public override Task<EmailSendOperation> SendAsync(
            WaitUntil wait,
            EmailMessage message,
            CancellationToken cancellationToken = default) =>
            Task.FromException<EmailSendOperation>(exception);
    }
}
