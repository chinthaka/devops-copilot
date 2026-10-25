using System.Globalization;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Application;

/// <summary>
/// Stage 6. Asks the model for a diagnosis and decides whether to trust it. An unconfigured,
/// silent, slow, failing or badly cited model all end the same way: the deterministic diagnosis,
/// with an open question naming what happened. The contract's confidence rule is applied last, on
/// every path, so no model can claim more confidence than the evidence allows.
/// </summary>
public sealed class Diagnose
{
    public const string ModelUnavailable = "The diagnosis model was unavailable.";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IDiagnosisModel _model;
    private readonly TimeSpan _timeout;

    public Diagnose(IDiagnosisModel model, TimeSpan? timeout = null)
    {
        _model = model;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<Diagnosis> DiagnoseAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken = default)
    {
        var diagnosis = await ProposeAsync(diagnostic, grounding, cancellationToken);
        return DiagnosisRules.ApplyConfidenceRule(diagnosis, diagnostic, grounding);
    }

    private async Task<Diagnosis> ProposeAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        CancellationToken cancellationToken)
    {
        if (!_model.IsConfigured)
        {
            return Deterministic(diagnostic, grounding, ModelUnavailable);
        }

        Diagnosis? proposed;
        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            proposed = await _model.ProposeAsync(diagnostic, grounding, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The worker is not shutting down, so this is the model running past its own deadline.
            var seconds = _timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
            return Deterministic(
                diagnostic,
                grounding,
                $"The diagnosis model did not answer within {seconds} seconds, so the deterministic diagnosis was used.");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The message can carry endpoint or payload detail, so only the type is reported.
            return Deterministic(
                diagnostic,
                grounding,
                $"The diagnosis model failed with {exception.GetType().Name}, so the deterministic diagnosis was used.");
        }

        if (proposed is null)
        {
            return Deterministic(
                diagnostic,
                grounding,
                "The diagnosis model returned no diagnosis, so the deterministic diagnosis was used.");
        }

        var citations = DiagnosisCitations.Validate(proposed, diagnostic, grounding);
        if (!citations.IsValid)
        {
            return Deterministic(
                diagnostic,
                grounding,
                "The model's diagnosis was rejected for citing evidence that was not returned. " + citations.Reason);
        }

        return proposed;
    }

    private static Diagnosis Deterministic(DiagnosticHandoff diagnostic, GroundingHandoff grounding, string reason) =>
        Diagnosis.Deterministic(diagnostic, grounding, reason);
}
