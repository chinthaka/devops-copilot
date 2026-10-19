namespace DevOpsCopilot.Daemon.Incident;

public sealed record GroundingHandoff(
    string EventId,
    string SearchStatus,
    string Index,
    IReadOnlyList<string> Queries,
    IReadOnlyList<SearchHit> Hits);
