using DevOpsCopilot.Domain.Reporting;
using System.Text;
using System.Text.Json.Nodes;

namespace DevOpsCopilot.Infrastructure.Reporting.Slack;

/// <summary>
/// Maps one notification to the chat.postMessage payload: a header block, a section per labelled
/// part of the report, and a plain <c>text</c> fallback for notification previews. Every line of
/// text comes from the notification (requirement D1); only the section labels are this builder's
/// own. Each block stays within Slack's limits — 150 characters for the header, 3 000 per section,
/// 50 blocks — every cut is marked visibly, and the incident id and the report location are never
/// dropped (AC-9). The notifier adds the channel id; nothing here sees a credential.
/// </summary>
public static class SlackBlocks
{
    public const int MaxBlocks = 50;
    public const int MaxTextLength = 3_000;
    public const int MaxHeaderLength = 150;

    public const string TruncationMarker = "…";

    private const string TruncatedList = "… (truncated; see the full report)";

    public static string Build(IncidentNotification notification)
    {
        var blocks = new List<JsonNode?>
        {
            Header(Clamp(notification.Title, MaxHeaderLength)),
            Section($"Incident {notification.IncidentId} (confidence: {notification.Confidence.Value})"),
            Section(Labelled("Likely cause", notification.LikelyCause)),
            Section(Labelled("Impact", notification.Impact)),
            Section(ListText("Evidence", notification.Evidence, notification.EvidenceUnavailable)),
            Section(ListText("Similar incidents", notification.SimilarIncidents, notification.SimilarIncidentsUnavailable)),
            Section(ListText("Suggested steps", notification.SuggestedSteps, whenEmpty: "None.")),
            Section(ListText("Open questions", notification.OpenQuestions, whenEmpty: "None.")),
            Section($"Full report: {notification.ReportLocation}"),
        };

        var fallback = $"Incident {notification.IncidentId}: {notification.Title} " +
            $"(confidence: {notification.Confidence.Value}). Full report: {notification.ReportLocation}";
        return new JsonObject
        {
            ["text"] = Clamp(fallback, MaxTextLength),
            ["blocks"] = new JsonArray([.. blocks]),
        }.ToJsonString();
    }

    private static string Labelled(string label, string text) =>
        Clamp($"*{label}*\n{text}", MaxTextLength);

    private static string ListText(string label, IReadOnlyList<string> lines, string? whenEmpty)
    {
        var builder = new StringBuilder("*").Append(label).Append('*');
        if (lines.Count == 0)
        {
            return builder.Append('\n').Append(whenEmpty ?? "None.").ToString();
        }

        // Room is reserved for the marker, so a cut list still states that it was cut.
        var budget = MaxTextLength - TruncatedList.Length - 1;
        var truncated = false;
        foreach (var line in lines)
        {
            var entry = "\n- " + line;
            if (builder.Length + entry.Length > budget)
            {
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

    private static string Clamp(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - TruncationMarker.Length), TruncationMarker);

    private static JsonObject Header(string text) =>
        new()
        {
            ["type"] = "header",
            ["text"] = new JsonObject { ["type"] = "plain_text", ["text"] = text },
        };

    private static JsonObject Section(string text) =>
        new()
        {
            ["type"] = "section",
            ["text"] = new JsonObject { ["type"] = "mrkdwn", ["text"] = text },
        };
}
