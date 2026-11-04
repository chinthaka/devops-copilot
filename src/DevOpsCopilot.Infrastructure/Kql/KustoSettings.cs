namespace DevOpsCopilot.Infrastructure.Kql;

/// <summary>The log store this worker queries.</summary>
public sealed record KustoSettings(string ClusterUri, string Database, string ExceptionsTable, string DependenciesTable);
