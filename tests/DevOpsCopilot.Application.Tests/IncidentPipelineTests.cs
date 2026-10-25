using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application.Tests;

public class IncidentPipelineTests
{
    [Fact]
    public async Task Pipeline_runs_evidence_then_grounding_then_report()
    {
        // Arrange
        var calls = new List<string>();
        var logQuery = new OrderedLogQuery(calls);
        var search = new OrderedKnowledgeSearch(calls);
        var writer = new OrderedReportWriter(calls);
        var pipeline = Pipeline(logQuery, search, writer);

        // Act
        var result = await pipeline.RunAsync(ErrorEvent("Error", "TimeoutException: slow"));

        // Assert
        Assert.Equal(["query", "query", "query", "search", "write"], calls);
        Assert.NotNull(result);
        Assert.Equal("ok", result.Diagnostic.KustoStatus);
        Assert.Equal("ok", result.Grounding.SearchStatus);
        Assert.Equal("written://draft.md", result.DraftPath);
    }

    [Fact]
    public async Task Warning_without_an_exception_does_not_run_the_team()
    {
        // Arrange
        var logQuery = new RecordingLogQuery();
        var search = new RecordingKnowledgeSearch();
        var writer = new RecordingReportWriter();
        var pipeline = Pipeline(logQuery, search, writer);

        // Act
        var result = await pipeline.RunAsync(ErrorEvent("Warning", exception: null));

        // Assert
        Assert.Null(result);
        Assert.Equal(0, logQuery.Executions);
        Assert.Equal(0, search.Executions);
        Assert.Empty(writer.Drafts);
    }

    [Fact]
    public async Task Error_without_kusto_configuration_records_three_queries_and_no_rows()
    {
        // Arrange
        var logQuery = new RecordingLogQuery { IsConfigured = false };
        var pipeline = Pipeline(logQuery, new RecordingKnowledgeSearch { IsConfigured = false }, new RecordingReportWriter());
        var logEvent = ErrorEvent("Error", "System.InvalidOperationException: failed\r\n   at Program.Main()");

        // Act
        var result = await pipeline.RunAsync(logEvent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unavailable", result.Diagnostic.KustoStatus);
        Assert.Empty(result.Diagnostic.Rows);
        Assert.Empty(result.Diagnostic.Findings);
        Assert.Equal(3, result.Diagnostic.Kql.Count);
        Assert.Equal(3, result.Diagnostic.Kql.Distinct().Count());
        Assert.Contains("4bf92f3577b34da6a3ce929d0e0e4736", result.Diagnostic.Kql[0], StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: failed", result.Diagnostic.Kql[1], StringComparison.Ordinal);
        Assert.Contains("Success == false", result.Diagnostic.Kql[2], StringComparison.Ordinal);
        Assert.Equal(0, logQuery.Executions);
    }

    [Fact]
    public async Task Missing_search_configuration_returns_no_hits()
    {
        // Arrange
        var search = new RecordingKnowledgeSearch { IsConfigured = false };
        var pipeline = Pipeline(new RecordingLogQuery { IsConfigured = false }, search, new RecordingReportWriter());

        // Act
        var result = await pipeline.RunAsync(ErrorEvent("Error", "TimeoutException: slow"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unavailable", result.Grounding.SearchStatus);
        Assert.Equal(PostmortemOverlap.IndexName, result.Grounding.Index);
        Assert.Empty(result.Grounding.Hits);
        Assert.NotEmpty(result.Grounding.Queries);
        Assert.Equal(0, search.Executions);
    }

    [Fact]
    public async Task Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service()
    {
        // Arrange
        var search = new RecordingKnowledgeSearch { IsConfigured = true, Hits = CreateHits() };
        var pipeline = Pipeline(new RecordingLogQuery { IsConfigured = false }, search, new RecordingReportWriter());

        // Act
        var result = await pipeline.RunAsync(ErrorEvent("Critical", "TimeoutException: checkout failed"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ok", result.Grounding.SearchStatus);
        Assert.Equal(5, result.Grounding.Hits.Count);
        Assert.DoesNotContain(result.Grounding.Hits, hit => hit.Id == "unrelated-billing");
        Assert.Equal(1, search.Executions);
    }

    [Fact]
    public async Task Kql_that_contains_drop_is_not_executed()
    {
        // Arrange
        var logQuery = new RecordingLogQuery { IsConfigured = true };
        var evidence = new GatherEvidence(logQuery, LogQueryTargets.Unconfigured);
        var kql = "StormEvents | .drop table StormEvents";

        // Act
        var call = await evidence.ExecuteAsync(kql, CancellationToken.None);

        // Assert
        Assert.False(call.Executed);
        Assert.Equal(0, logQuery.Executions);
        Assert.Empty(call.Rows);
    }

    private static RunIncidentPipeline Pipeline(ILogQuery logQuery, IKnowledgeSearch search, IReportWriter writer) =>
        new(
            new GatherEvidence(logQuery, LogQueryTargets.Unconfigured),
            new GroundInHistory(search),
            writer);

    private static LogEvent ErrorEvent(string level, string? exception)
    {
        return new LogEvent(
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            level,
            "Checkout.Api",
            "checkout failed",
            exception,
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7");
    }

    private static IReadOnlyList<SearchHit> CreateHits()
    {
        var hits = new List<SearchHit>();
        for (var index = 1; index <= 6; index++)
        {
            hits.Add(new SearchHit(
                "hit-" + index,
                "TimeoutException on checkout",
                "TimeoutException during Checkout.Api payment.",
                1.0 / index));
        }

        hits.Add(new SearchHit(
            "unrelated-billing",
            "Billing ledger delay",
            "The billing ledger lagged behind invoices.",
            0.2));
        return hits;
    }

    private sealed class OrderedLogQuery(List<string> calls) : ILogQuery
    {
        public bool IsConfigured => true;

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken)
        {
            calls.Add("query");
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows =
                [new Dictionary<string, string> { ["ServiceName"] = "Checkout.Api" }];
            return Task.FromResult(rows);
        }
    }

    private sealed class OrderedKnowledgeSearch(List<string> calls) : IKnowledgeSearch
    {
        public bool IsConfigured => true;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            calls.Add("search");
            IReadOnlyList<SearchHit> hits =
                [new SearchHit("hit-1", "TimeoutException on checkout", "Checkout.Api timed out.", 1.0)];
            return Task.FromResult(hits);
        }
    }

    private sealed class OrderedReportWriter(List<string> calls) : IReportWriter
    {
        public Task<string> WriteAsync(
            DiagnosticHandoff diagnostic,
            GroundingHandoff grounding,
            string draft,
            CancellationToken cancellationToken)
        {
            calls.Add("write");
            return Task.FromResult("written://draft.md");
        }
    }
}
