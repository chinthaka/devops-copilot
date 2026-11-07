namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Hands one built email to Azure Communication Services and returns the send operation's id.
/// Infrastructure-local: it exists only so <see cref="EmailNotifier"/> is testable without a
/// resource, and never leaves this layer.
/// </summary>
public interface IEmailTransport
{
    Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
