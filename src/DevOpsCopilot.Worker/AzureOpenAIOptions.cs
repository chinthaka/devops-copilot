namespace DevOpsCopilot.Worker;

/// <summary>
/// The <c>Incident:AzureOpenAI</c> configuration section. Every value is empty by default: the
/// endpoint, deployment and key come from Aspire, user secrets or Azure configuration, never from
/// the repository. With no endpoint or deployment the worker runs the deterministic diagnosis.
/// </summary>
public sealed class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = "";

    public string Deployment { get; set; } = "";

    /// <summary>Empty means managed identity, which is how the deployed worker authenticates.</summary>
    public string ApiKey { get; set; } = "";

    public int TimeoutSeconds { get; set; }
}
