namespace DevOpsCopilot.Domain;

/// <summary>Whether a diagnosis cites only evidence the tools returned, and why not when it does not.</summary>
public sealed record CitationValidation(bool IsValid, string Reason)
{
    public static readonly CitationValidation Valid = new(true, "");

    public static CitationValidation Invalid(string reason) => new(false, reason);
}
