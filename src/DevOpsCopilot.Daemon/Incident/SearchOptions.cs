namespace DevOpsCopilot.Daemon.Incident;

public sealed class SearchOptions
{
    public string Endpoint { get; set; } = "";

    public string Index { get; set; } = PostmortemSearchTool.IndexName;

    public string ApiKey { get; set; } = "";
}
