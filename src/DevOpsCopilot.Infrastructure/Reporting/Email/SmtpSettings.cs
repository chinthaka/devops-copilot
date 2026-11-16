namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// The SMTP server the email transport sends through (requirements D14 and D17). A blank host or
/// a non-positive port means unconfigured. The username and password are optional — a relay may
/// accept the sender by network — and the password is a credential, never printed.
/// </summary>
public sealed record SmtpSettings(string Host, int Port, SmtpSecurity Security, string Username, string Password)
{
    public static SmtpSettings Unconfigured { get; } = new("", 0, SmtpSecurity.StartTls, "", "");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && Port > 0;

    /// <summary>A login is attempted only when both halves of it are set.</summary>
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    /// <summary>Keeps the password out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Host = ").Append(Host)
            .Append(", Port = ").Append(Port)
            .Append(", Security = ").Append(Security)
            .Append(", Username = ").Append(string.IsNullOrEmpty(Username) ? "(none)" : "(set)")
            .Append(", Password = ").Append(string.IsNullOrEmpty(Password) ? "(none)" : "(set)");
        return true;
    }
}
