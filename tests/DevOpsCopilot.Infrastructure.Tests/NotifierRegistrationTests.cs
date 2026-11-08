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
            email: channel == "email" ? Addressing : null,
            emailTransport: channel == "email" ? Transport(EmailTransportKind.CommunicationServices) : null);

        // Act
        using var provider = services.BuildServiceProvider();
        var notifiers = provider.GetServices<INotifier>().ToArray();

        // Assert
        Assert.Equal(4, notifiers.Length);
        Assert.All(notifiers, notifier => Assert.Equal(notifier.Channel == channel, notifier.IsConfigured));
    }

    [Theory]
    [InlineData(EmailTransportKind.CommunicationServices, typeof(CommunicationServicesEmailTransport))]
    [InlineData(EmailTransportKind.Smtp, typeof(SmtpEmailTransport))]
    public void AddInfrastructure_WithEachEmailTransportSelected_RegistersOnlyThatTransport(
        EmailTransportKind kind,
        Type expected)
    {
        // Arrange: both transports' settings are filled, so only the selection can decide (AC-17).
        var services = Services(email: Addressing, emailTransport: Transport(kind));

        // Act
        using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IEmailTransport>();
        var email = provider.GetServices<INotifier>().Single(notifier => notifier.Channel == "email");

        // Assert
        Assert.IsType(expected, transport);
        Assert.True(email.IsConfigured);
    }

    [Theory]
    [InlineData(EmailTransportKind.CommunicationServices)]
    [InlineData(EmailTransportKind.Smtp)]
    public void AddInfrastructure_WithTheSelectedTransportUnconfigured_RegistersAnUnconfiguredEmailNotifier(
        EmailTransportKind kind)
    {
        // Arrange: the selected transport is blank and the other one is filled (AC-17).
        var transport = kind == EmailTransportKind.Smtp
            ? new EmailTransportSettings(kind, CommunicationServices, SmtpSettings.Unconfigured)
            : new EmailTransportSettings(kind, CommunicationServicesEmailSettings.Unconfigured, Smtp);
        var services = Services(email: Addressing, emailTransport: transport);

        // Act
        using var provider = services.BuildServiceProvider();
        var email = provider.GetServices<INotifier>().Single(notifier => notifier.Channel == "email");

        // Assert
        Assert.False(email.IsConfigured);
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

    private static readonly EmailSettings Addressing = new("DoNotReply@contoso.com", ["oncall@contoso.com"]);

    private static readonly CommunicationServicesEmailSettings CommunicationServices =
        new("", "https://acs.communication.azure.com/");

    private static readonly SmtpSettings Smtp = new("smtp.contoso.com", 587, SmtpSecurity.StartTls, "", "");

    private static EmailTransportSettings Transport(EmailTransportKind kind) => new(kind, CommunicationServices, Smtp);

    private static ServiceCollection Services(
        TeamsSettings? teams = null,
        SlackSettings? slack = null,
        DiscordSettings? discord = null,
        EmailSettings? email = null,
        EmailTransportSettings? emailTransport = null,
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
            emailTransport,
            templates);
        return services;
    }
}
