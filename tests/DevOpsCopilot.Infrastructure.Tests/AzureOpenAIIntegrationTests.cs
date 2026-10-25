using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Infrastructure.Tests;

/// <summary>
/// The one test that talks to a real Azure OpenAI deployment. Configuration comes from user
/// secrets or environment variables and never from the repository; without it the test is skipped,
/// not failed, so CI stays green with no secrets. Run it locally with:
///
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:AzureOpenAI:Endpoint" "https://&lt;resource&gt;.openai.azure.com/"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:AzureOpenAI:Deployment" "&lt;deployment-name&gt;"
///   dotnet user-secrets --project tests/DevOpsCopilot.Infrastructure.Tests \
///     set "Incident:AzureOpenAI:ApiKey" "&lt;key&gt;"        # omit to use your Azure login
/// </summary>
[Trait("Category", "RequiresAzureOpenAI")]
public class AzureOpenAIIntegrationTests
{
    [AzureOpenAIRequiredFact]
    public async Task AzureOpenAI_ReturnsABindableAndCitedDiagnosis()
    {
        // Arrange
        var settings = AzureOpenAIConfiguration.Settings!;
        using var chatClient = AzureOpenAIChatClient.TryCreate(settings);
        var model = new AzureOpenAIDiagnosisModel(chatClient);
        var diagnostic = Diagnostic();
        var grounding = Grounding();
        using var timeout = new CancellationTokenSource(settings.Timeout);

        // Act
        var diagnosis = await model.ProposeAsync(diagnostic, grounding, timeout.Token);

        // Assert
        Assert.NotNull(diagnosis);
        Assert.Equal(diagnostic.EventId, diagnosis.IncidentId);
        Assert.False(string.IsNullOrWhiteSpace(diagnosis.LikelyCause));
        Assert.True(DiagnosisCitations.Validate(diagnosis, diagnostic, grounding).IsValid);
    }

    private static DiagnosticHandoff Diagnostic() =>
        new(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            "System.TimeoutException: the payment gateway did not respond within 30s",
            "ok",
            ["ExceptionsTable | where TraceId == \"4bf92f3577b34da6a3ce929d0e0e4736\"", "DependenciesTable | where Success == false"],
            [
                new Dictionary<string, string> { ["ServiceName"] = "Checkout.Api", ["ExceptionType"] = "System.TimeoutException" },
                new Dictionary<string, string> { ["Target"] = "payments.contoso.com", ["DurationMs"] = "30001" },
            ],
            [
                "Query 0 returned ServiceName=Checkout.Api, ExceptionType=System.TimeoutException.",
                "Query 1 returned Target=payments.contoso.com, DurationMs=30001.",
            ]);

    private static GroundingHandoff Grounding() =>
        new(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "ok",
            PostmortemOverlap.IndexName,
            ["Checkout.Api TimeoutException"],
            [
                new SearchHit("pm-204", "Payment gateway timeouts", "The gateway's connection pool was exhausted during a traffic peak.", 0.91),
                new SearchHit("pm-311", "Checkout latency", "Checkout.Api timed out when the downstream payment service was slow.", 0.78),
            ]);
}
