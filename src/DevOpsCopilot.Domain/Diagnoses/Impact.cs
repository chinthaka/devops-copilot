namespace DevOpsCopilot.Domain.Diagnoses;

/// <summary>What one incident touched: its services, resources, event count and time window.</summary>
public sealed record Impact(
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Resources,
    int EventCount,
    string Window);
