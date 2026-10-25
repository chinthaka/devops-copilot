namespace DevOpsCopilot.Worker;

public sealed class KustoOptions
{
    public string ClusterUri { get; set; } = "";

    public string Database { get; set; } = "";

    public string ExceptionsTable { get; set; } = "";

    public string DependenciesTable { get; set; } = "";
}
