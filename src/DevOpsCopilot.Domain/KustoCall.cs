namespace DevOpsCopilot.Domain;

public sealed record KustoCall(string Kql, bool Executed, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);
