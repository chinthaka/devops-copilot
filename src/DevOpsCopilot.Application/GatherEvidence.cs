using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Stage 4. Asks the log store the fixed questions for one event and turns the rows into findings.
/// Every query is built here, never taken from a model, and a control command is refused.
/// </summary>
public sealed class GatherEvidence
{
    private readonly ILogQuery _logQuery;
    private readonly LogQueryTargets _targets;

    public GatherEvidence(ILogQuery logQuery, LogQueryTargets targets)
    {
        _logQuery = logQuery;
        _targets = targets;
    }

    public async Task<KustoCall> ExecuteAsync(string kql, CancellationToken cancellationToken = default)
    {
        if (KqlQuestions.IsControlCommand(kql) || !_logQuery.IsConfigured)
        {
            return new KustoCall(kql, false, []);
        }

        var rows = await _logQuery.QueryAsync(kql, cancellationToken);
        return new KustoCall(kql, true, rows);
    }

    public async Task<DiagnosticHandoff> DiagnoseAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var queries = KqlQuestions.Build(logEvent, _targets);
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
            EventIdentity.EventId(logEvent.Timestamp),
            logEvent.TraceId ?? "",
            logEvent.SpanId ?? "",
            logEvent.Category,
            logEvent.Exception ?? "",
            status,
            storedQueries,
            rows,
            findings);
    }

    private static string FormatRow(IReadOnlyDictionary<string, string> row)
    {
        return string.Join(", ", row.Select(pair => pair.Key + "=" + pair.Value));
    }
}
