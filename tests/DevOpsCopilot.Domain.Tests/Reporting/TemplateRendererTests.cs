using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Domain.Tests.Common;

namespace DevOpsCopilot.Domain.Tests.Reporting;

public class TemplateRendererTests
{
    [Fact]
    public void Render_WithEveryPlaceholderNamed_ReplacesEachTokenWithItsValue()
    {
        // Arrange
        var values = new Dictionary<string, TemplateValue>
        {
            ["Title"] = TemplateValue.Of("Checkout times out"),
            ["Confidence"] = TemplateValue.Of("medium"),
        };

        // Act
        var rendered = TemplateRenderer.Render("[{{Title}}] ({{ Confidence }})", values);

        // Assert
        Assert.Equal("[Checkout times out] (medium)", rendered);
    }

    [Fact]
    public void Render_WithAListValue_ExpandsItToOneBulletLinePerEntry()
    {
        // Arrange
        var values = new Dictionary<string, TemplateValue>
        {
            ["Steps"] = TemplateValue.List(["one", "two"], "unused"),
            ["Evidence"] = TemplateValue.List([], IncidentReport.KqlUnavailable),
        };

        // Act
        var rendered = TemplateRenderer.Render("{{Steps}}\n{{Evidence}}", values);

        // Assert
        Assert.Equal("- one\n- two\n" + IncidentReport.KqlUnavailable, rendered);
    }

    [Fact]
    public void Render_WithAnUnknownPlaceholder_LeavesTheTokenVisible()
    {
        // Arrange
        var values = new Dictionary<string, TemplateValue> { ["Title"] = TemplateValue.Of("t") };

        // Act
        var rendered = TemplateRenderer.Render("{{Title}} {{Titel}}", values);

        // Assert
        Assert.Equal("t {{Titel}}", rendered);
    }

    [Fact]
    public void Render_WithAValueThatContainsAToken_DoesNotExpandItAgain()
    {
        // Arrange
        var values = new Dictionary<string, TemplateValue>
        {
            ["Title"] = TemplateValue.Of("{{Secret}}"),
            ["Secret"] = TemplateValue.Of("leaked"),
        };

        // Act
        var rendered = TemplateRenderer.Render("{{Title}}", values);

        // Assert
        Assert.Equal("{{Secret}}", rendered);
    }

    [Theory]
    [InlineData("ok", "ok", true)]
    [InlineData("unavailable", "unavailable", false)]
    [InlineData("ok", "unavailable", true)]
    public void Render_WithTheDefaultTemplate_MatchesTheHardCodedLayoutByteForByte(string kusto, string search, bool findings)
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic(kusto, findings: findings ? ["Query `q` returned ServiceName=Checkout.Api."] : null);
        var grounding = search == "ok"
            ? Handoffs.Grounding(diagnostic.EventId, search, Handoffs.Hit("pm-1"), Handoffs.Hit("pm-2"))
            : Handoffs.Grounding(diagnostic.EventId, search);
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason") with
        {
            Impact = new Impact(["Checkout.Api", "Payments"], ["payment-db"], 4, "2026-10-19T12:00:00.0000000+00:00"),
        };

        // Act
        var rendered = IncidentReport.Render(diagnosis, diagnostic, grounding);

        // Assert
        Assert.Equal(LegacyIncidentReport.Render(diagnosis, diagnostic, grounding), rendered);
    }

    [Fact]
    public void Render_WithTheDefaultTemplateAndNoStepsOrTrace_MatchesTheHardCodedLayoutByteForByte()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("unavailable", traceId: "");
        var grounding = Handoffs.Grounding(diagnostic.EventId, "unavailable");
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason") with
        {
            SuggestedSteps = [],
            OpenQuestions = [],
        };

        // Act and assert
        Assert.Equal(
            LegacyIncidentReport.Render(diagnosis, diagnostic, grounding),
            IncidentReport.Render(diagnosis, diagnostic, grounding));
    }

    [Fact]
    public void Render_WithACustomTemplate_FollowsItsLayout()
    {
        // Arrange
        var diagnostic = Handoffs.Diagnostic("ok", findings: ["Query `q` returned ServiceName=Checkout.Api."]);
        var grounding = Handoffs.Grounding(diagnostic.EventId, "ok", Handoffs.Hit("pm-1"));
        var diagnosis = Diagnosis.Deterministic(diagnostic, grounding, "reason");
        const string template = "Vorfall {{IncidentId}} ({{Confidence}})\r\n\r\nBelege:\r\n{{Evidence}}\r\n";

        // Act
        var rendered = IncidentReport.Render(diagnosis, diagnostic, grounding, template);

        // Assert
        var nl = Environment.NewLine;
        Assert.Equal(
            $"Vorfall {diagnosis.IncidentId} (low){nl}{nl}Belege:{nl}- Query `q` returned ServiceName=Checkout.Api.{nl}",
            rendered);
    }
}
