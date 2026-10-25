namespace DevOpsCopilot.Domain;

public sealed record SearchHit(string Id, string Title, string Excerpt, double Score);
