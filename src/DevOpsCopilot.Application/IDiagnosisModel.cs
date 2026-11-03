using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Proposes one diagnosis from the two handoffs, or reports itself unconfigured. An implementation
/// never decides what happens when it fails: it returns a diagnosis, returns null, or throws, and
/// the <see cref="Diagnose"/> stage chooses the fallback.
/// </summary>
public interface IDiagnosisModel
{
    /// <summary>Whether a model is configured. When false, <see cref="ProposeAsync"/> is never called.</summary>
    bool IsConfigured { get; }

    Task<Diagnosis?> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken);
}
