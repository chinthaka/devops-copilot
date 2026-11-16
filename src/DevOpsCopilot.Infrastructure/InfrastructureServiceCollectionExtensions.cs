using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Infrastructure.Agents;
using DevOpsCopilot.Infrastructure.Diagnoses;
using DevOpsCopilot.Infrastructure.Events;
using DevOpsCopilot.Infrastructure.Grounding;
using DevOpsCopilot.Infrastructure.Kql;
using DevOpsCopilot.Infrastructure.Reporting.Discord;
using DevOpsCopilot.Infrastructure.Reporting.Email;
using DevOpsCopilot.Infrastructure.Reporting.Slack;
using DevOpsCopilot.Infrastructure.Reporting.Teams;
using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Registers the implementations of the Application interfaces. The settings records come from
/// the host, so nothing here reads configuration.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        EventHubSettings eventHub,
        KustoSettings kusto,
        SearchSettings search,
        ReportSettings report,
        AzureOpenAISettings? azureOpenAI = null,
        TeamsSettings? teams = null,
        SlackSettings? slack = null,
        DiscordSettings? discord = null,
        EmailSettings? email = null,
        EmailTransportSettings? emailTransport = null,
        TemplateSettings? templates = null)
    {
        azureOpenAI ??= AzureOpenAISettings.Unconfigured;
        services.AddSingleton(eventHub);
        services.AddSingleton(kusto);
        services.AddSingleton(search);
        services.AddSingleton(report);
        services.AddSingleton(azureOpenAI);
        services.AddSingleton(teams ?? TeamsSettings.Unconfigured);
        services.AddSingleton(slack ?? SlackSettings.Unconfigured);
        services.AddSingleton(discord ?? DiscordSettings.Unconfigured);
        services.AddSingleton(email ?? EmailSettings.Unconfigured);
        services.AddSingleton(templates ?? TemplateSettings.Unconfigured);

        services.TryAddSingleton<IEventSource, EventHubLogConsumer>();
        services.TryAddSingleton<ILogQuery, KustoLogQuery>();
        services.TryAddSingleton<IKnowledgeSearch, PostmortemKnowledgeSearch>();
        services.TryAddSingleton<IReportWriter, FileReportWriter>();

        // The chat client is registered only when it exists, so the three agents keep resolving it
        // optionally and an unconfigured worker stays on the deterministic path.
        var chatClient = AzureOpenAIChatClient.TryCreate(azureOpenAI);
        if (chatClient is not null)
        {
            services.TryAddSingleton(chatClient);
        }

        services.TryAddSingleton<IDiagnosisModel>(
            serviceProvider => new AzureOpenAIDiagnosisModel(serviceProvider.GetService<IChatClient>()));
        services.TryAddSingleton(
            serviceProvider => new Diagnose(
                serviceProvider.GetRequiredService<IDiagnosisModel>(),
                serviceProvider.GetRequiredService<AzureOpenAISettings>().Timeout));

        services.AddSingleton(serviceProvider => new DiagnosticAgent(
            serviceProvider.GetRequiredService<GatherEvidence>(),
            serviceProvider.GetService<IChatClient>()));
        services.AddSingleton(serviceProvider => new RagGroundingAgent(
            serviceProvider.GetRequiredService<GroundInHistory>(),
            serviceProvider.GetService<IChatClient>()));
        // Stage 7. Every notifier is always registered, in the contract order Teams, Slack,
        // Discord, email; its settings decide only whether it sends (AC-2, AC-3). The HTTP
        // notifiers are typed clients so IHttpClientFactory owns the handlers.
        services.AddHttpClient<TeamsNotifier>();
        services.AddHttpClient<SlackNotifier>();
        services.AddHttpClient<DiscordNotifier>();
        services.TryAddSingleton(_ => CreateEmailTransport(emailTransport));
        services.AddSingleton<INotifier>(serviceProvider => serviceProvider.GetRequiredService<TeamsNotifier>());
        services.AddSingleton<INotifier>(serviceProvider => serviceProvider.GetRequiredService<SlackNotifier>());
        services.AddSingleton<INotifier>(serviceProvider => serviceProvider.GetRequiredService<DiscordNotifier>());
        services.AddSingleton<INotifier, EmailNotifier>();
        services.TryAddSingleton<ITemplateSource, FileTemplateSource>();

        services.AddSingleton(serviceProvider => new DiagnosisAgent(
            serviceProvider.GetRequiredService<Diagnose>(),
            serviceProvider.GetRequiredService<IReportWriter>(),
            serviceProvider.GetService<IChatClient>()));

        return services;
    }

    /// <summary>
    /// Builds exactly the selected email transport (requirement D14). This is the only place the
    /// kind is examined, and it is composition, not channel logic: the email notifier never asks
    /// which transport it holds. No transport selected, or the selected one blank, leaves the
    /// email channel unavailable.
    /// </summary>
    private static IEmailTransport CreateEmailTransport(EmailTransportSettings? settings) =>
        settings?.Kind switch
        {
            EmailTransportKind.CommunicationServices when settings.CommunicationServices.IsConfigured =>
                CommunicationServicesEmailTransport.Create(settings.CommunicationServices),
            EmailTransportKind.Smtp => new SmtpEmailTransport(settings.Smtp),
            _ => new UnconfiguredEmailTransport(),
        };
}
