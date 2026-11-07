namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Registered when the email channel is unconfigured, so the service graph still resolves without
/// a Communication Services resource. <see cref="EmailNotifier"/> answers unavailable before any
/// send, so this transport is never called; a call is therefore a wiring bug and throws.
/// </summary>
public sealed class UnconfiguredEmailTransport : IEmailTransport
{
    public Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "The email channel is not configured, so nothing may send through it.");
}
