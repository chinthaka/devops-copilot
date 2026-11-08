namespace DevOpsCopilot.Infrastructure.Reporting.Email;

/// <summary>Which transport carries the email channel's messages (requirement D14). Exactly one per deployment.</summary>
public enum EmailTransportKind
{
    CommunicationServices,
    Smtp,
}
