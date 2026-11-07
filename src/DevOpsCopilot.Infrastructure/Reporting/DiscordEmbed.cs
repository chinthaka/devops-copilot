using DevOpsCopilot.Domain.Reporting;
using System.Text;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Maps one notification to a webhook payload carrying one embed: the title, a description with
/// the incident line and the likely cause, one field each for impact, evidence, similar incidents,
/// suggested steps, open questions and the report location, and a colour chosen from the
/// confidence. Every line of text comes from the notification (requirement D1). The embed stays
/// within Discord's limits — 256 characters of title, 4 096 of description, 25 fields of 1 024,
/// 6 000 in total, 10 embeds — each cut is marked visibly, and the incident id and the report
/// location are never dropped (AC-9). No mention is allowed, because diagnosis text comes from
/// logs and must never ping a channel.
/// </summary>
public static class DiscordEmbed
{
    public const int MaxEmbeds = 10;
    public const int MaxTitleLength = 256;
    public const int MaxDescriptionLength = 4_096;
    public const int MaxFields = 25;
    public const int MaxFieldNameLength = 256;
    public const int MaxFieldValueLength = 1_024;
    public const int MaxTotalLength = 6_000;

    public const int LowColour = 0x95A5A6;
    public const int MediumColour = 0xF1C40F;
    public const int HighColour = 0xE74C3C;

    public const string TruncationMarker = "…";

    private const string TruncatedList = "… (truncated; see the full report)";

    /// <summary>Tried in order until the embed's total text fits <see cref="MaxTotalLength"/>.</summary>
    private static readonly DiscordEmbedBudget[] Budgets =
    [
        new(MaxDescriptionLength, MaxFieldValueLength),
        new(2_000, 800),
        new(1_000, 600),
        new(500, 400),
        new(300, 300),
    ];

    public static string Build(IncidentNotification notification)
    {
        JsonObject embed = [];
        foreach (var budget in Budgets)
        {
            embed = Embed(notification, budget);
            if (TotalLength(embed) <= MaxTotalLength)
            {
                break;
            }
        }

        return new JsonObject
        {
            ["embeds"] = new JsonArray(embed),
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
        }.ToJsonString();
    }

    private static JsonObject Embed(IncidentNotification notification, DiscordEmbedBudget budget)
    {
        var description =
            $"Incident {notification.IncidentId} (confidence: {notification.Confidence.Value})\n\n" +
            $"**Likely cause**\n{notification.LikelyCause}";

        return new JsonObject
        {
            ["title"] = Clamp(notification.Title, MaxTitleLength),
            ["description"] = Clamp(description, budget.Description),
            ["color"] = Colour(notification.Confidence.Value),
            ["fields"] = new JsonArray(
                Field("Impact", Clamp(notification.Impact, budget.FieldValue)),
                Field("Evidence", ListText(notification.Evidence, notification.EvidenceUnavailable, budget.FieldValue)),
                Field("Similar incidents", ListText(notification.SimilarIncidents, notification.SimilarIncidentsUnavailable, budget.FieldValue)),
                Field("Suggested steps", ListText(notification.SuggestedSteps, "None.", budget.FieldValue)),
                Field("Open questions", ListText(notification.OpenQuestions, "None.", budget.FieldValue)),
                Field("Full report", Clamp(notification.ReportLocation, MaxFieldValueLength))),
        };
    }

    private static int Colour(string confidence) =>
        confidence switch
        {
            "high" => HighColour,
            "medium" => MediumColour,
            _ => LowColour,
        };

    private static string ListText(IReadOnlyList<string> lines, string? whenEmpty, int max)
    {
        if (lines.Count == 0)
        {
            return Clamp(whenEmpty ?? "None.", max);
        }

        // Room is reserved for the marker, so a cut list still states that it was cut.
        var budget = max - TruncatedList.Length - 1;
        var builder = new StringBuilder();
        var truncated = false;
        foreach (var line in lines)
        {
            var entry = (builder.Length == 0 ? "- " : "\n- ") + line;
            if (builder.Length + entry.Length > budget)
            {
                if (builder.Length == 0)
                {
                    // A single line longer than the whole field is cut rather than dropped.
                    builder.Append(Clamp(entry, budget));
                }

                truncated = true;
                break;
            }

            builder.Append(entry);
        }

        if (truncated)
        {
            builder.Append('\n').Append(TruncatedList);
        }

        return builder.ToString();
    }

    private static int TotalLength(JsonObject embed) =>
        embed["title"]!.GetValue<string>().Length
        + embed["description"]!.GetValue<string>().Length
        + embed["fields"]!.AsArray().Sum(field =>
            field!["name"]!.GetValue<string>().Length + field["value"]!.GetValue<string>().Length);

    private static string Clamp(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - TruncationMarker.Length), TruncationMarker);

    private static JsonObject Field(string name, string value) =>
        new()
        {
            ["name"] = name,
            ["value"] = value,
            ["inline"] = false,
        };
}
