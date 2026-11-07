using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class SlackBlocksTests
{
    [Fact]
    public void Build_WithACompleteNotification_ProducesBlocksWhoseTextComesFromTheNotification()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var payload = JsonNode.Parse(SlackBlocks.Build(notification))!;
        var texts = BlockTexts(payload);

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
    public void Build_WithACompleteNotification_StartsWithAHeaderAndCarriesAFallbackText()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var payload = JsonNode.Parse(SlackBlocks.Build(notification))!;

        // Assert
        var first = payload["blocks"]!.AsArray()[0]!;
        Assert.Equal("header", first["type"]!.GetValue<string>());
        Assert.Equal("plain_text", first["text"]!["type"]!.GetValue<string>());
        Assert.Equal(notification.Title, first["text"]!["text"]!.GetValue<string>());
        var fallback = payload["text"]!.GetValue<string>();
        Assert.Contains(notification.IncidentId, fallback, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, fallback, StringComparison.Ordinal);
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
        var texts = BlockTexts(JsonNode.Parse(SlackBlocks.Build(notification))!);

        // Assert
        Assert.Contains(notification.EvidenceUnavailable!, texts, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidentsUnavailable!, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithAnOversizedDiagnosis_KeepsEveryBlockWithinSlacksLimits()
    {
        // Arrange
        var notification = Notifications.Oversized();

        // Act
        var payload = JsonNode.Parse(SlackBlocks.Build(notification))!;
        var blocks = payload["blocks"]!.AsArray();

        // Assert
        Assert.InRange(blocks.Count, 1, SlackBlocks.MaxBlocks);
        foreach (var block in blocks)
        {
            var text = block!["text"]!["text"]!.GetValue<string>();
            var limit = block["type"]!.GetValue<string>() == "header"
                ? SlackBlocks.MaxHeaderLength
                : SlackBlocks.MaxTextLength;
            Assert.InRange(text.Length, 1, limit);
        }

        var texts = BlockTexts(payload);
        Assert.Contains(SlackBlocks.TruncationMarker, texts, StringComparison.Ordinal);
        Assert.Contains(notification.IncidentId, texts, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithTheSameNotification_ProducesTheSameBody()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act and assert
        Assert.Equal(SlackBlocks.Build(notification), SlackBlocks.Build(notification));
    }

    private static string BlockTexts(JsonNode payload) =>
        string.Join(
            "\n",
            payload["blocks"]!.AsArray().Select(block => block!["text"]!["text"]!.GetValue<string>()));
}
