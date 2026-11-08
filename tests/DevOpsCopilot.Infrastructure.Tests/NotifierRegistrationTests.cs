using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Infrastructure.Diagnoses;
using DevOpsCopilot.Infrastructure.Events;
using DevOpsCopilot.Infrastructure.Grounding;
using DevOpsCopilot.Infrastructure.Kql;
using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace DevOpsCopilot.Infrastructure.Tests;

/// <summary>
/// What the host gets from <see cref="InfrastructureServiceCollectionExtensions.AddInfrastructure"/>
/// for stage 7: all four notifiers, always, in the contract order — configuration decides only
/// whether each one sends (AC-2, AC-3).
/// </summary>
public class NotifierRegistrationTests
{
    [Fact]
    public void AddInfrastructure_WithNoChannelConfigured_RegistersFourUnconfiguredNotifiers()
    {
        // Arrange
        var services = Services();

        // Act
        using var provider = services.BuildServiceProvider();
        var notifiers = provider.GetServices<INotifier>().ToArray();

        // Assert
        Assert.Equal(["teams", "slack", "discord", "email"], notifiers.Select(notifier => notifier.Channel));
        Assert.All(notifiers, notifier => Assert.False(notifier.IsConfigured));
    }

    [Theory]
    [InlineData("teams")]
    [InlineData("slack")]
    [InlineData("discord")]
    [InlineData("email")]
    public void AddInfrastructure_WithEachChannelConfigured_RegistersThatNotifier(string channel)
    {
        // Arrange
        var services = Services(
            teams: channel == "teams" ? new TeamsSettings("https://prod-27.westus.logic.azure.com/workflows/a/paths/invoke?sig=s") : null,
            slack: channel == "slack" ? new SlackSettings("xoxb-token", "C0123456789", "https://slack.example.test/api/chat.postMessage") : null,
            discord: channel == "discord" ? new DiscordSettings("https://discord.com/api/webhooks/1/t", MaxRetryDelaySeconds: 10) : null,
            email: channel == "email" ? new EmailSettings("", "https://acs.communication.azure.com/", "DoNotReply@contoso.com", ["oncall@contoso.com"]) : null);

        // Act
        using var provider = services.BuildServiceProvider();
        var notifiers = provider.GetServices<INotifier>().ToArray();

        // Assert
        Assert.Equal(4, notifiers.Length);
        Assert.All(notifiers, notifier => Assert.Equal(notifier.Channel == channel, notifier.IsConfigured));
    }

    [Fact]
    public void AddInfrastructure_WithTemplatePaths_RegistersTheFileTemplateSource()
    {
        // Arrange
        var services = Services(templates: new TemplateSettings("report.md", "", "", ""));

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<FileTemplateSource>(provider.GetRequiredService<ITemplateSource>());
    }

    private static ServiceCollection Services(
        TeamsSettings? teams = null,
        SlackSettings? slack = null,
        DiscordSettings? discord = null,
        EmailSettings? email = null,
        TemplateSettings? templates = null)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            new EventHubSettings("", ""),
            new KustoSettings("", "", "", ""),
            new SearchSettings("", "historical-postmortems", ""),
            new ReportSettings("incidents"),
            AzureOpenAISettings.Unconfigured,
            teams,
            slack,
            discord,
            email,
            templates);
        return services;
    }
}
