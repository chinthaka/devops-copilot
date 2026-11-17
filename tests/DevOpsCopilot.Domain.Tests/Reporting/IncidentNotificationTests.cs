using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Domain.Tests.Common;

namespace DevOpsCopilot.Domain.Tests.Reporting;

public class IncidentNotificationTests
{
    private const string ReportPath = "incidents/2026-10-19-event.md";

    [Fact]
    public void From_WithACompleteDiagnosis_CarriesTheTitleCauseConfidenceImpactAndReportLocation()
    {
        // Arrange
        var (diagnosis, diagnostic, grounding) = Complete();

        // Act
        var notification = IncidentNotification.From(diagnosis, diagnostic, grounding, ReportPath);

        // Assert
        Assert.Equal(diagnosis.IncidentId, notification.IncidentId);
        Assert.Equal(diagnosis.Title, notification.Title);
        Assert.Equal(diagnosis.LikelyCause, notification.LikelyCause);
        Assert.Equal(Confidence.Medium, notification.Confidence);
        Assert.Contains("Checkout.Api", notification.Impact, StringComparison.Ordinal);
        Assert.Contains("payment-db", notification.Impact, StringComparison.Ordinal);
        Assert.Contains("Event count: 3", notification.Impact, StringComparison.Ordinal);
        Assert.Contains(diagnosis.Impact.Window, notification.Impact, StringComparison.Ordinal);
        Assert.Equal(diagnostic.Findings, notification.Evidence);
        Assert.Null(notification.EvidenceUnavailable);
        Assert.Equal(["TimeoutException on checkout: \"Checkout.Api timed out during payment.\""], notification.SimilarIncidents);
        Assert.Null(notification.SimilarIncidentsUnavailable);
        Assert.Equal(diagnosis.SuggestedSteps, notification.SuggestedSteps);
        Assert.Equal(diagnosis.OpenQuestions, notification.OpenQuestions);
        Assert.Equal(ReportPath, notification.ReportLocation);
    }

    [Fact]
    public void From_WithADiagnosis_AddsNoTextThatIsNotInTheDiagnosisOrTheHandoffs()
    {
        // Arrange
        var (diagnosis, diagnostic, grounding) = Complete();

        // Act
        var notification = IncidentNotification.From(diagnosis, diagnostic, grounding, ReportPath);

        // Assert
        Assert.All(notification.Evidence, line => Assert.Contains(line, diagnostic.Findings));
        Assert.All(notification.SimilarIncidents, line => Assert.Contains(
            grounding.Hits,
            hit => line == $"{hit.Title}: \"{hit.Excerpt}\""));
        Assert.All(notification.SuggestedSteps, line => Assert.Contains(line, diagnosis.SuggestedSteps));
        Assert.All(notification.OpenQuestions, line => Assert.Contains(line, diagnosis.OpenQuestions));
        var impactWithoutLabels = notification.Impact
            .Replace("Services: ", "", StringComparison.Ordinal)
            .Replace(". Resources: ", "|", StringComparison.Ordinal)
            .Replace(". Event count: ", "|", StringComparison.Ordinal)
            .Replace(". Window: ", "|", StringComparison.Ordinal)
            .TrimEnd('.');
        Assert.Equal($"Checkout.Api|payment-db|3|{diagnosis.Impact.Window}", impactWithoutLabels);
    }

    [Fact]
    public void From_WhenBothToolsWereUnavailable_SaysSoInsteadOfListingEvidence()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "The diagnosis model was unavailable.");

        // Act
        var notification = IncidentNotification.From(diagnosis, diagnostic, grounding, ReportPath);

        // Assert
        Assert.Empty(notification.Evidence);
        Assert.Empty(notification.SimilarIncidents);
        Assert.Equal(IncidentReport.KqlUnavailable, notification.EvidenceUnavailable);
        Assert.Equal(IncidentReport.SearchUnavailable, notification.SimilarIncidentsUnavailable);
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);
        Assert.Contains(notification.EvidenceUnavailable!, report, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidentsUnavailable!, report, StringComparison.Ordinal);
    }

    [Fact]
    public void From_WithTheSameInputs_ProducesAnEqualNotification()
    {
        // Arrange
        var (diagnosis, diagnostic, grounding) = Complete();

        // Act
        var first = IncidentNotification.From(diagnosis, diagnostic, grounding, ReportPath);
        var second = IncidentNotification.From(diagnosis, diagnostic, grounding, ReportPath);

        // Assert
        Assert.Equal(first.IncidentId, second.IncidentId);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.LikelyCause, second.LikelyCause);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.Equal(first.Impact, second.Impact);
        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(first.EvidenceUnavailable, second.EvidenceUnavailable);
        Assert.Equal(first.SimilarIncidents, second.SimilarIncidents);
        Assert.Equal(first.SimilarIncidentsUnavailable, second.SimilarIncidentsUnavailable);
        Assert.Equal(first.SuggestedSteps, second.SuggestedSteps);
        Assert.Equal(first.OpenQuestions, second.OpenQuestions);
        Assert.Equal(first.ReportLocation, second.ReportLocation);
    }

    private static (Diagnosis, Kql.DiagnosticHandoff, Grounding.GroundingHandoff) Complete()
    {
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason") with
        {
            Title = "Checkout times out on the payment gateway",
            LikelyCause = "The payment gateway stopped answering.",
            Confidence = Confidence.Medium,
            Impact = new Impact(["Checkout.Api"], ["payment-db"], 3, "2026-10-19T12:00:00.0000000+00:00"),
            SuggestedSteps = ["Check the payment gateway's health."],
            OpenQuestions = ["Was the gateway deployed in this window?"],
        };
        return (diagnosis, diagnostic, grounding);
    }
}
