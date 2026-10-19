using DevOpsCopilot.Daemon.Incident;

namespace DevOpsCopilot.Daemon.Tests;

public class IncidentWorkflowTests
{
	[Fact]
	public async Task Warning_without_an_exception_does_not_run_the_team()
	{
		// Arrange
		var kusto = new RecordingKustoRunner();
		var search = new RecordingSearch();
		using var drafts = new DraftDirectory();
		var workflow = CreateWorkflow(kusto, search, drafts.Path);
		var logEvent = ErrorEvent(level: "Warning", exception: null);

		// Act
		var result = await workflow.RunAsync(logEvent);

		// Assert
		Assert.Null(result);
		Assert.Equal(0, kusto.Executions);
		Assert.Equal(0, search.Executions);
		Assert.Empty(Directory.GetFiles(drafts.Path));
	}

	[Fact]
	public async Task Error_without_kusto_configuration_records_three_queries_and_no_rows()
	{
		// Arrange
		var kusto = new RecordingKustoRunner { IsConfigured = false };
		var search = new RecordingSearch { IsConfigured = false };
		using var drafts = new DraftDirectory();
		var workflow = CreateWorkflow(kusto, search, drafts.Path);
		var logEvent = ErrorEvent(level: "Error", exception: "System.InvalidOperationException: failed\r\n   at Program.Main()");

		// Act
		var result = await workflow.RunAsync(logEvent);

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
		Assert.Equal(0, kusto.Executions);
	}

	[Fact]
	public async Task Missing_search_configuration_returns_no_hits()
	{
		// Arrange
		var kusto = new RecordingKustoRunner { IsConfigured = false };
		var search = new RecordingSearch { IsConfigured = false };
		using var drafts = new DraftDirectory();
		var workflow = CreateWorkflow(kusto, search, drafts.Path);

		// Act
		var result = await workflow.RunAsync(ErrorEvent(level: "Error", exception: "TimeoutException: slow"));

		// Assert
		Assert.NotNull(result);
		Assert.Equal("unavailable", result.Grounding.SearchStatus);
		Assert.Equal(PostmortemSearchTool.IndexName, result.Grounding.Index);
		Assert.Empty(result.Grounding.Hits);
		Assert.NotEmpty(result.Grounding.Queries);
		Assert.Equal(0, search.Executions);
	}

	[Fact]
	public async Task Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service()
	{
		// Arrange
		var kusto = new RecordingKustoRunner { IsConfigured = false };
		var search = new RecordingSearch
		{
			IsConfigured = true,
			Hits = CreateHits(),
		};
		using var drafts = new DraftDirectory();
		var workflow = CreateWorkflow(kusto, search, drafts.Path);

		// Act
		var result = await workflow.RunAsync(ErrorEvent(level: "Critical", exception: "TimeoutException: checkout failed"));

		// Assert
		Assert.NotNull(result);
		Assert.Equal("ok", result.Grounding.SearchStatus);
		Assert.Equal(5, result.Grounding.Hits.Count);
		Assert.DoesNotContain(result.Grounding.Hits, hit => hit.Id == "unrelated-billing");
		Assert.Equal(1, search.Executions);
	}

	[Fact]
	public async Task Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named()
	{
		// Arrange
		var kusto = new RecordingKustoRunner { IsConfigured = false };
		var search = new RecordingSearch { IsConfigured = false };
		using var drafts = new DraftDirectory();
		var workflow = CreateWorkflow(kusto, search, drafts.Path);
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
		var result = await workflow.RunAsync(ErrorEvent(level: "Error", exception: "System.IO.IOException: disk"));

		// Assert
		Assert.NotNull(result);
		Assert.True(File.Exists(result.DraftPath));
		var draft = File.ReadAllText(result.DraftPath);
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

	[Fact]
	public async Task Kql_that_contains_drop_is_not_executed()
	{
		// Arrange
		var kusto = new RecordingKustoRunner { IsConfigured = true };
		var tool = new KustoQueryTool(kusto, new IncidentOptions());
		var kql = "StormEvents | .drop table StormEvents";

		// Act
		var call = await tool.ExecuteAsync(kql);

		// Assert
		Assert.False(call.Executed);
		Assert.Equal(0, kusto.Executions);
		Assert.Empty(call.Rows);
	}

	private static IncidentWorkflow CreateWorkflow(RecordingKustoRunner kusto, RecordingSearch search, string draftsDirectory)
	{
		var options = new IncidentOptions { DraftsDirectory = draftsDirectory };
		var diagnostic = new DiagnosticAgent(new KustoQueryTool(kusto, options));
		var grounding = new RagGroundingAgent(new PostmortemSearchTool(search));
		var remediation = new RemediationAgent(options);
		return new IncidentWorkflow(diagnostic, grounding, remediation);
	}

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

	private sealed class RecordingKustoRunner : IKustoQueryRunner
	{
		public bool IsConfigured { get; init; }

		public int Executions { get; private set; }

		public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken)
		{
			Executions++;
			return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
		}
	}

	private sealed class RecordingSearch : IPostmortemSearch
	{
		public bool IsConfigured { get; init; }

		public IReadOnlyList<SearchHit> Hits { get; init; } = [];

		public int Executions { get; private set; }

		public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
		{
			Executions++;
			return Task.FromResult(Hits);
		}
	}

	private sealed class DraftDirectory : IDisposable
	{
		public DraftDirectory()
		{
			Path = Directory.CreateTempSubdirectory("devops-copilot-incidents").FullName;
		}

		public string Path { get; }

		public void Dispose()
		{
			if (Directory.Exists(Path))
			{
				Directory.Delete(Path, recursive: true);
			}
		}
	}
}
