namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// Hands one built email to the selected transport (requirement D14) and returns the message id
/// the transport gives it. Every failure except cancellation surfaces as an
/// <see cref="EmailTransportException"/> with a safe reason (D16). Infrastructure-local: it exists
/// so <see cref="EmailNotifier"/> is transport-neutral and testable without a server.
/// </summary>
public interface IEmailTransport
{
    /// <summary>False when the transport's own settings are blank; the channel is then unavailable.</summary>
    bool IsConfigured { get; }

    Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
