namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>
/// The selected email transport and the settings of both (requirement D14). Only the selected
/// transport is built; the other's settings are ignored. Printing it prints the two redacting
/// records, so no credential is shown.
/// </summary>
public sealed record EmailTransportSettings(
    EmailTransportKind Kind,
    CommunicationServicesEmailSettings CommunicationServices,
    SmtpSettings Smtp);
