namespace DevOpsCopilot.Daemon.Incident;

public sealed record SearchHit(string Id, string Title, string Excerpt, double Score);
