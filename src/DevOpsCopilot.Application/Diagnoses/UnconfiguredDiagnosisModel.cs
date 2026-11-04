using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Diagnoses;

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
