using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Domain.Tests;

public class DiagnosisTests
{
    [Fact]
    public void Confidence_rejects_a_value_outside_the_three_levels()
    {
        // Arrange
        string[] allowed = ["low", "medium", "high", "Low", "MEDIUM", "High"];

        // Act
        var parsed = allowed.Select(Confidence.Create).ToArray();

        // Assert
        Assert.Equal(["low", "medium", "high", "low", "medium", "high"], parsed.Select(confidence => confidence.Value));
        Assert.Throws<ArgumentException>(() => Confidence.Create("certain"));
        Assert.Throws<ArgumentException>(() => Confidence.Create(""));
    }

    [Fact]
    public void A_diagnosis_carries_the_contract_fields()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));

        // Act
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "The diagnosis model was unavailable.");

        // Assert
        Assert.Equal(diagnostic.EventId, diagnosis.IncidentId);
        Assert.False(string.IsNullOrWhiteSpace(diagnosis.Title));
        Assert.False(string.IsNullOrWhiteSpace(diagnosis.LikelyCause));
        Assert.Equal(Confidence.Low, diagnosis.Confidence);
        Assert.NotNull(diagnosis.Impact);
        Assert.NotNull(diagnosis.Evidence);
        Assert.Equal(["pm-1"], diagnosis.SimilarIncidents);
        Assert.NotEmpty(diagnosis.SuggestedSteps);
        Assert.Contains("The diagnosis model was unavailable.", diagnosis.OpenQuestions);
    }

    [Fact]
    public void Confidence_is_low_when_both_tools_were_unavailable()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var confident = Diagnosis.Deterministic(diagnostic, grounding, "reason") with { Confidence = Confidence.High };

        // Act
        var ruled = DiagnosisRules.ApplyConfidenceRule(confident, diagnostic, grounding);

        // Assert
        Assert.Equal(Confidence.Low, ruled.Confidence);
    }

    [Fact]
    public void Impact_for_one_event_is_one_event_its_category_and_no_resources()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");

        // Act
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason");

        // Assert
        Assert.Equal(1, diagnosis.Impact.EventCount);
        Assert.Equal(["Checkout.Api"], diagnosis.Impact.Services);
        Assert.Empty(diagnosis.Impact.Resources);
        Assert.Equal(EventIdentity.ParseEventId(diagnostic.EventId).ToString("O"), diagnosis.Impact.Window);
    }

    [Fact]
    public void The_deterministic_diagnosis_is_byte_identical_for_the_same_handoffs()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));

        // Act
        var first = IncidentReport.Render(
            Diagnosis.Deterministic(diagnostic, grounding, "reason"), diagnostic, grounding);
        var second = IncidentReport.Render(
            Diagnosis.Deterministic(diagnostic, grounding, "reason"), diagnostic, grounding);

        // Assert
        Assert.Equal(first, second);
    }
}
