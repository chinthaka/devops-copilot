namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The <c>Incident:Notifications:Email:Smtp</c> section, used when the email transport is
/// <c>Smtp</c>. The port and security mode come from <c>appsettings.json</c>, not from code.
/// <see cref="Security"/> is <c>StartTls</c>, <c>SslOnConnect</c> or <c>None</c>. The username and
/// password are optional but set together; the password is a secret.
/// </summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; }

    public string Security { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";
}
