namespace DevOpsCopilot.Worker.Options;

/// <summary>The <c>Incident:Report</c> section: the optional custom report template path.</summary>
public sealed class ReportOptions
{
    public string TemplatePath { get; set; } = "";
}
