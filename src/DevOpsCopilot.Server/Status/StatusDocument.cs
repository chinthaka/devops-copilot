namespace DevOpsCopilot.Server.Status;

/// <summary>
/// The dashboard's status document: one JSON answer combining the worker's own status with the
/// enumerated health of the services the dashboard shows. Shape defined in
/// docs/specs/2026-10-25-status-dashboard/requirement.md.
/// </summary>
public sealed record StatusDocument(
    WorkerStatus Worker,
    IReadOnlyList<ServiceHealth> Services,
    DateTimeOffset GeneratedAt);
