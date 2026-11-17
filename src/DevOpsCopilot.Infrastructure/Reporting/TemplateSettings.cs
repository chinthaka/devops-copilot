namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The optional paths of the custom template files (requirement D13). A blank path means the
/// embedded default template; none of these is a credential.
/// </summary>
public sealed record TemplateSettings(
    string ReportPath,
    string EmailSubjectPath,
    string EmailHtmlPath,
    string EmailTextPath)
{
    public static TemplateSettings Unconfigured { get; } = new("", "", "", "");
}
