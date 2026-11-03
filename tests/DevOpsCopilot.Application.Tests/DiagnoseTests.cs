using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

public class DiagnoseTests
{
    [Fact]
    public async Task DiagnoseAsync_WithoutAConfiguredModel_UsesTheDeterministicDiagnosis()
    {
        // Arrange
        var model = new RecordingDiagnosisModel { IsConfigured = false };
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        var expected = Diagnosis.Deterministic(diagnostic, grounding, Diagnose.ModelUnavailable);
        Assert.Equal(expected.Title, diagnosis.Title);
        Assert.Equal(expected.LikelyCause, diagnosis.LikelyCause);
        Assert.Equal(expected.Confidence, diagnosis.Confidence);
        Assert.Equal(expected.SuggestedSteps, diagnosis.SuggestedSteps);
        Assert.Equal(expected.OpenQuestions, diagnosis.OpenQuestions);
        Assert.Equal(0, model.Proposals);
    }

    [Fact]
    public async Task DiagnoseAsync_WithoutAConfiguredModel_RecordsTheModelAsUnavailableInTheOpenQuestions()
    {
        // Arrange
        var model = new RecordingDiagnosisModel { IsConfigured = false };
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Contains(diagnosis.OpenQuestions, question => question.Contains("model was unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnoseAsync_WithAWellFormedModelDiagnosis_UsesTheModelDiagnosis()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel { Proposed = Proposed(diagnostic, Confidence.High) };

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal("The payment gateway stopped answering.", diagnosis.LikelyCause);
        Assert.Equal(["Check the payment gateway's health."], diagnosis.SuggestedSteps);
        Assert.Equal(Confidence.High, diagnosis.Confidence);
        Assert.DoesNotContain(diagnosis.OpenQuestions, question => question.Contains("model was unavailable", StringComparison.Ordinal));
        Assert.Equal(1, model.Proposals);
    }

    [Fact]
    public async Task DiagnoseAsync_WhenBothToolsWereUnavailable_LowersAHighModelConfidenceToLow()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable", kql: ["query-one"], rows: [], findings: []);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var model = new RecordingDiagnosisModel
        {
            Proposed = Proposed(diagnostic, Confidence.High) with { Evidence = [], SimilarIncidents = [] },
        };

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal(Confidence.Low, diagnosis.Confidence);
        Assert.Equal("The payment gateway stopped answering.", diagnosis.LikelyCause);
    }

    [Fact]
    public async Task DiagnoseAsync_WhenTheModelDiagnosisIsRejected_FallsBackWithAnOpenQuestion()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel
        {
            Proposed = Proposed(diagnostic, Confidence.High) with { Evidence = [new EvidenceCitation(0, 99)] },
        };

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal("Not determined from the available evidence.", diagnosis.LikelyCause);
        Assert.Equal(Confidence.Low, diagnosis.Confidence);
        Assert.Contains(
            diagnosis.OpenQuestions,
            question => question.Contains("rejected", StringComparison.Ordinal)
                && question.Contains("row 99", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnoseAsync_WhenTheModelThrows_FallsBackWithoutPropagatingTheException()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel { Throws = new InvalidOperationException("the endpoint refused the call") };

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal("Not determined from the available evidence.", diagnosis.LikelyCause);
        Assert.Contains(
            diagnosis.OpenQuestions,
            question => question.Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.DoesNotContain(
            diagnosis.OpenQuestions,
            question => question.Contains("the endpoint refused the call", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnoseAsync_WhenTheModelReturnsNothing_FallsBackToTheDeterministicDiagnosis()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel { Proposed = null };

        // Act
        var diagnosis = await new Diagnose(model).DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal("Not determined from the available evidence.", diagnosis.LikelyCause);
        Assert.Contains(
            diagnosis.OpenQuestions,
            question => question.Contains("returned no diagnosis", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnoseAsync_WhenTheModelTimesOut_FallsBackToTheDeterministicDiagnosis()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel { WaitsForCancellation = true };
        var diagnose = new Diagnose(model, TimeSpan.Zero);

        // Act
        var diagnosis = await diagnose.DiagnoseAsync(diagnostic, grounding);

        // Assert
        Assert.Equal("Not determined from the available evidence.", diagnosis.LikelyCause);
        Assert.Contains(diagnosis.OpenQuestions, question => question.Contains("did not answer within", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnoseAsync_WhenShutdownIsRequestedWhileTheModelRuns_PropagatesTheCancellation()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `query-one` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("h1"));
        var model = new RecordingDiagnosisModel { WaitsForCancellation = true };
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        // Act
        var act = () => new Diagnose(model).DiagnoseAsync(diagnostic, grounding, shutdown.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    private static Diagnosis Proposed(DiagnosticHandoff diagnostic, Confidence confidence)
    {
        return new Diagnosis(
            diagnostic.EventId,
            "TimeoutException on checkout",
            "The payment gateway stopped answering.",
            confidence,
            new Impact(["Checkout.Api"], [], 1, "2026-10-19T12:00:00.0000000+00:00"),
            [new EvidenceCitation(0, 0)],
            ["h1"],
            ["Check the payment gateway's health."],
            []);
    }
}
