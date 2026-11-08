namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// A transport's failure, already turned into a reason that is safe to record and log
/// (requirement D16): no credential, username or server free text. The email notifier records the
/// message as the failed delivery's reason without knowing which transport raised it.
/// </summary>
public sealed class EmailTransportException : Exception
{
    public EmailTransportException(string reason)
        : base(reason)
    {
    }
}
