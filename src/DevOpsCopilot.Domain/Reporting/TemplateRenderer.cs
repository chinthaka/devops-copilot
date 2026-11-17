using System.Text.RegularExpressions;

namespace DevOpsCopilot.Domain.Reporting;

/// <summary>
/// Fills <c>{{Placeholder}}</c> tokens from named values in a single pass, so a value that itself
/// contains braces is never expanded again. A token that names nothing is left in the output as
/// written, so a typo in a custom template is seen rather than silently dropped. A template can
/// only rearrange the values it is given; it cannot add a fact.
/// </summary>
public static partial class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, TemplateValue> values)
    {
        return Placeholder().Replace(
            template,
            match => values.TryGetValue(match.Groups["name"].Value, out var value) ? value.Text : match.Value);
    }

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
