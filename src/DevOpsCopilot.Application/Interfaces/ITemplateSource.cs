namespace DevOpsCopilot.Application.Interfaces;

/// <summary>
/// Supplies the configured text of one named template, or null to mean "use the embedded
/// default". Read on every render, so an edited template applies to the next event without a
/// deployment. Implementations do not throw: an unset, missing or unreadable template is null.
/// </summary>
public interface ITemplateSource
{
    Task<string?> GetAsync(string name, CancellationToken cancellationToken);
}
