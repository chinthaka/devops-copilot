namespace DevOpsCopilot.Infrastructure;

/// <summary>Where the stream and its checkpoints live. Built by the host from configuration.</summary>
public sealed record EventHubSettings(string EventHubConnectionString, string CheckpointConnectionString);

/// <summary>The log store this worker queries.</summary>
public sealed record KustoSettings(string ClusterUri, string Database, string ExceptionsTable, string DependenciesTable);

/// <summary>The knowledge search service this worker grounds against.</summary>
public sealed record SearchSettings(string Endpoint, string Index, string ApiKey);

/// <summary>Where drafts are written when no channel is configured.</summary>
public sealed record ReportSettings(string DraftsDirectory);

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
