namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// The three email templates: subject, HTML body and plain-text body (requirement D13). The
/// defaults are embedded in this assembly; any of them can be replaced by a configured file.
/// </summary>
public sealed record EmailTemplates(string Subject, string Html, string PlainText)
{
    private const string Prefix = "DevOpsCopilot.Infrastructure.Reporting.Templates.";

    private static readonly Lazy<EmailTemplates> Embedded = new(() => new EmailTemplates(
        Load("email-subject.txt").Trim(),
        Load("email.html"),
        Load("email.txt")));

    public static EmailTemplates Default => Embedded.Value;

    private static string Load(string name)
    {
        using var stream = typeof(EmailTemplates).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The embedded email template {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
