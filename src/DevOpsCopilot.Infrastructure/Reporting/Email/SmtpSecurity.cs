namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// How the SMTP connection is secured (requirement D17). There is deliberately no "TLS when
/// available" mode: a downgrade would quietly send the password in clear.
/// </summary>
public enum SmtpSecurity
{
    /// <summary>Connect in plain text, then upgrade with STARTTLS; refuse to continue without it. Usually port 587.</summary>
    StartTls,

    /// <summary>TLS from the first byte (implicit TLS). Usually port 465.</summary>
    SslOnConnect,

    /// <summary>No TLS at all. Meant only for a local test server.</summary>
    None,
}
