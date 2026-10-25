namespace DevOpsCopilot.Domain;

/// <summary>
/// The two log-store tables the fixed evidence questions read. Supplied by the host from
/// configuration, so the layers below never see the configuration tree.
/// </summary>
public sealed record LogQueryTargets(string ExceptionsTable, string DependenciesTable)
{
    public static LogQueryTargets Unconfigured { get; } = new("", "");
}
