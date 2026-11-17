using DevOpsCopilot.Infrastructure.Reporting.Email;
using MailKit.Security;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// D17 and AC-17: the transport's security mapping and its own configuration rule. The connect,
/// TLS and login path needs a server and is covered by <c>SmtpIntegrationTests</c>.
/// </summary>
public class SmtpEmailTransportTests
{
    [Theory]
    [InlineData(SmtpSecurity.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurity.SslOnConnect, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpSecurity.None, SecureSocketOptions.None)]
    public void SecureSocketOption_ForEachSecurityMode_MapsToTheMatchingMailKitOption(
        SmtpSecurity security,
        SecureSocketOptions expected)
    {
        // Act
        var option = SmtpEmailTransport.SecureSocketOption(security);

        // Assert
        Assert.Equal(expected, option);
        Assert.NotEqual(SecureSocketOptions.StartTlsWhenAvailable, option);
        Assert.NotEqual(SecureSocketOptions.Auto, option);
    }

    [Theory]
    [InlineData("", 587)]
    [InlineData("   ", 587)]
    [InlineData("smtp.contoso.com", 0)]
    [InlineData("smtp.contoso.com", -1)]
    public void IsConfigured_WithoutAHostOrPort_IsFalse(string host, int port)
    {
        // Arrange
        var transport = new SmtpEmailTransport(new SmtpSettings(host, port, SmtpSecurity.StartTls, "", ""));

        // Act and assert
        Assert.False(transport.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WithAHostAndPort_IsTrue()
    {
        // Arrange
        var transport = new SmtpEmailTransport(new SmtpSettings("smtp.contoso.com", 587, SmtpSecurity.StartTls, "", ""));

        // Act and assert
        Assert.True(transport.IsConfigured);
    }
}
