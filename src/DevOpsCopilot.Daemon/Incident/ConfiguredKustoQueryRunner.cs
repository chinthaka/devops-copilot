using System.Data;
using Kusto.Data;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class ConfiguredKustoQueryRunner : IKustoQueryRunner
{
    private readonly IncidentOptions _options;

    public ConfiguredKustoQueryRunner(IOptions<IncidentOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured =>
        HasValue(_options.Kusto.ClusterUri)
        && HasValue(_options.Kusto.Database)
        && HasValue(_options.Kusto.ExceptionsTable)
        && HasValue(_options.Kusto.DependenciesTable);

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string kql, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Kusto is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var builder = new KustoConnectionStringBuilder(_options.Kusto.ClusterUri, _options.Kusto.Database);
        using var provider = KustoClientFactory.CreateCslQueryProvider(builder);
        using var reader = provider.ExecuteQuery(_options.Kusto.Database, kql, properties: null);
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
