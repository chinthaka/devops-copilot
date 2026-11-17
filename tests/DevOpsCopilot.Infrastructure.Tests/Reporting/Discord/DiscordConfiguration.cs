using DevOpsCopilot.Infrastructure.Reporting.Discord;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Discord;

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
            .AddJsonFile("worker.appsettings.json", optional: false)
            .AddUserSecrets(typeof(DiscordConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:Notifications:Discord");
        var settings = new DiscordSettings(
            section["WebhookUrl"] ?? "",
            int.TryParse(section["MaxRetryDelaySeconds"], out var seconds) ? seconds : 0);
        return settings.IsConfigured ? settings : null;
    }
}
