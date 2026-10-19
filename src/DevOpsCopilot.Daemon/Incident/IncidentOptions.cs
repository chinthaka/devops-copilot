namespace DevOpsCopilot.Daemon.Incident;

public sealed class IncidentOptions
{
    public KustoOptions Kusto { get; set; } = new();

    public SearchOptions Search { get; set; } = new();

    public string DraftsDirectory { get; set; } = "incidents";
}
