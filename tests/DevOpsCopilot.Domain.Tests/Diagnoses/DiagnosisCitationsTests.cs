using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Domain.Tests.Common;

namespace DevOpsCopilot.Domain.Tests.Diagnoses;

public class DiagnosisCitationsTests
{
    [Fact]
    public void Validate_WithACitationToAMissingKqlEntry_ReturnsInvalid()
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
    public void Validate_WithACitationToAMissingRow_ReturnsInvalid()
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
    public void Validate_WithASimilarIncidentThatWasNotAHit_ReturnsInvalid()
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
    public void Validate_WhenOnlyReturnedEvidenceIsCited_ReturnsValid()
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
