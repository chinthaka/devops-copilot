using System.Globalization;

namespace DevOpsCopilot.Domain;

/// <summary>
/// The fixed, read-only questions the evidence stage asks of the log store, and the guard that
/// keeps control commands out. KQL is never taken from a model.
/// </summary>
public static class KqlQuestions
{
    private static readonly string[] ControlCommands = [".set", ".drop", ".ingest", ".alter"];

    public static bool IsControlCommand(string kql)
    {
        return ControlCommands.Any(command => kql.Contains(command, StringComparison.OrdinalIgnoreCase));
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

    public static IReadOnlyList<string> Build(LogEvent logEvent, LogQueryTargets targets)
    {
        var trace = Quote(logEvent.TraceId ?? "");
        var category = Quote(logEvent.Category);
        var exceptionType = Quote(ExceptionType(logEvent));
        var timestamp = logEvent.Timestamp.ToUniversalTime();
        var previous = KustoDate(timestamp.AddMinutes(-30));
        var start = KustoDate(timestamp.AddMinutes(-15));
        var end = KustoDate(timestamp.AddMinutes(15));
        var exceptions = SafeTable(targets.ExceptionsTable, "ExceptionsTable");
        var dependencies = SafeTable(targets.DependenciesTable, "DependenciesTable");

        return
        [
            $"{exceptions} | where TraceId == {trace} | where Timestamp between ({start} .. {end})",
            $"{exceptions} | extend ExceptionType = iff(isnotempty(Exception), tostring(split(Exception, \"\\n\")[0]), Message) | where Category == {category} | where ExceptionType == {exceptionType} | where Timestamp between ({previous} .. {end}) | summarize Count = count() by Window = iff(Timestamp < {start}, \"previous\", \"current\")",
            $"{dependencies} | where TraceId == {trace} | where Success == false | where Timestamp between ({start} .. {end})",
        ];
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
