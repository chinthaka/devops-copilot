namespace DevOpsCopilot.Domain;

public sealed record GroundingHandoff(
    string EventId,
    string SearchStatus,
    string Index,
    IReadOnlyList<string> Queries,
    IReadOnlyList<SearchHit> Hits);
