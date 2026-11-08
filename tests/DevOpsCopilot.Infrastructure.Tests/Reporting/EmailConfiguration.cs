using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// Reads the Communication Services settings from user secrets or the environment, as
/// <c>TeamsConfiguration</c> does for the trigger URL. Missing simply means "not set".
/// </summary>
internal static class EmailConfiguration
{
    private static readonly Lazy<(EmailSettings, CommunicationServicesEmailSettings)?> Lazy = new(Read);

    /// <summary>The addressing, or null unless both it and Communication Services are set.</summary>
    public static EmailSettings? Settings => Lazy.Value?.Item1;

    public static CommunicationServicesEmailSettings? CommunicationServices => Lazy.Value?.Item2;

    private static (EmailSettings, CommunicationServicesEmailSettings)? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(EmailConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:Notifications:Email");
        var settings = new EmailSettings(
            section["SenderAddress"] ?? "",
            section.GetSection("Recipients").GetChildren().Select(child => child.Value ?? "").ToArray());
        var communicationServices = new CommunicationServicesEmailSettings(
            section["ConnectionString"] ?? "",
            section["Endpoint"] ?? "");
        return settings.IsConfigured && communicationServices.IsConfigured
            ? (settings, communicationServices)
            : null;
    }
}
