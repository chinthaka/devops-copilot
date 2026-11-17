namespace DevOpsCopilot.Application.Tests.Common;

/// <summary>
/// The timeouts the stages take in tests. Production values come from configuration; these only
/// need to be long enough that a well-behaved fake never hits them.
/// </summary>
internal static class TestTimeouts
{
    public static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);
}
