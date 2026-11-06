using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Text;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class TeamsAdaptiveCardTests
{
    [Fact]
    public void Build_WithACompleteNotification_ProducesAMessageWithOneAdaptiveCardAttachment()
    {
        // Act
        var body = TeamsAdaptiveCard.Build(Notifications.Complete());

        // Assert
        var root = JsonNode.Parse(body)!;
        Assert.Equal("message", root["type"]!.GetValue<string>());
        var attachment = Assert.Single(root["attachments"]!.AsArray())!;
        Assert.Equal("application/vnd.microsoft.card.adaptive", attachment["contentType"]!.GetValue<string>());
        Assert.True(attachment.AsObject().ContainsKey("contentUrl"));
        Assert.Null(attachment["contentUrl"]);
        var card = attachment["content"]!;
        Assert.Equal("AdaptiveCard", card["type"]!.GetValue<string>());
        Assert.Equal("1.5", card["version"]!.GetValue<string>());
        Assert.NotEmpty(card["body"]!.AsArray());
    }

    [Fact]
    public void Build_WithACompleteNotification_PutsTheNotificationsTextInTheCard()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var texts = CardTexts(TeamsAdaptiveCard.Build(notification));

        // Assert
        Assert.Contains(notification.Title, texts, StringComparison.Ordinal);
        Assert.Contains(notification.LikelyCause, texts, StringComparison.Ordinal);
        Assert.Contains("medium", texts, StringComparison.Ordinal);
        Assert.Contains(notification.Impact, texts, StringComparison.Ordinal);
        Assert.Contains(notification.Evidence[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidents[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.SuggestedSteps[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.OpenQuestions[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.IncidentId, texts, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, texts, StringComparison.Ordinal);
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
        var texts = CardTexts(TeamsAdaptiveCard.Build(notification));

        // Assert
        Assert.Contains(notification.EvidenceUnavailable!, texts, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidentsUnavailable!, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithAnOversizedDiagnosis_TruncatesTheCardVisibly()
    {
        // Arrange
        var notification = Notifications.Oversized();

        // Act
        var body = TeamsAdaptiveCard.Build(notification);
        var texts = CardTexts(body);

        // Assert
        Assert.InRange(Encoding.UTF8.GetByteCount(body), 1, TeamsAdaptiveCard.MaxBodyBytes);
        Assert.Contains(TeamsAdaptiveCard.TruncationMarker, texts, StringComparison.Ordinal);
        Assert.Contains(notification.IncidentId, texts, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithTheSameNotification_ProducesTheSameBody()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act and assert
        Assert.Equal(TeamsAdaptiveCard.Build(notification), TeamsAdaptiveCard.Build(notification));
    }

    private static string CardTexts(string body)
    {
        var card = JsonNode.Parse(body)!["attachments"]![0]!["content"]!;
        return string.Join(
            "\n",
            card["body"]!.AsArray().Select(block => block!["text"]!.GetValue<string>()));
    }
}
