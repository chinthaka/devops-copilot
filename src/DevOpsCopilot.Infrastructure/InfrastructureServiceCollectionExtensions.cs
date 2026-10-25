using DevOpsCopilot.Application;
using DevOpsCopilot.Infrastructure.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        AzureOpenAISettings? azureOpenAI = null)
    {
        azureOpenAI ??= AzureOpenAISettings.Unconfigured;
        services.AddSingleton(eventHub);
        services.AddSingleton(kusto);
        services.AddSingleton(search);
        services.AddSingleton(report);
        services.AddSingleton(azureOpenAI);

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
        services.AddSingleton(serviceProvider => new RemediationAgent(
            serviceProvider.GetRequiredService<IReportWriter>(),
            serviceProvider.GetService<IChatClient>()));

        return services;
    }
}
