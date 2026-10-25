using System.Data;
using DevOpsCopilot.Application;
using Kusto.Data;
using Kusto.Data.Net.Client;

namespace DevOpsCopilot.Infrastructure;

/// <summary>Runs one read-only KQL query against Log Analytics.</summary>
public sealed class KustoLogQuery : ILogQuery
{
    private readonly KustoSettings _settings;

    public KustoLogQuery(KustoSettings settings)
    {
        _settings = settings;
    }

    public bool IsConfigured =>
        HasValue(_settings.ClusterUri)
        && HasValue(_settings.Database)
        && HasValue(_settings.ExceptionsTable)
        && HasValue(_settings.DependenciesTable);

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Kusto is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var builder = new KustoConnectionStringBuilder(_settings.ClusterUri, _settings.Database);
        using var provider = KustoClientFactory.CreateCslQueryProvider(builder);
        using var reader = provider.ExecuteQuery(_settings.Database, kql, properties: null);
        return Task.FromResult(ReadRows(reader));
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadRows(IDataReader reader)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? "" : reader.GetValue(index)?.ToString() ?? "";
            }

            rows.Add(row);
        }

        return rows;
    }

    private static bool HasValue(string value) => !string.IsNullOrWhiteSpace(value);
}
