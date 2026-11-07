using Azure;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class EmailNotifierTests
{
    private const string ConnectionString = "endpoint=https://acs.communication.azure.com/;accesskey=secret-access-key";
    private const string Sender = "DoNotReply@incidents.contoso.com";

    private static readonly string[] Recipients =
        ["oncall-a@contoso.com", "oncall-b@contoso.com", "oncall-c@contoso.com"];

    [Fact]
    public async Task SendAsync_WithThreeConfiguredRecipients_SendsOneMessageAddressedToAllOfThem()
    {
        // Arrange
        var transport = new FakeEmailTransport();
        var notifier = new EmailNotifier(Settings(), transport);
        var notification = Notifications.Complete();

        // Act
        var delivery = await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        var email = Assert.Single(transport.Sent);
        Assert.Equal(Sender, email.Sender);
        Assert.Equal(Recipients, email.To);
        var expected = EmailBody.Build(notification, EmailTemplates.Default);
        Assert.Equal(expected.Subject, email.Subject);
        Assert.Equal(expected.Html, email.Html);
        Assert.Equal(expected.PlainText, email.PlainText);
        Assert.Equal("email", delivery.Channel);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal("operation-1", delivery.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithBlankAndDuplicateRecipients_AddressesEachOnce()
    {
        // Arrange
        var transport = new FakeEmailTransport();
        var settings = Settings() with { Recipients = ["a@contoso.com", " ", "A@contoso.com", "b@contoso.com "] };
        var notifier = new EmailNotifier(settings, transport);

        // Act
        await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(["a@contoso.com", "b@contoso.com"], Assert.Single(transport.Sent).To);
    }

    [Fact]
    public async Task SendAsync_WhenTheTransportFails_RecordsItFailedWithTheErrorCode()
    {
        // Arrange
        var transport = new FakeEmailTransport
        {
            Throws = new RequestFailedException(400, "Sender domain not verified for " + ConnectionString, "DomainNotLinked", null),
        };
        var notifier = new EmailNotifier(Settings(), transport);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains("DomainNotLinked", delivery.Reason, StringComparison.Ordinal);
        Assert.Contains("400", delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-access-key", delivery.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenTheTransportThrowsSomethingElse_RecordsItFailedWithTheType()
    {
        // Arrange
        var transport = new FakeEmailTransport { Throws = new InvalidOperationException(ConnectionString) };
        var notifier = new EmailNotifier(Settings(), transport);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Contains(nameof(InvalidOperationException), delivery.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-access-key", delivery.Reason, StringComparison.Ordinal);
    }

    public static TheoryData<EmailSettings> Unconfigured() =>
        new()
        {
            new EmailSettings("", "", Sender, Recipients),
            new EmailSettings(ConnectionString, "", "", Recipients),
            new EmailSettings(ConnectionString, "", Sender, []),
            new EmailSettings(ConnectionString, "", Sender, ["", "  "]),
        };

    [Theory]
    [MemberData(nameof(Unconfigured))]
    public async Task SendAsync_WithoutASenderOrRecipients_ReportsItselfUnconfigured(EmailSettings settings)
    {
        // Arrange
        var transport = new FakeEmailTransport();
        var notifier = new EmailNotifier(settings, transport);

        // Act
        var delivery = await notifier.SendAsync(Notifications.Complete(), CancellationToken.None);

        // Assert
        Assert.False(notifier.IsConfigured);
        Assert.Equal(NotificationStatus.Unavailable, delivery.Status);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void IsConfigured_WithAnEndpointAndNoConnectionString_IsTrueForManagedIdentity()
    {
        // Arrange
        var settings = new EmailSettings("", "https://acs.communication.azure.com/", Sender, Recipients);

        // Act
        var notifier = new EmailNotifier(settings, new FakeEmailTransport());

        // Assert
        Assert.True(notifier.IsConfigured);
    }

    [Fact]
    public async Task SendAsync_WithCustomTemplatesFromTheSource_RendersThroughThem()
    {
        // Arrange
        var transport = new FakeEmailTransport();
        var source = new FakeTemplateSource(new Dictionary<string, string>
        {
            [TemplateNames.EmailSubject] = "Vorfall {{IncidentId}}",
            [TemplateNames.EmailText] = "Ursache: {{LikelyCause}}",
        });
        var notifier = new EmailNotifier(Settings(), transport, source);
        var notification = Notifications.Complete();

        // Act
        await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        var email = Assert.Single(transport.Sent);
        Assert.Equal($"Vorfall {notification.IncidentId}", email.Subject);
        Assert.Equal($"Ursache: {notification.LikelyCause}", email.PlainText);
        Assert.Equal(EmailBody.Build(notification, EmailTemplates.Default).Html, email.Html);
        Assert.Equal([TemplateNames.EmailSubject, TemplateNames.EmailHtml, TemplateNames.EmailText], source.Requested);
    }

    [Fact]
    public async Task SendAsync_WhenTheTemplateSourceThrows_StillSendsWithTheDefaults()
    {
        // Arrange
        var transport = new FakeEmailTransport();
        var notifier = new EmailNotifier(Settings(), transport, new FakeTemplateSource([]) { Throws = true });
        var notification = Notifications.Complete();

        // Act
        var delivery = await notifier.SendAsync(notification, CancellationToken.None);

        // Assert
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal(EmailBody.Build(notification, EmailTemplates.Default).Subject, Assert.Single(transport.Sent).Subject);
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerIsCancelled_PropagatesTheCancellation()
    {
        // Arrange
        var notifier = new EmailNotifier(Settings(), new FakeEmailTransport());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => notifier.SendAsync(Notifications.Complete(), cancelled.Token));
    }

    private static EmailSettings Settings() => new(ConnectionString, "", Sender, Recipients);

    private sealed class FakeTemplateSource(Dictionary<string, string> templates) : ITemplateSource
    {
        public bool Throws { get; init; }

        public List<string> Requested { get; } = [];

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
        {
            Requested.Add(name);
            if (Throws)
            {
                throw new IOException("share unavailable");
            }

            return Task.FromResult(templates.TryGetValue(name, out var template) ? template : null);
        }
    }
}
