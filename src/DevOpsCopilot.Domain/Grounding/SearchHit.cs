namespace DevOpsCopilot.Domain.Grounding;

public sealed record SearchHit(string Id, string Title, string Excerpt, double Score);
