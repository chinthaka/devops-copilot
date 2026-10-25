namespace DevOpsCopilot.Domain;

/// <summary>Whether a diagnosis cites only evidence the tools returned, and why not when it does not.</summary>
public sealed record CitationValidation(bool IsValid, string Reason)
{
    public static readonly CitationValidation Valid = new(true, "");

    public static CitationValidation Invalid(string reason) => new(false, reason);
}

/// <summary>
/// The citation guard. A diagnosis is rejected when any evidence citation points outside the
/// diagnostic handoff's lists, or a similar incident names a hit that was not returned.
/// </summary>
public static class DiagnosisCitations
{
    public static CitationValidation Validate(Diagnosis diagnosis, DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        foreach (var citation in diagnosis.Evidence)
        {
            if (citation.Kql < 0 || citation.Kql >= diagnostic.Kql.Count)
            {
                return CitationValidation.Invalid(
                    $"The diagnosis cites kql {citation.Kql}, but only {diagnostic.Kql.Count} queries were recorded.");
            }

            if (citation.Row < 0 || citation.Row >= diagnostic.Rows.Count)
            {
                return CitationValidation.Invalid(
                    $"The diagnosis cites row {citation.Row}, but only {diagnostic.Rows.Count} rows were returned.");
            }
        }

        foreach (var similar in diagnosis.SimilarIncidents)
        {
            if (!grounding.Hits.Any(hit => hit.Id == similar))
            {
                return CitationValidation.Invalid(
                    $"The diagnosis names similar incident \"{similar}\", which was not among the returned hits.");
            }
        }

        return CitationValidation.Valid;
    }
}
