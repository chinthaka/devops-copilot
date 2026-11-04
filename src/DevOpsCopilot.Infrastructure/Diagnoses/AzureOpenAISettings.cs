namespace DevOpsCopilot.Infrastructure.Diagnoses;

/// <summary>
/// The Azure OpenAI deployment that produces the diagnosis. Every value comes from configuration;
/// there is no default endpoint, deployment or key, so an unconfigured worker simply runs the
/// deterministic path. An empty key means managed identity.
/// </summary>
public sealed record AzureOpenAISettings(string Endpoint, string Deployment, string ApiKey, int TimeoutSeconds)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>No endpoint, no deployment, no key: the diagnosis model is unavailable.</summary>
    public static AzureOpenAISettings Unconfigured { get; } = new("", "", "", 0);

    public TimeSpan Timeout => TimeoutSeconds > 0 ? TimeSpan.FromSeconds(TimeoutSeconds) : DefaultTimeout;

    /// <summary>Keeps the key out of logs and error messages. Records print their members by default.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Endpoint = ").Append(Endpoint)
            .Append(", Deployment = ").Append(Deployment)
            .Append(", ApiKey = ").Append(string.IsNullOrEmpty(ApiKey) ? "(none)" : "(set)")
            .Append(", TimeoutSeconds = ").Append(TimeoutSeconds);
        return true;
    }
}
