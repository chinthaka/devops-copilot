namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The <c>Incident:Notifications:Email</c> section. The connection string is a secret; an endpoint
/// with no connection string means managed identity. The template paths are optional overrides of
/// the embedded defaults.
/// </summary>
public sealed class EmailOptions
{
    public string ConnectionString { get; set; } = "";

    public string Endpoint { get; set; } = "";

    public string SenderAddress { get; set; } = "";

    public IList<string> Recipients { get; set; } = [];

    public string SubjectTemplatePath { get; set; } = "";

    public string HtmlTemplatePath { get; set; } = "";

    public string TextTemplatePath { get; set; } = "";
}
