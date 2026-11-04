using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Domain.Tests.Common;

namespace DevOpsCopilot.Domain.Tests.Reporting;

public class IncidentReportTests
{
    [Fact]
    public void Render_WithACompleteDiagnosis_WritesTheSevenContractSectionsInOrder()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason");
        string[] headings =
        [
            "# Incident report:",
            "## Impact",
            "## Likely cause",
            "## Evidence",
            "## Similar incidents",
            "## Suggested steps",
            "## Open questions",
        ];

        // Act
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);

        // Assert
        var position = -1;
        foreach (var heading in headings)
        {
            var next = report.IndexOf(heading, position + 1, StringComparison.Ordinal);
            Assert.True(next > position, heading);
            position = next;
            Assert.Equal(next, report.LastIndexOf(heading, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Render_ForAnyDiagnosis_NamesTheEventTheTraceAndTheHubInTheHeader()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason");

        // Act
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);
        var header = report[..report.IndexOf("## Impact", StringComparison.Ordinal)];

        // Assert
        Assert.Contains(diagnosis.Title, header, StringComparison.Ordinal);
        Assert.Contains("Status:", header, StringComparison.Ordinal);
        Assert.Contains(diagnostic.EventId, header, StringComparison.Ordinal);
        Assert.Contains(diagnostic.TraceId, header, StringComparison.Ordinal);
        Assert.Contains("Hub: logs", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithAMediumConfidenceDiagnosis_StatesTheConfidenceInTheLikelyCauseSection()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason") with
        {
            LikelyCause = "Checkout.Api timed out calling the payment gateway.",
            Confidence = Confidence.Medium,
        };

        // Act
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);
        var start = report.IndexOf("## Likely cause", StringComparison.Ordinal);
        var section = report[start..report.IndexOf("## Evidence", StringComparison.Ordinal)];

        // Assert
        Assert.Contains("Checkout.Api timed out calling the payment gateway.", section, StringComparison.Ordinal);
        Assert.Contains("medium", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenBothToolsWereUnavailable_SaysSoAndListsNoEvidence()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "The diagnosis model was unavailable.");

        // Act
        var report = IncidentReport.Render(diagnosis, diagnostic, grounding);
        var evidenceStart = report.IndexOf("## Evidence", StringComparison.Ordinal);
        var similarStart = report.IndexOf("## Similar incidents", StringComparison.Ordinal);
        var stepsStart = report.IndexOf("## Suggested steps", StringComparison.Ordinal);
        var evidence = report[evidenceStart..similarStart];
        var similar = report[similarStart..stepsStart];

        // Assert
        Assert.Contains("KQL was unavailable", evidence, StringComparison.Ordinal);
        Assert.Contains("search was unavailable", similar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ServiceName", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeoutException on checkout", similar, StringComparison.Ordinal);
    }
}
