using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Domain.Tests;

public class IncidentDraftTests
{
    [Fact]
    public void Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named()
    {
        // Arrange
        var diagnostic = UnavailableDiagnostic("System.IO.IOException: disk");
        var grounding = UnavailableGrounding(diagnostic.EventId);
        string[] headings =
        [
            "# Incident draft:",
            "## Diagnosis",
            "## Similar incidents",
            "## Slack",
            "## Teams",
            "## Bicep",
            "## Open questions",
        ];

        // Act
        var draft = IncidentDraft.Render(diagnostic, grounding);

        // Assert
        var position = -1;
        foreach (var heading in headings)
        {
            var next = draft.IndexOf(heading, position + 1, StringComparison.Ordinal);
            Assert.True(next > position, heading);
            position = next;
        }

        Assert.DoesNotContain("```bicep", draft, StringComparison.Ordinal);
        Assert.Contains("No finding names a resource change.", draft, StringComparison.Ordinal);
    }

    private static DiagnosticHandoff UnavailableDiagnostic(string exception)
    {
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            exception,
            "unavailable",
            ["ExceptionsTable | where TraceId == \"\""],
            [],
            []);
    }

    private static GroundingHandoff UnavailableGrounding(string eventId)
    {
        return new GroundingHandoff(eventId, "unavailable", PostmortemOverlap.IndexName, ["Checkout.Api"], []);
    }
}
