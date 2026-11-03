using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>The default model: none. Keeps the pipeline on the deterministic path.</summary>
public sealed class UnconfiguredDiagnosisModel : IDiagnosisModel
{
    public static UnconfiguredDiagnosisModel Instance { get; } = new();

    private UnconfiguredDiagnosisModel()
    {
    }

    public bool IsConfigured => false;

    public Task<Diagnosis?> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken) => Task.FromResult<Diagnosis?>(null);
}
