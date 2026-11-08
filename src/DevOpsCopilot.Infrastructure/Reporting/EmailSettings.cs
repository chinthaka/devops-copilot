namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Who the email channel sends as and to (requirement D6), whichever transport carries the
/// message (D14). Recipients are trimmed, blank ones dropped and duplicates removed without regard
/// to case. A blank sender or no recipient means the channel is unconfigured. The transport's own
/// settings — and its credentials — live in <see cref="CommunicationServicesEmailSettings"/> and
/// <see cref="SmtpSettings"/>.
/// </summary>
public sealed record EmailSettings(string SenderAddress, IReadOnlyList<string> Recipients)
{
    public static EmailSettings Unconfigured { get; } = new("", []);

    /// <summary>The cleaned recipient list every email is addressed to.</summary>
    public IReadOnlyList<string> To =>
        Recipients
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SenderAddress) && To.Count > 0;

    /// <summary>Prints the recipients as a count, so no address list reaches a log line.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("SenderAddress = ").Append(SenderAddress)
            .Append(", Recipients = ").Append(To.Count);
        return true;
    }
}
