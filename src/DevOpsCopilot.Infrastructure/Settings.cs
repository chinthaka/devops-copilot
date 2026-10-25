namespace DevOpsCopilot.Infrastructure;

/// <summary>Where the stream and its checkpoints live. Built by the host from configuration.</summary>
public sealed record EventHubSettings(string EventHubConnectionString, string CheckpointConnectionString);

/// <summary>The log store this worker queries.</summary>
public sealed record KustoSettings(string ClusterUri, string Database, string ExceptionsTable, string DependenciesTable);

/// <summary>The knowledge search service this worker grounds against.</summary>
public sealed record SearchSettings(string Endpoint, string Index, string ApiKey);

/// <summary>Where drafts are written when no channel is configured.</summary>
public sealed record ReportSettings(string DraftsDirectory);
