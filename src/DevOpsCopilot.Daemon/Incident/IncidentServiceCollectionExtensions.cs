using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Daemon.Incident;

public static class IncidentServiceCollectionExtensions
{
    public static IServiceCollection AddIncidentWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IncidentOptions>(configuration.GetSection("Incident"));
        services.TryAddSingleton<IKustoQueryRunner, ConfiguredKustoQueryRunner>();
        services.TryAddSingleton<IPostmortemSearch, ConfiguredPostmortemSearch>();
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<IncidentOptions>>().Value);
        services.AddSingleton<KustoQueryTool>();
        services.AddSingleton<PostmortemSearchTool>();
        services.AddSingleton(serviceProvider => new DiagnosticAgent(
            serviceProvider.GetRequiredService<KustoQueryTool>(),
            serviceProvider.GetService<IChatClient>()));
        services.AddSingleton(serviceProvider => new RagGroundingAgent(
            serviceProvider.GetRequiredService<PostmortemSearchTool>(),
            serviceProvider.GetService<IChatClient>()));
        services.AddSingleton(serviceProvider => new RemediationAgent(
            serviceProvider.GetRequiredService<IncidentOptions>(),
            serviceProvider.GetService<IChatClient>()));
        services.AddSingleton<IncidentWorkflow>();
        return services;
    }
}
