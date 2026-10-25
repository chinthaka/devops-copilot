using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Worker;

public sealed class SearchOptions
{
    public string Endpoint { get; set; } = "";

    public string Index { get; set; } = PostmortemOverlap.IndexName;

    public string ApiKey { get; set; } = "";
}
