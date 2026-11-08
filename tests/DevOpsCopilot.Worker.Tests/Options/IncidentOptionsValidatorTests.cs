using DevOpsCopilot.Worker.Options;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Worker.Tests.Options;

/// <summary>
/// The startup checks: values with no default in code must be present and valid, and a bad one
/// stops the worker with an error naming its key (AC-17, D14, D17, and the 2026-11-07 revision).
/// </summary>
public class IncidentOptionsValidatorTests
{
    [Fact]
    public void Validate_WithTheShippedAppSettings_Passes()
    {
        // Arrange
        var options = Shipped();

        // Act
        var exception = Record.Exception(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Smtpp")]
    [InlineData("")]
    [InlineData("1")]
    public void Validate_WithAnUnknownEmailTransport_ThrowsNamingTheKey(string transport)
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Email.Transport = transport;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("Incident:Notifications:Email:Transport", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CommunicationServices", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("smtp")]
    [InlineData("communicationservices")]
    public void Validate_WithATransportInAnyCase_Passes(string transport)
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Email.Transport = transport;

        // Act
        var exception = Record.Exception(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("relay-user", "")]
    [InlineData("", "smtp-secret-password")]
    public void Validate_WithAnSmtpUsernameButNoPassword_ThrowsNamingTheKeys(string username, string password)
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Email.Smtp.Username = username;
        options.Notifications.Email.Smtp.Password = password;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("Incident:Notifications:Email:Smtp:Username", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Incident:Notifications:Email:Smtp:Password", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp-secret-password", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Opportunistic")]
    [InlineData("")]
    [InlineData("StartTlsWhenAvailable")]
    public void Validate_WithSmtpSelectedAndAnUnknownSecurityMode_ThrowsNamingTheKey(string security)
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Email.Transport = "Smtp";
        options.Notifications.Email.Smtp.Security = security;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("Incident:Notifications:Email:Smtp:Security", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public void Validate_WithSmtpSelectedAndANonPositivePort_ThrowsNamingTheKey(int port)
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Email.Transport = "Smtp";
        options.Notifications.Email.Smtp.Port = port;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("Incident:Notifications:Email:Smtp:Port", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithCommunicationServicesSelectedAndBlankSmtpSettings_Passes()
    {
        // Arrange: the unselected transport's settings are not checked.
        var options = Shipped();
        options.Notifications.Email.Transport = "CommunicationServices";
        options.Notifications.Email.Smtp.Security = "";
        options.Notifications.Email.Smtp.Port = 0;

        // Act
        var exception = Record.Exception(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Incident:AzureOpenAI:TimeoutSeconds")]
    [InlineData("Incident:Notifications:TimeoutSeconds")]
    [InlineData("Incident:Notifications:Discord:MaxRetryDelaySeconds")]
    public void Validate_WithANonPositiveTimeout_ThrowsNamingTheKey(string key)
    {
        // Arrange
        var options = Shipped();
        switch (key)
        {
            case "Incident:AzureOpenAI:TimeoutSeconds": options.AzureOpenAI.TimeoutSeconds = 0; break;
            case "Incident:Notifications:TimeoutSeconds": options.Notifications.TimeoutSeconds = 0; break;
            default: options.Notifications.Discord.MaxRetryDelaySeconds = 0; break;
        }

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithoutASlackPostMessageUrl_ThrowsNamingTheKey()
    {
        // Arrange
        var options = Shipped();
        options.Notifications.Slack.PostMessageUrl = " ";

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => IncidentOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("Incident:Notifications:Slack:PostMessageUrl", exception.Message, StringComparison.Ordinal);
    }

    private static IncidentOptions Shipped() =>
        new ConfigurationBuilder()
            .AddJsonFile("worker.appsettings.json", optional: false)
            .Build()
            .GetSection("Incident")
            .Get<IncidentOptions>()!;
}
