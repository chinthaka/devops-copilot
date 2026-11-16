namespace DevOpsCopilot.Domain.Reporting;

/// <summary>
/// One value a template placeholder can take: plain text, or a list rendered as one <c>- </c>
/// line per entry, with a stated sentence in place of an empty list.
/// </summary>
public sealed record TemplateValue
{
    private TemplateValue(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public static TemplateValue Of(string text) => new(text);

    public static TemplateValue List(IReadOnlyList<string> lines, string whenEmpty) =>
        new(lines.Count == 0 ? whenEmpty : string.Join("\n", lines.Select(line => "- " + line)));
}
