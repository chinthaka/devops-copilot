namespace DevOpsCopilot.Infrastructure;

/// <summary>Where drafts are written when no channel is configured.</summary>
public sealed record ReportSettings(string DraftsDirectory);
