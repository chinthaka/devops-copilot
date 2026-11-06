using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

/// <summary>
/// Reads the Teams Workflows trigger URL from user secrets or the environment, as
/// <c>AzureOpenAIConfiguration</c> does for the model. Missing simply means "not set".
/// </summary>
internal static class TeamsConfiguration
{
    private static readonly Lazy<TeamsSettings?> Lazy = new(Read);

    public static TeamsSettings? Settings => Lazy.Value;

    private static TeamsSettings? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(TeamsConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var webhookUrl = configuration["Incident:Notifications:Teams:WebhookUrl"] ?? "";
        return string.IsNullOrWhiteSpace(webhookUrl) ? null : new TeamsSettings(webhookUrl);
    }
}
