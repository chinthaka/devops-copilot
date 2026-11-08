namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Stands in when the selected transport has no settings, so the email channel reports itself
/// unavailable. Sending through it is a programming error, because the notifier checks first.
/// </summary>
public sealed class UnconfiguredEmailTransport : IEmailTransport
{
    public bool IsConfigured => false;

    public Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "The email channel is not configured, so nothing may send through it.");
}
