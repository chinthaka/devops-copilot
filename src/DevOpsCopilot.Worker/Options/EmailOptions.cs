namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The <c>Incident:Notifications:Email</c> section. <see cref="Transport"/> selects how the email
/// leaves the worker, <c>CommunicationServices</c> or <c>Smtp</c>, with no default in code. The
/// sender and recipients apply to either. The connection string is a secret; an endpoint with no
/// connection string means managed identity. The SMTP server is the <see cref="Smtp"/> section.
/// The template paths are optional overrides of the embedded defaults.
/// </summary>
public sealed class EmailOptions
{
    public string Transport { get; set; } = "";

    public SmtpOptions Smtp { get; set; } = new();

    public string ConnectionString { get; set; } = "";

    public string Endpoint { get; set; } = "";

    public string SenderAddress { get; set; } = "";

    public IList<string> Recipients { get; set; } = [];

    public string SubjectTemplatePath { get; set; } = "";

    public string HtmlTemplatePath { get; set; } = "";

    public string TextTemplatePath { get; set; } = "";
}
