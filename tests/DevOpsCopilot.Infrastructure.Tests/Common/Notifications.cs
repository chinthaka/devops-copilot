using DevOpsCopilot.Domain.Diagnoses;
using DevOpsCopilot.Domain.Reporting;

namespace DevOpsCopilot.Infrastructure.Tests.Common;

/// <summary>Builds the notifications the channel tests share.</summary>
internal static class Notifications
{
    public static IncidentNotification Complete() =>
        new(
            "20261019T120000Z",
            "Checkout times out on the payment gateway",
            "The payment gateway stopped answering.",
            Confidence.Medium,
            "Services: Checkout.Api. Resources: (none). Event count: 3. Window: 2026-10-19T12:00:00.0000000+00:00.",
            ["Query `q` returned ServiceName=Checkout.Api."],
            null,
            ["TimeoutException on checkout: \"Checkout.Api timed out during payment.\""],
            null,
            ["Check the payment gateway's health."],
            ["Was the gateway deployed in this window?"],
            "incidents/2026-10-19-20261019T120000Z.md");

    /// <summary>Far beyond every channel's payload limit, to force visible truncation (AC-9).</summary>
    public static IncidentNotification Oversized()
    {
        var line = "The checkout call chain stalled while waiting for the payment gateway to answer. ";
        return Complete() with
        {
            LikelyCause = string.Concat(Enumerable.Repeat(line, 200)),
            Evidence = Enumerable.Range(0, 300)
                .Select(index => $"Query {index} returned " + string.Concat(Enumerable.Repeat(line, 5)))
                .ToArray(),
            SimilarIncidents = Enumerable.Range(0, 100)
                .Select(index => $"Post-mortem {index}: " + string.Concat(Enumerable.Repeat(line, 5)))
                .ToArray(),
            SuggestedSteps = Enumerable.Range(0, 100)
                .Select(index => $"Step {index}: " + string.Concat(Enumerable.Repeat(line, 5)))
                .ToArray(),
            OpenQuestions = Enumerable.Range(0, 100)
                .Select(index => $"Question {index}: " + string.Concat(Enumerable.Repeat(line, 5)))
                .ToArray(),
        };
    }
}
