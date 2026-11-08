namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// The Azure Communication Services resource the email transport sends through (requirement D6).
/// The connection string carries an access key, so it is a credential and never printed. An
/// endpoint with no connection string means managed identity. Both blank means unconfigured.
/// </summary>
public sealed record CommunicationServicesEmailSettings(string ConnectionString, string Endpoint)
{
    public static CommunicationServicesEmailSettings Unconfigured { get; } = new("", "");

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>Keeps the connection string out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("ConnectionString = ").Append(string.IsNullOrWhiteSpace(ConnectionString) ? "(none)" : "(set)")
            .Append(", Endpoint = ").Append(Endpoint);
        return true;
    }
}
