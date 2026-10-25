namespace DevOpsCopilot.Daemon.Incident;

public sealed class KustoQueryTool
{
    private readonly IKustoQueryRunner _runner;
    private readonly IncidentOptions _options;

    public KustoQueryTool(IKustoQueryRunner runner, IncidentOptions options)
    {
        _runner = runner;
        _options = options;
    }

    public static bool IsControlCommand(string kql) => KqlQuestions.IsControlCommand(kql);

    public async Task<KustoCall> ExecuteAsync(string kql, CancellationToken cancellationToken = default)
    {
        if (IsControlCommand(kql) || !_runner.IsConfigured)
        {
            return new KustoCall(kql, false, []);
        }

        var rows = await _runner.QueryAsync(kql, cancellationToken);
        return new KustoCall(kql, true, rows);
    }

    public async Task<DiagnosticHandoff> DiagnoseAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var queries = BuildQueries(logEvent, _options);
        var storedQueries = new List<string>();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var findings = new List<string>();

        foreach (var query in queries)
        {
            var call = await ExecuteAsync(query, cancellationToken);
            storedQueries.Add(call.Kql);
            if (!call.Executed || call.Rows.Count == 0)
            {
                continue;
            }

            var row = call.Rows[0];
            rows.Add(row);
            findings.Add($"Query `{call.Kql}` returned {FormatRow(row)}.");
        }

        var status = rows.Count > 0 && findings.Count > 0 ? "ok" : "unavailable";
        return new DiagnosticHandoff(
            EventId(logEvent.Timestamp),
            logEvent.TraceId ?? "",
            logEvent.SpanId ?? "",
            logEvent.Category,
            logEvent.Exception ?? "",
            status,
            storedQueries,
            rows,
            findings);
    }

    public static string EventId(DateTimeOffset timestamp) => EventIdentity.EventId(timestamp);

    public static DateTimeOffset ParseEventId(string eventId) => EventIdentity.ParseEventId(eventId);

    public static string ExceptionType(LogEvent logEvent) => KqlQuestions.ExceptionType(logEvent);

    public static IReadOnlyList<string> BuildQueries(LogEvent logEvent, IncidentOptions options) =>
        KqlQuestions.Build(logEvent, new LogQueryTargets(options.Kusto.ExceptionsTable, options.Kusto.DependenciesTable));

    private static string FormatRow(IReadOnlyDictionary<string, string> row)
    {
        return string.Join(", ", row.Select(pair => pair.Key + "=" + pair.Value));
    }
}
