using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using System.Globalization;

namespace DevOpsCopilot.Domain.Reporting;

/// <summary>
/// Renders one diagnosis through a report template. The embedded default template is the seven
/// sections of pipeline.md § 7, in order: header, impact, likely cause with confidence, evidence,
/// similar incidents, suggested steps and open questions. A custom template may reword or reorder
/// them; every value still comes from the diagnosis and the handoffs. An unavailable tool is
/// stated in its section and nothing is listed for it.
/// </summary>
public static class IncidentReport
{
    /// <summary>What the evidence section says when KQL returned nothing usable.</summary>
    public const string KqlUnavailable = "KQL was unavailable. No rows were returned.";

    /// <summary>What the similar-incidents section says when search returned nothing usable.</summary>
    public const string SearchUnavailable = "Azure AI Search was unavailable. No historical post-mortems were returned.";

    private const string DefaultTemplateResource = "DevOpsCopilot.Domain.Reporting.Templates.report.md";

    private static readonly Lazy<string> Default = new(LoadDefaultTemplate);

    /// <summary>The embedded seven-section template, with <c>\n</c> line endings.</summary>
    public static string DefaultTemplate => Default.Value;

    /// <summary>Renders with <paramref name="template"/>, or with the default when it is null or blank.</summary>
    public static string Render(
        Diagnosis diagnosis,
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string? template = null)
    {
        var source = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var rendered = TemplateRenderer.Render(Normalise(source), Values(diagnosis, diagnostic, grounding));
        return rendered.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    /// <summary>The placeholders a report template can use, by name.</summary>
    public static IReadOnlyDictionary<string, TemplateValue> Values(
        Diagnosis diagnosis,
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding)
    {
        var evidence = diagnostic.KustoStatus == "ok" ? diagnostic.Findings : [];
        var similar = grounding.SearchStatus == "ok"
            ? grounding.Hits.Select(hit => $"{hit.Title}: \"{hit.Excerpt}\"").ToArray()
            : [];

        return new Dictionary<string, TemplateValue>(StringComparer.Ordinal)
        {
            ["Title"] = TemplateValue.Of(diagnosis.Title),
            ["IncidentId"] = TemplateValue.Of(diagnosis.IncidentId),
            ["TraceId"] = TemplateValue.Of(string.IsNullOrWhiteSpace(diagnostic.TraceId) ? "(missing)" : diagnostic.TraceId),
            ["Services"] = TemplateValue.Of(NamesOrNone(diagnosis.Impact.Services)),
            ["Resources"] = TemplateValue.Of(NamesOrNone(diagnosis.Impact.Resources)),
            ["EventCount"] = TemplateValue.Of(diagnosis.Impact.EventCount.ToString(CultureInfo.InvariantCulture)),
            ["Window"] = TemplateValue.Of(diagnosis.Impact.Window),
            ["LikelyCause"] = TemplateValue.Of(diagnosis.LikelyCause),
            ["Confidence"] = TemplateValue.Of(diagnosis.Confidence.Value),
            ["Evidence"] = TemplateValue.List(evidence, KqlUnavailable),
            ["SimilarIncidents"] = TemplateValue.List(similar, SearchUnavailable),
            ["SuggestedSteps"] = TemplateValue.List(diagnosis.SuggestedSteps, "- None."),
            ["OpenQuestions"] = TemplateValue.List(diagnosis.OpenQuestions, "- None."),
        };
    }

    private static string NamesOrNone(IReadOnlyList<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);

    private static string Normalise(string template) =>
        template.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string LoadDefaultTemplate()
    {
        using var stream = typeof(IncidentReport).Assembly.GetManifestResourceStream(DefaultTemplateResource)
            ?? throw new InvalidOperationException($"The embedded report template {DefaultTemplateResource} is missing.");
        using var reader = new StreamReader(stream);
        return Normalise(reader.ReadToEnd());
    }
}
