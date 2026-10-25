using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

/// <summary>Builds the two handoffs the diagnose-stage tests share, with single-event values.</summary>
internal static class Handoffs
{
    public static DiagnosticHandoff Diagnostic(
        string kustoStatus,
        IReadOnlyList<string>? kql = null,
        IReadOnlyList<IReadOnlyDictionary<string, string>>? rows = null,
        IReadOnlyList<string>? findings = null)
    {
        findings ??= [];
        rows ??= findings.Select(_ => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
        {
            ["ServiceName"] = "Checkout.Api",
        }).ToArray();
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            "System.TimeoutException: checkout timed out",
            kustoStatus,
            kql ?? ["query-one"],
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
