using System.Globalization;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class KustoQueryTool
{
    private static readonly string[] ControlCommands = [".set", ".drop", ".ingest", ".alter"];

    private readonly IKustoQueryRunner _runner;
    private readonly IncidentOptions _options;

    public KustoQueryTool(IKustoQueryRunner runner, IncidentOptions options)
    {
        _runner = runner;
        _options = options;
    }

    public static bool IsControlCommand(string kql)
    {
        return ControlCommands.Any(command => kql.Contains(command, StringComparison.OrdinalIgnoreCase));
    }

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

    public static string EventId(DateTimeOffset timestamp)
    {
        return "evt-" + timestamp.ToUniversalTime().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseEventId(string eventId)
    {
        var compact = eventId.StartsWith("evt-", StringComparison.Ordinal) ? eventId[4..] : eventId;
        return DateTimeOffset.ParseExact(compact, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    public static string ExceptionType(LogEvent logEvent)
    {
        if (!string.IsNullOrWhiteSpace(logEvent.Exception))
        {
            var line = logEvent.Exception
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return logEvent.Message.Trim();
    }

    public static IReadOnlyList<string> BuildQueries(LogEvent logEvent, IncidentOptions options)
    {
        var trace = Quote(logEvent.TraceId ?? "");
        var category = Quote(logEvent.Category);
        var exceptionType = Quote(ExceptionType(logEvent));
        var timestamp = logEvent.Timestamp.ToUniversalTime();
        var previous = KustoDate(timestamp.AddMinutes(-30));
        var start = KustoDate(timestamp.AddMinutes(-15));
        var end = KustoDate(timestamp.AddMinutes(15));
        var exceptions = SafeTable(options.Kusto.ExceptionsTable, "ExceptionsTable");
        var dependencies = SafeTable(options.Kusto.DependenciesTable, "DependenciesTable");

        return
        [
            $"{exceptions} | where TraceId == {trace} | where Timestamp between ({start} .. {end})",
            $"{exceptions} | extend ExceptionType = iff(isnotempty(Exception), tostring(split(Exception, \"\\n\")[0]), Message) | where Category == {category} | where ExceptionType == {exceptionType} | where Timestamp between ({previous} .. {end}) | summarize Count = count() by Window = iff(Timestamp < {start}, \"previous\", \"current\")",
            $"{dependencies} | where TraceId == {trace} | where Success == false | where Timestamp between ({start} .. {end})",
        ];
    }

    private static string FormatRow(IReadOnlyDictionary<string, string> row)
    {
        return string.Join(", ", row.Select(pair => pair.Key + "=" + pair.Value));
    }

    private static string SafeTable(string configuredName, string placeholder)
    {
        if (configuredName.Length == 0 || configuredName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            return placeholder;
        }

        return configuredName;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string KustoDate(DateTimeOffset timestamp)
    {
        return "datetime(" + timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + ")";
    }
}
