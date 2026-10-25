using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Infrastructure.Tests;

public class AzureOpenAIDiagnosisModelTests
{
    private const string WellFormed = """
        {
          "title": "Checkout times out on the payment gateway",
          "likelyCause": "The payment gateway stopped answering within the timeout.",
          "confidence": "medium",
          "impact": { "services": ["Checkout.Api"], "resources": [], "eventCount": 1, "window": "2026-10-19T12:00:00Z" },
          "evidence": [ { "kql": 0, "row": 0 } ],
          "similarIncidents": ["h1"],
          "suggestedSteps": ["Check the payment gateway's health."],
          "openQuestions": ["Was the gateway deployed in this window?"]
        }
        """;

    [Fact]
    public void Without_a_chat_client_the_model_is_not_configured()
    {
        // Arrange
        var model = new AzureOpenAIDiagnosisModel(chatClient: null);

        // Act
        var configured = model.IsConfigured;

        // Assert
        Assert.False(configured);
    }

    [Fact]
    public void With_a_chat_client_the_model_is_configured()
    {
        // Arrange
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(WellFormed));

        // Act
        var configured = model.IsConfigured;

        // Assert
        Assert.True(configured);
    }

    [Fact]
    public async Task An_unconfigured_model_proposes_nothing_without_calling_anything()
    {
        // Arrange
        var model = new AzureOpenAIDiagnosisModel(chatClient: null);

        // Act
        var diagnosis = await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Null(diagnosis);
    }

    [Fact]
    public async Task A_well_formed_answer_is_mapped_to_the_domain_diagnosis()
    {
        // Arrange
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(WellFormed));
        var diagnostic = Diagnostic();

        // Act
        var diagnosis = await model.ProposeAsync(diagnostic, Grounding(), CancellationToken.None);

        // Assert
        Assert.NotNull(diagnosis);
        Assert.Equal(diagnostic.EventId, diagnosis.IncidentId);
        Assert.Equal("Checkout times out on the payment gateway", diagnosis.Title);
        Assert.Equal("The payment gateway stopped answering within the timeout.", diagnosis.LikelyCause);
        Assert.Equal(Confidence.Medium, diagnosis.Confidence);
        Assert.Equal(["Checkout.Api"], diagnosis.Impact.Services);
        Assert.Equal(1, diagnosis.Impact.EventCount);
        Assert.Equal([new EvidenceCitation(0, 0)], diagnosis.Evidence);
        Assert.Equal(["h1"], diagnosis.SimilarIncidents);
        Assert.Equal(["Check the payment gateway's health."], diagnosis.SuggestedSteps);
        Assert.Equal(["Was the gateway deployed in this window?"], diagnosis.OpenQuestions);
    }

    [Fact]
    public async Task The_incident_id_always_comes_from_the_handoff_not_the_model()
    {
        // Arrange
        var answer = WellFormed.Replace("\"title\":", "\"incidentId\": \"evt-99999999999999999\", \"title\":", StringComparison.Ordinal);
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(answer));
        var diagnostic = Diagnostic();

        // Act
        var diagnosis = await model.ProposeAsync(diagnostic, Grounding(), CancellationToken.None);

        // Assert
        Assert.NotNull(diagnosis);
        Assert.Equal(diagnostic.EventId, diagnosis.IncidentId);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_proposes_nothing()
    {
        // Arrange
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient("I think the gateway is down."));

        // Act
        var diagnosis = await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Null(diagnosis);
    }

    [Fact]
    public async Task An_answer_with_a_confidence_outside_the_contract_proposes_nothing()
    {
        // Arrange
        var answer = WellFormed.Replace("\"medium\"", "\"certain\"", StringComparison.Ordinal);
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(answer));

        // Act
        var diagnosis = await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Null(diagnosis);
    }

    [Fact]
    public async Task An_answer_without_a_likely_cause_proposes_nothing()
    {
        // Arrange
        var answer = WellFormed.Replace("\"The payment gateway stopped answering within the timeout.\"", "\"\"", StringComparison.Ordinal);
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(answer));

        // Act
        var diagnosis = await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Null(diagnosis);
    }

    [Fact]
    public async Task Missing_lists_are_mapped_to_empty_lists_never_null()
    {
        // Arrange
        var answer = """
            {
              "title": "Checkout times out",
              "likelyCause": "The payment gateway stopped answering.",
              "confidence": "low"
            }
            """;
        var model = new AzureOpenAIDiagnosisModel(new FakeChatClient(answer));

        // Act
        var diagnosis = await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.NotNull(diagnosis);
        Assert.Empty(diagnosis.Evidence);
        Assert.Empty(diagnosis.SimilarIncidents);
        Assert.Empty(diagnosis.SuggestedSteps);
        Assert.Empty(diagnosis.OpenQuestions);
        Assert.Empty(diagnosis.Impact.Resources);
        Assert.Empty(diagnosis.Impact.Services);
    }

    [Fact]
    public async Task The_prompt_lists_the_evidence_by_index_and_forbids_infrastructure_code()
    {
        // Arrange
        var client = new FakeChatClient(WellFormed);
        var model = new AzureOpenAIDiagnosisModel(client);

        // Act
        await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Contains("kql 0", client.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("row 0", client.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("query-one", client.Prompt, StringComparison.Ordinal);
        Assert.Contains("ServiceName=Checkout.Api", client.Prompt, StringComparison.Ordinal);
        Assert.Contains("h1", client.Prompt, StringComparison.Ordinal);
        Assert.Contains("Bicep", client.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Terraform", client.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cite", client.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_prompt_says_a_tool_was_unavailable_rather_than_inventing_evidence()
    {
        // Arrange
        var client = new FakeChatClient(WellFormed);
        var model = new AzureOpenAIDiagnosisModel(client);
        var diagnostic = Diagnostic("unavailable", findings: []);

        // Act
        await model.ProposeAsync(diagnostic, Grounding("unavailable"), CancellationToken.None);

        // Assert
        Assert.Contains("unavailable", client.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ServiceName=Checkout.Api", client.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_model_is_asked_exactly_once_per_event()
    {
        // Arrange
        var client = new FakeChatClient(WellFormed);
        var model = new AzureOpenAIDiagnosisModel(client);

        // Act
        await model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task A_cancelled_call_propagates_so_the_stage_can_decide()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var client = new FakeChatClient(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return WellFormed;
        });
        var model = new AzureOpenAIDiagnosisModel(client);

        // Act
        var act = () => model.ProposeAsync(Diagnostic(), Grounding(), cancelled.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task A_failing_transport_propagates_so_the_stage_can_decide()
    {
        // Arrange
        var client = new FakeChatClient((_, _) => throw new HttpRequestException("the endpoint refused the call"));
        var model = new AzureOpenAIDiagnosisModel(client);

        // Act
        var act = () => model.ProposeAsync(Diagnostic(), Grounding(), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    private static DiagnosticHandoff Diagnostic(string kustoStatus = "ok", IReadOnlyList<string>? findings = null)
    {
        findings ??= ["Query `query-one` returned ServiceName=Checkout.Api."];
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = findings.Count == 0
            ? []
            : [new Dictionary<string, string> { ["ServiceName"] = "Checkout.Api" }];
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            "System.TimeoutException: checkout timed out",
            kustoStatus,
            ["query-one"],
            rows,
            findings);
    }

    private static GroundingHandoff Grounding(string searchStatus = "ok")
    {
        SearchHit[] hits = searchStatus == "ok"
            ? [new SearchHit("h1", "TimeoutException on checkout", "Checkout.Api timed out during payment.", 0.9)]
            : [];
        return new GroundingHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)),
            searchStatus,
            PostmortemOverlap.IndexName,
            ["Checkout.Api"],
            hits);
    }
}
