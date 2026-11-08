namespace DevOpsCopilot.Infrastructure.Reporting.Teams;

/// <summary>How many list items and characters per text a Teams card may carry at one truncation step.</summary>
internal readonly record struct TeamsCardBudget(int Items, int Characters);
