using DevOpsCopilot.Domain.Grounding;

namespace DevOpsCopilot.Worker.Options;

public sealed class SearchOptions
{
    public string Endpoint { get; set; } = "";

    public string Index { get; set; } = PostmortemOverlap.IndexName;

    public string ApiKey { get; set; } = "";
}
