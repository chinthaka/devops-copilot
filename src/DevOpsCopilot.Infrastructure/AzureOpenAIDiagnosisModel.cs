using System.Text.Json.Serialization;
using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Asks the configured Azure OpenAI deployment for a diagnosis as structured output and maps it to
/// the domain type. It decides nothing: an answer that will not bind, or that breaks the contract's
/// shape, comes back as null and the Application stage chooses the fallback. The model's own
/// incident id is ignored, because the pipeline already knows which event this is.
/// </summary>
public sealed class AzureOpenAIDiagnosisModel : IDiagnosisModel
{
    private readonly IChatClient? _chatClient;

    public AzureOpenAIDiagnosisModel(IChatClient? chatClient)
    {
        _chatClient = chatClient;
    }

    public bool IsConfigured => _chatClient is not null;

    public async Task<Diagnosis?> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken)
    {
        if (_chatClient is null)
        {
            return null;
        }

        List<ChatMessage> messages =
        [
            new(ChatRole.System, DiagnosisPrompt.System),
            new(ChatRole.User, DiagnosisPrompt.User(diagnostic, grounding)),
        ];

        var response = await _chatClient.GetResponseAsync<DiagnosisAnswer>(
            messages,
            cancellationToken: cancellationToken);

        return response.TryGetResult(out var answer) ? Map(answer, diagnostic) : null;
    }

    private static Diagnosis? Map(DiagnosisAnswer answer, DiagnosticHandoff diagnostic)
    {
        if (string.IsNullOrWhiteSpace(answer.Title) || string.IsNullOrWhiteSpace(answer.LikelyCause))
        {
            return null;
        }

        Confidence confidence;
        try
        {
            confidence = Confidence.Create(answer.Confidence ?? "");
        }
        catch (ArgumentException)
        {
            // A confidence outside the contract is an answer we cannot trust at all.
            return null;
        }

        var impact = new Impact(
            answer.Impact?.Services ?? [],
            answer.Impact?.Resources ?? [],
            answer.Impact?.EventCount is > 0 ? answer.Impact.EventCount : 1,
            answer.Impact?.Window ?? "");

        return new Diagnosis(
            diagnostic.EventId,
            answer.Title,
            answer.LikelyCause,
            confidence,
            impact,
            answer.Evidence?.Select(citation => new EvidenceCitation(citation.Kql, citation.Row)).ToArray() ?? [],
            answer.SimilarIncidents ?? [],
            answer.SuggestedSteps ?? [],
            answer.OpenQuestions ?? []);
    }

    /// <summary>The model's answer shape. It stays in this layer and never reaches Application.</summary>
    internal sealed class DiagnosisAnswer
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("likelyCause")]
        public string? LikelyCause { get; set; }

        [JsonPropertyName("confidence")]
        public string? Confidence { get; set; }

        [JsonPropertyName("impact")]
        public ImpactAnswer? Impact { get; set; }

        [JsonPropertyName("evidence")]
        public CitationAnswer[]? Evidence { get; set; }

        [JsonPropertyName("similarIncidents")]
        public string[]? SimilarIncidents { get; set; }

        [JsonPropertyName("suggestedSteps")]
        public string[]? SuggestedSteps { get; set; }

        [JsonPropertyName("openQuestions")]
        public string[]? OpenQuestions { get; set; }
    }

    internal sealed class ImpactAnswer
    {
        [JsonPropertyName("services")]
        public string[]? Services { get; set; }

        [JsonPropertyName("resources")]
        public string[]? Resources { get; set; }

        [JsonPropertyName("eventCount")]
        public int EventCount { get; set; }

        [JsonPropertyName("window")]
        public string? Window { get; set; }
    }

    internal sealed class CitationAnswer
    {
        [JsonPropertyName("kql")]
        public int Kql { get; set; }

        [JsonPropertyName("row")]
        public int Row { get; set; }
    }
}
