using DevOpsCopilot.Infrastructure.Reporting.Email;
using DevOpsCopilot.Infrastructure.Tests.Common;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

public class EmailBodyTests
{
    [Fact]
    public void Build_WithACompleteNotification_ProducesAnHtmlBodyAndAPlainTextAlternative()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var content = EmailBody.Build(notification, EmailTemplates.Default);

        // Assert
        string[] facts =
        [
            notification.IncidentId,
            notification.LikelyCause,
            notification.Impact,
            notification.Evidence[0],
            notification.SuggestedSteps[0],
            notification.OpenQuestions[0],
            notification.ReportLocation,
        ];
        foreach (var fact in facts)
        {
            Assert.Contains(fact, content.PlainText, StringComparison.Ordinal);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(fact), content.Html, StringComparison.Ordinal);
        }

        Assert.Contains("<html", content.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", content.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", content.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithANotification_SubjectNamesTheIncidentAndTheConfidence()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var content = EmailBody.Build(notification, EmailTemplates.Default);

        // Assert
        Assert.Contains(notification.IncidentId, content.Subject, StringComparison.Ordinal);
        Assert.Contains("medium", content.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", content.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithCustomEmailTemplates_FillsTheirPlaceholders()
    {
        // Arrange
        var notification = Notifications.Complete();
        var templates = new EmailTemplates(
            "[{{Confidence}}] {{IncidentId}}",
            "<p>Ursache: {{LikelyCause}}</p><div>{{SuggestedSteps}}</div>",
            "Ursache: {{LikelyCause}}\n{{SuggestedSteps}}");

        // Act
        var content = EmailBody.Build(notification, templates);

        // Assert
        Assert.Equal($"[medium] {notification.IncidentId}", content.Subject);
        Assert.StartsWith("<p>Ursache: The payment gateway stopped answering.</p>", content.Html, StringComparison.Ordinal);
        Assert.Contains("<li>Check the payment gateway&#39;s health.</li>", content.Html, StringComparison.Ordinal);
        Assert.Equal("Ursache: The payment gateway stopped answering.\n- Check the payment gateway's health.", content.PlainText);
    }

    [Fact]
    public void Build_WithMarkupInTheDiagnosis_EncodesItInTheHtmlBody()
    {
        // Arrange: diagnosis text comes from logs and is never trusted as markup.
        var notification = Notifications.Complete() with { LikelyCause = "<script>alert(1)</script>" };

        // Act
        var content = EmailBody.Build(notification, EmailTemplates.Default);

        // Assert
        Assert.DoesNotContain("<script>", content.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", content.Html, StringComparison.Ordinal);
        Assert.Contains("<script>", content.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithALineBreakInTheTitle_KeepsTheSubjectOnOneLine()
    {
        // Arrange
        var notification = Notifications.Complete() with { IncidentId = "abc\r\nBcc: attacker@example.com" };

        // Act
        var content = EmailBody.Build(notification, EmailTemplates.Default);

        // Assert
        Assert.DoesNotContain("\r", content.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", content.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenAToolWasUnavailable_SaysSoInsteadOfListingEvidence()
    {
        // Arrange
        var notification = Notifications.Complete() with
        {
            Evidence = [],
            EvidenceUnavailable = "KQL was unavailable. No rows were returned.",
            SimilarIncidents = [],
            SimilarIncidentsUnavailable = "Azure AI Search was unavailable. No historical post-mortems were returned.",
        };

        // Act
        var content = EmailBody.Build(notification, EmailTemplates.Default);

        // Assert
        Assert.Contains(notification.EvidenceUnavailable!, content.PlainText, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidentsUnavailable!, content.PlainText, StringComparison.Ordinal);
        Assert.Contains(notification.EvidenceUnavailable!, content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithTheSameNotification_ProducesTheSameContent()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act and assert
        Assert.Equal(EmailBody.Build(notification, EmailTemplates.Default), EmailBody.Build(notification, EmailTemplates.Default));
    }
}
