using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Tests.Diagnoses;

/// <summary>
/// A diagnosis model that returns, refuses, throws or waits, so the stage's four fallback paths
/// can be driven from a test.
/// </summary>
internal sealed class RecordingDiagnosisModel : IDiagnosisModel
{
    public bool IsConfigured { get; init; } = true;

    public Diagnosis? Proposed { get; init; }

    public Exception? Throws { get; init; }

    public bool WaitsForCancellation { get; init; }

    public int Proposals { get; private set; }

    public async Task<Diagnosis?> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken)
    {
        Proposals++;
        if (Throws is not null)
        {
            throw Throws;
        }

        if (WaitsForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return Proposed;
    }
}
