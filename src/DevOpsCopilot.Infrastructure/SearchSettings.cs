namespace DevOpsCopilot.Infrastructure;

/// <summary>The knowledge search service this worker grounds against.</summary>
public sealed record SearchSettings(string Endpoint, string Index, string ApiKey);
