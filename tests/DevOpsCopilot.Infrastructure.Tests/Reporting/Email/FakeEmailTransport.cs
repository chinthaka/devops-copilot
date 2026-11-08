using DevOpsCopilot.Infrastructure.Reporting.Email;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// Records each outgoing email and answers with an operation id, or throws on request. Configured
/// unless told otherwise, so a test can prove the notifier respects an unconfigured transport.
/// </summary>
internal sealed class FakeEmailTransport : IEmailTransport
{
    public List<OutgoingEmail> Sent { get; } = [];

    public Exception? Throws { get; init; }

    public bool IsConfigured { get; init; } = true;

    public Task<string> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sent.Add(email);
        if (Throws is not null)
        {
            throw Throws;
        }

        return Task.FromResult("operation-1");
    }
}
