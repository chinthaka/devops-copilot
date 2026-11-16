namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>One rendered email: its subject, HTML body and plain-text alternative.</summary>
public sealed record EmailContent(string Subject, string Html, string PlainText);
