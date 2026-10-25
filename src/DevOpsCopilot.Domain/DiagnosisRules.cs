namespace DevOpsCopilot.Domain;

/// <summary>The contract rules every diagnosis obeys, whatever produced it.</summary>
public static class DiagnosisRules
{
    /// <summary>
    /// Confidence is low when both tools were unavailable, whatever the model returned
    /// (pipeline.md § 6).
    /// </summary>
    public static Diagnosis ApplyConfidenceRule(Diagnosis diagnosis, DiagnosticHandoff diagnostic, GroundingHandoff grounding)
    {
        if (diagnostic.KustoStatus != "ok" && grounding.SearchStatus != "ok")
        {
            return diagnosis with { Confidence = Confidence.Low };
        }

        return diagnosis;
    }
}
