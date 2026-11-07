using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// Reads the Discord webhook URL from user secrets or the environment, as
/// <c>TeamsConfiguration</c> does for the trigger URL. Missing simply means "not set".
/// </summary>
internal static class DiscordConfiguration
{
    private static readonly Lazy<DiscordSettings?> Lazy = new(Read);

    public static DiscordSettings? Settings => Lazy.Value;

    private static DiscordSettings? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DiscordConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var settings = new DiscordSettings(configuration["Incident:Notifications:Discord:WebhookUrl"] ?? "");
        return settings.IsConfigured ? settings : null;
    }
}
