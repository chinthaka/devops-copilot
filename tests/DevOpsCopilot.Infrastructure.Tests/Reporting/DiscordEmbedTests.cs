using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class DiscordEmbedTests
{
    [Fact]
    public void Build_WithACompleteNotification_ProducesOneEmbedWithTheReportFields()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act
        var payload = JsonNode.Parse(DiscordEmbed.Build(notification))!;

        // Assert
        var embed = Assert.Single(payload["embeds"]!.AsArray())!;
        Assert.Equal(notification.Title, embed["title"]!.GetValue<string>());
        Assert.Contains(notification.LikelyCause, embed["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(
            ["Impact", "Evidence", "Similar incidents", "Suggested steps", "Open questions", "Full report"],
            embed["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>()));
        var texts = EmbedTexts(embed);
        Assert.Contains("medium", texts, StringComparison.Ordinal);
        Assert.Contains(notification.Impact, texts, StringComparison.Ordinal);
        Assert.Contains(notification.Evidence[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidents[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.SuggestedSteps[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.OpenQuestions[0], texts, StringComparison.Ordinal);
        Assert.Contains(notification.IncidentId, texts, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, texts, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("low", DiscordEmbed.LowColour)]
    [InlineData("medium", DiscordEmbed.MediumColour)]
    [InlineData("high", DiscordEmbed.HighColour)]
    public void Build_WithEachConfidence_ChoosesItsColour(string confidence, int expected)
    {
        // Arrange
        var notification = Notifications.Complete() with { Confidence = Confidence.Create(confidence) };

        // Act
        var embed = JsonNode.Parse(DiscordEmbed.Build(notification))!["embeds"]![0]!;

        // Assert
        Assert.Equal(expected, embed["color"]!.GetValue<int>());
    }

    [Fact]
    public void Build_WithMentionsInTheDiagnosis_AllowsNoMentions()
    {
        // Arrange: log text is not trusted, so "@everyone" must never ping anyone.
        var notification = Notifications.Complete() with { LikelyCause = "@everyone the gateway is down" };

        // Act
        var payload = JsonNode.Parse(DiscordEmbed.Build(notification))!;

        // Assert
        Assert.Empty(payload["allowed_mentions"]!["parse"]!.AsArray());
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
        var texts = EmbedTexts(JsonNode.Parse(DiscordEmbed.Build(notification))!["embeds"]![0]!);

        // Assert
        Assert.Contains(notification.EvidenceUnavailable!, texts, StringComparison.Ordinal);
        Assert.Contains(notification.SimilarIncidentsUnavailable!, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithAnOversizedDiagnosis_KeepsTheEmbedWithinDiscordsLimits()
    {
        // Arrange
        var notification = Notifications.Oversized() with { Title = new string('T', 400) };

        // Act
        var payload = JsonNode.Parse(DiscordEmbed.Build(notification))!;

        // Assert
        var embeds = payload["embeds"]!.AsArray();
        Assert.InRange(embeds.Count, 1, DiscordEmbed.MaxEmbeds);
        var embed = embeds[0]!;
        var title = embed["title"]!.GetValue<string>();
        var description = embed["description"]!.GetValue<string>();
        var fields = embed["fields"]!.AsArray();
        Assert.InRange(title.Length, 1, DiscordEmbed.MaxTitleLength);
        Assert.InRange(description.Length, 1, DiscordEmbed.MaxDescriptionLength);
        Assert.InRange(fields.Count, 1, DiscordEmbed.MaxFields);
        foreach (var field in fields)
        {
            Assert.InRange(field!["name"]!.GetValue<string>().Length, 1, DiscordEmbed.MaxFieldNameLength);
            Assert.InRange(field["value"]!.GetValue<string>().Length, 1, DiscordEmbed.MaxFieldValueLength);
        }

        var total = title.Length + description.Length
            + fields.Sum(field => field!["name"]!.GetValue<string>().Length + field["value"]!.GetValue<string>().Length);
        Assert.InRange(total, 1, DiscordEmbed.MaxTotalLength);
        var texts = EmbedTexts(embed);
        Assert.Contains(DiscordEmbed.TruncationMarker, texts, StringComparison.Ordinal);
        Assert.Contains(notification.IncidentId, texts, StringComparison.Ordinal);
        Assert.Contains(notification.ReportLocation, texts, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithTheSameNotification_ProducesTheSameBody()
    {
        // Arrange
        var notification = Notifications.Complete();

        // Act and assert
        Assert.Equal(DiscordEmbed.Build(notification), DiscordEmbed.Build(notification));
    }

    private static string EmbedTexts(JsonNode embed) =>
        string.Join(
            "\n",
            new[] { embed["title"]!.GetValue<string>(), embed["description"]!.GetValue<string>() }
                .Concat(embed["fields"]!.AsArray().Select(field => field!["value"]!.GetValue<string>())));
}
