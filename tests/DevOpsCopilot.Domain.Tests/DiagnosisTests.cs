using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Domain.Tests;

public class DiagnosisTests
{
    [Fact]
    public void Create_WithAValueOutsideTheThreeLevels_ThrowsArgumentException()
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
    public void Deterministic_WithOkHandoffs_CarriesTheContractFields()
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
    public void ApplyConfidenceRule_WhenBothToolsWereUnavailable_LowersTheConfidenceToLow()
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
    public void Deterministic_ForASingleEvent_SetsImpactToOneEventItsServiceAndNoResources()
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
    public void Deterministic_WithTheSameHandoffs_RendersByteIdenticalReports()
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
