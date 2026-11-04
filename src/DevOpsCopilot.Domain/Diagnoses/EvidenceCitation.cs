namespace DevOpsCopilot.Domain.Diagnoses;

/// <summary>A pointer into the diagnostic handoff: the KQL entry and the row a claim rests on.</summary>
public sealed record EvidenceCitation(int Kql, int Row);
