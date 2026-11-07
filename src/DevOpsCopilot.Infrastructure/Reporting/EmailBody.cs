using DevOpsCopilot.Domain.Reporting;
using System.Net;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Renders one notification through the three email templates with the Domain template renderer
/// (requirement D12). Every value comes from the notification; a template only places it. Values
/// are HTML-encoded in the HTML body, because diagnosis text comes from logs and is never trusted
/// as markup, and line breaks are removed from the subject so no value can add a mail header.
/// </summary>
public static class EmailBody
{
    public static EmailContent Build(IncidentNotification notification, EmailTemplates templates)
    {
        var subject = TemplateRenderer.Render(Normalise(templates.Subject), TextValues(notification));
        var html = TemplateRenderer.Render(Normalise(templates.Html), HtmlValues(notification));
        var text = TemplateRenderer.Render(Normalise(templates.PlainText), TextValues(notification));
        return new EmailContent(OneLine(subject), html, text);
    }

    private static Dictionary<string, TemplateValue> TextValues(IncidentNotification notification) =>
        new(StringComparer.Ordinal)
        {
            ["IncidentId"] = TemplateValue.Of(notification.IncidentId),
            ["Title"] = TemplateValue.Of(notification.Title),
            ["LikelyCause"] = TemplateValue.Of(notification.LikelyCause),
            ["Confidence"] = TemplateValue.Of(notification.Confidence.Value),
            ["Impact"] = TemplateValue.Of(notification.Impact),
            ["Evidence"] = TemplateValue.List(notification.Evidence, notification.EvidenceUnavailable ?? "None."),
            ["SimilarIncidents"] = TemplateValue.List(notification.SimilarIncidents, notification.SimilarIncidentsUnavailable ?? "None."),
            ["SuggestedSteps"] = TemplateValue.List(notification.SuggestedSteps, "None."),
            ["OpenQuestions"] = TemplateValue.List(notification.OpenQuestions, "None."),
            ["ReportLocation"] = TemplateValue.Of(notification.ReportLocation),
        };

    private static Dictionary<string, TemplateValue> HtmlValues(IncidentNotification notification) =>
        new(StringComparer.Ordinal)
        {
            ["IncidentId"] = Encoded(notification.IncidentId),
            ["Title"] = Encoded(notification.Title),
            ["LikelyCause"] = Encoded(notification.LikelyCause),
            ["Confidence"] = Encoded(notification.Confidence.Value),
            ["Impact"] = Encoded(notification.Impact),
            ["Evidence"] = HtmlList(notification.Evidence, notification.EvidenceUnavailable ?? "None."),
            ["SimilarIncidents"] = HtmlList(notification.SimilarIncidents, notification.SimilarIncidentsUnavailable ?? "None."),
            ["SuggestedSteps"] = HtmlList(notification.SuggestedSteps, "None."),
            ["OpenQuestions"] = HtmlList(notification.OpenQuestions, "None."),
            ["ReportLocation"] = Encoded(notification.ReportLocation),
        };

    private static TemplateValue Encoded(string text) => TemplateValue.Of(WebUtility.HtmlEncode(text));

    private static TemplateValue HtmlList(IReadOnlyList<string> lines, string whenEmpty) =>
        TemplateValue.Of(lines.Count == 0
            ? $"<p>{WebUtility.HtmlEncode(whenEmpty)}</p>"
            : "<ul>" + string.Concat(lines.Select(line => $"<li>{WebUtility.HtmlEncode(line)}</li>")) + "</ul>");

    private static string OneLine(string subject) =>
        string.Join(' ', subject.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Normalise(string template) =>
        template.Replace("\r\n", "\n", StringComparison.Ordinal);
}
