using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>Persists one rendered draft and reports where it went.</summary>
public interface IReportWriter
{
    Task<string> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string draft,
        CancellationToken cancellationToken);
}
