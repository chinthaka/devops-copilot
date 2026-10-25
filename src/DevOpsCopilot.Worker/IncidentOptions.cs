namespace DevOpsCopilot.Worker;

/// <summary>The <c>Incident</c> configuration section. Bound here and nowhere else.</summary>
public sealed class IncidentOptions
{
    public KustoOptions Kusto { get; set; } = new();

    public SearchOptions Search { get; set; } = new();

    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    public string DraftsDirectory { get; set; } = "incidents";
}
