using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// Reads the Communication Services settings from user secrets or the environment, as
/// <c>TeamsConfiguration</c> does for the trigger URL. Missing simply means "not set".
/// </summary>
internal static class EmailConfiguration
{
    private static readonly Lazy<EmailSettings?> Lazy = new(Read);

    public static EmailSettings? Settings => Lazy.Value;

    private static EmailSettings? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(EmailConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:Notifications:Email");
        var settings = new EmailSettings(
            section["ConnectionString"] ?? "",
            section["Endpoint"] ?? "",
            section["SenderAddress"] ?? "",
            section.GetSection("Recipients").GetChildren().Select(child => child.Value ?? "").ToArray());
        return settings.IsConfigured ? settings : null;
    }
}
