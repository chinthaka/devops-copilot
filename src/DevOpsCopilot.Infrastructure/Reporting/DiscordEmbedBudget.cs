namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>How long the description and each field value may be at one Discord truncation step.</summary>
internal readonly record struct DiscordEmbedBudget(int Description, int FieldValue);
