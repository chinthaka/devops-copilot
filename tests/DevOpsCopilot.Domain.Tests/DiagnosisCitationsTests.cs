using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Domain.Tests;

public class DiagnosisCitationsTests
{
    [Fact]
    public void A_citation_to_a_kql_entry_that_does_not_exist_is_invalid()
    {
        // Arrange
        var diagnostic = TwoEntryDiagnostic();
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var diagnosis = Valid(diagnostic, grounding) with { Evidence = [new EvidenceCitation(5, 0)] };

        // Act
        var result = DiagnosisCitations.Validate(diagnosis, diagnostic, grounding);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("kql 5", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_citation_to_a_row_that_does_not_exist_is_invalid()
    {
        // Arrange
        var diagnostic = TwoEntryDiagnostic();
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var diagnosis = Valid(diagnostic, grounding) with { Evidence = [new EvidenceCitation(0, 9)] };

        // Act
        var result = DiagnosisCitations.Validate(diagnosis, diagnostic, grounding);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("row 9", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_similar_incident_that_was_not_a_hit_is_invalid()
    {
        // Arrange
        var diagnostic = TwoEntryDiagnostic();
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"), Handoffs.Hit("h2"));
        var diagnosis = Valid(diagnostic, grounding) with { SimilarIncidents = ["h9"] };

        // Act
        var result = DiagnosisCitations.Validate(diagnosis, diagnostic, grounding);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("h9", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_diagnosis_citing_only_returned_evidence_is_valid()
    {
        // Arrange
        var diagnostic = TwoEntryDiagnostic();
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"), Handoffs.Hit("h2"));
        var diagnosis = Valid(diagnostic, grounding) with
        {
            Evidence = [new EvidenceCitation(0, 0), new EvidenceCitation(1, 1)],
            SimilarIncidents = ["h1", "h2"],
        };

        // Act
        var result = DiagnosisCitations.Validate(diagnosis, diagnostic, grounding);

        // Assert
        Assert.True(result.IsValid);
    }

    private static DiagnosticHandoff TwoEntryDiagnostic()
    {
        return Handoffs.Diagnostic(
            "ok",
            kql: ["query-one", "query-two"],
            rows:
            [
                new Dictionary<string, string> { ["ServiceName"] = "Checkout.Api" },
                new Dictionary<string, string> { ["Count"] = "3" },
            ],
            findings: ["Query `query-one` returned ServiceName=Checkout.Api.", "Query `query-two` returned Count=3."]);
    }

    private static Diagnosis Valid(DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        return Diagnosis.Deterministic(diagnostic, grounding, "reason");
    }
}
