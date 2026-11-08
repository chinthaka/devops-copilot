namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>One built email, addressed and ready for the transport.</summary>
public sealed record OutgoingEmail(
    string Sender,
    IReadOnlyList<string> To,
    string Subject,
    string Html,
    string PlainText);
