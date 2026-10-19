using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class ConfiguredPostmortemSearch : IPostmortemSearch
{
    private readonly IncidentOptions _options;

    public ConfiguredPostmortemSearch(IOptions<IncidentOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured =>
        Uri.TryCreate(_options.Search.Endpoint, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(_options.Search.ApiKey);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Azure AI Search is not configured.");
        }

        var client = new SearchClient(
            new Uri(_options.Search.Endpoint),
            PostmortemSearchTool.IndexName,
            new AzureKeyCredential(_options.Search.ApiKey));
        var response = await client.SearchAsync<SearchDocument>(
            query,
            new Azure.Search.Documents.SearchOptions { Size = 10 },
            cancellationToken);

        var hits = new List<SearchHit>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            hits.Add(new SearchHit(
                Read(result.Document, "id"),
                Read(result.Document, "title"),
                FirstNonEmpty(Read(result.Document, "excerpt"), Read(result.Document, "content")),
                result.Score ?? 0));
        }

        return hits;
    }

    private static string Read(SearchDocument document, string name)
    {
        return document.TryGetValue(name, out var value) ? value?.ToString() ?? "" : "";
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
