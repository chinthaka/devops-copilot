using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Domain.Pipeline;

public sealed record IncidentWorkflowResult(
    DiagnosticHandoff Diagnostic,
    GroundingHandoff Grounding,
    string DraftPath)
{
    /// <summary>One delivery per registered channel, in registration order. Empty when none is registered.</summary>
    public IReadOnlyList<NotificationDelivery> Deliveries { get; init; } = [];
}
