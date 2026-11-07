namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The Communication Services resource, sender and recipients for the email channel (requirement
/// D6). A connection string authenticates with its access key; an endpoint with no connection
/// string means managed identity. The connection string is a credential and never printed, and
/// recipients are printed as a count only. A missing resource, a blank sender or no non-blank
/// recipient means the channel is unconfigured.
/// </summary>
public sealed record EmailSettings(
    string ConnectionString,
    string Endpoint,
    string SenderAddress,
    IReadOnlyList<string> Recipients)
{
    public static EmailSettings Unconfigured { get; } = new("", "", "", []);

    /// <summary>Trimmed, non-blank and de-duplicated without regard to case, in configured order.</summary>
    public IReadOnlyList<string> To =>
        Recipients
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool IsConfigured =>
        (!string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(Endpoint))
        && !string.IsNullOrWhiteSpace(SenderAddress)
        && To.Count > 0;

    /// <summary>Keeps the connection string and the addresses out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("ConnectionString = ").Append(string.IsNullOrWhiteSpace(ConnectionString) ? "(none)" : "(set)")
            .Append(", Endpoint = ").Append(Endpoint)
            .Append(", SenderAddress = ").Append(SenderAddress)
            .Append(", Recipients = ").Append(To.Count);
        return true;
    }
}
