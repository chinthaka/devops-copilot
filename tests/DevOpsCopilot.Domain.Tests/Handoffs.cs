using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Domain.Tests;

/// <summary>Builds the two handoffs the diagnosis tests share, with sensible single-event values.</summary>
internal static class Handoffs
{
    public static DiagnosticHandoff Diagnostic(
        string kustoStatus,
        IReadOnlyList<string>? kql = null,
        IReadOnlyList<IReadOnlyDictionary<string, string>>? rows = null,
        IReadOnlyList<string>? findings = null,
        string exception = "System.TimeoutException: checkout timed out",
        string traceId = "4bf92f3577b34da6a3ce929d0e0e4736")
    {
        findings ??= [];
        rows ??= findings.Select(_ => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
        {
            ["ServiceName"] = "Checkout.Api",
        }).ToArray();
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            traceId,
            "00f067aa0ba902b7",
            "Checkout.Api",
            exception,
            kustoStatus,
            kql ?? ["ExceptionsTable | where TraceId == \"4bf92f3577b34da6a3ce929d0e0e4736\""],
            rows,
            findings);
    }

    public static GroundingHandoff Grounding(string eventId, string searchStatus, params SearchHit[] hits)
    {
        return new GroundingHandoff(eventId, searchStatus, PostmortemOverlap.IndexName, ["Checkout.Api"], hits);
    }

    public static SearchHit Hit(string id)
    {
        return new SearchHit(id, "TimeoutException on checkout", "Checkout.Api timed out during payment.", 0.9);
    }
}
