using DevOpsCopilot.Domain.Reporting;
using System.Text;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Maps one notification to the body the Workflows trigger expects: a <c>message</c> carrying one
/// Adaptive Card 1.5 attachment. Every line of text comes from the notification (requirement D1);
/// only the section labels are this builder's own. When the full content would exceed the card
/// limit, text and lists are cut down step by step, each cut marked visibly, and the incident id
/// and the report location are never dropped (AC-9).
/// </summary>
public static class TeamsAdaptiveCard
{
    /// <summary>The Workflows connector rejects larger card payloads (~28 KB).</summary>
    public const int MaxBodyBytes = 28_000;

    public const string TruncationMarker = "…";

    private const string TruncatedList = "… (truncated; see the full report)";

    /// <summary>Tried in order until the serialised body fits <see cref="MaxBodyBytes"/>.</summary>
    private static readonly TeamsCardBudget[] Budgets =
    [
        new(int.MaxValue, 700),
        new(10, 400),
        new(5, 200),
        new(2, 120),
    ];

    public static string Build(IncidentNotification notification)
    {
        var body = "";
        foreach (var budget in Budgets)
        {
            body = Message(notification, budget).ToJsonString();
            if (Encoding.UTF8.GetByteCount(body) <= MaxBodyBytes)
            {
                return body;
            }
        }

        // The smallest budget is a few hundred characters per section, far below the limit.
        return body;
    }

    private static JsonObject Message(IncidentNotification notification, TeamsCardBudget budget) =>
        new()
        {
            ["type"] = "message",
            ["attachments"] = new JsonArray(new JsonObject
            {
                ["contentType"] = "application/vnd.microsoft.card.adaptive",
                ["contentUrl"] = null,
                ["content"] = Card(notification, budget),
            }),
        };

    private static JsonObject Card(IncidentNotification notification, TeamsCardBudget budget)
    {
        var blocks = new List<JsonNode?>
        {
            TextBlock(Clamp(notification.Title, budget), size: "Large", weight: "Bolder"),
            TextBlock($"Incident {notification.IncidentId} (confidence: {notification.Confidence.Value})", subtle: true),
            Header("Likely cause"),
            TextBlock(Clamp(notification.LikelyCause, budget)),
            Header("Impact"),
            TextBlock(Clamp(notification.Impact, budget)),
            Header("Evidence"),
        };
        AddList(blocks, notification.Evidence, notification.EvidenceUnavailable, budget);
        blocks.Add(Header("Similar incidents"));
        AddList(blocks, notification.SimilarIncidents, notification.SimilarIncidentsUnavailable, budget);
        blocks.Add(Header("Suggested steps"));
        AddList(blocks, notification.SuggestedSteps, whenEmpty: "None.", budget);
        blocks.Add(Header("Open questions"));
        AddList(blocks, notification.OpenQuestions, whenEmpty: "None.", budget);
        blocks.Add(TextBlock($"Full report: {notification.ReportLocation}", subtle: true));

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = new JsonArray([.. blocks]),
            ["msteams"] = new JsonObject { ["width"] = "Full" },
        };
    }

    private static void AddList(
        List<JsonNode?> blocks,
        IReadOnlyList<string> lines,
        string? whenEmpty,
        TeamsCardBudget budget)
    {
        if (lines.Count == 0)
        {
            blocks.Add(TextBlock(whenEmpty ?? "None."));
            return;
        }

        foreach (var line in lines.Take(budget.Items))
        {
            blocks.Add(TextBlock("- " + Clamp(line, budget)));
        }

        if (lines.Count > budget.Items)
        {
            blocks.Add(TextBlock(TruncatedList, subtle: true));
        }
    }

    private static string Clamp(string text, TeamsCardBudget budget) =>
        text.Length <= budget.Characters ? text : text[..budget.Characters] + TruncationMarker;

    private static JsonObject TextBlock(string text, string? size = null, string? weight = null, bool subtle = false)
    {
        var block = new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = text,
            ["wrap"] = true,
        };
        if (size is not null)
        {
            block["size"] = size;
        }

        if (weight is not null)
        {
            block["weight"] = weight;
        }

        if (subtle)
        {
            block["isSubtle"] = true;
        }

        return block;
    }

    private static JsonObject Header(string text) => TextBlock(text, weight: "Bolder");
}
