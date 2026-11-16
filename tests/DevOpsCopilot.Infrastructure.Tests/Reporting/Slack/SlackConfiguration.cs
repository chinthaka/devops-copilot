using DevOpsCopilot.Infrastructure.Reporting.Slack;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Slack;

/// <summary>
/// Reads the Slack bot token and channel id from user secrets or the environment, as
/// <c>TeamsConfiguration</c> does for the trigger URL. Missing simply means "not set".
/// </summary>
internal static class SlackConfiguration
{
    private static readonly Lazy<SlackSettings?> Lazy = new(Read);

    public static SlackSettings? Settings => Lazy.Value;

    private static SlackSettings? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("worker.appsettings.json", optional: false)
            .AddUserSecrets(typeof(SlackConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:Notifications:Slack");
        var settings = new SlackSettings(section["BotToken"] ?? "", section["ChannelId"] ?? "", section["PostMessageUrl"] ?? "");
        return settings.IsConfigured ? settings : null;
    }
}
