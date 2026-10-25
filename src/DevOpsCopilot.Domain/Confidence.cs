namespace DevOpsCopilot.Domain;

/// <summary>The confidence of a diagnosis: low, medium or high and nothing else.</summary>
public sealed record Confidence
{
    public static readonly Confidence Low = new("low");
    public static readonly Confidence Medium = new("medium");
    public static readonly Confidence High = new("high");

    private Confidence(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Confidence Create(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "low" => Low,
            "medium" => Medium,
            "high" => High,
            _ => throw new ArgumentException($"Confidence must be low, medium or high, not \"{value}\".", nameof(value)),
        };
    }

    public override string ToString() => Value;
}
