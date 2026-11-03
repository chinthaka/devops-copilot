using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;

namespace DevOpsCopilot.Application.Interfaces;

/// <summary>Persists one rendered draft and reports where it went.</summary>
public interface IReportWriter
{
    Task<string> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string draft,
        CancellationToken cancellationToken);
}
