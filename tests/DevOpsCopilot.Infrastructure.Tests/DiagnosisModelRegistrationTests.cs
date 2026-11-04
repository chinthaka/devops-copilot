using DevOpsCopilot.Application.Diagnoses;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Infrastructure.Agents;
using DevOpsCopilot.Infrastructure.Diagnoses;
using DevOpsCopilot.Infrastructure.Events;
using DevOpsCopilot.Infrastructure.Grounding;
using DevOpsCopilot.Infrastructure.Kql;
using DevOpsCopilot.Infrastructure.Reporting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DevOpsCopilot.Infrastructure.Tests;

/// <summary>
/// What the host gets from <see cref="InfrastructureServiceCollectionExtensions.AddInfrastructure"/>
/// for stage 6, with and without an Azure OpenAI deployment configured.
/// </summary>
public class DiagnosisModelRegistrationTests
{
    [Fact]
    public void AddInfrastructure_WithoutAzureOpenAI_RegistersAnUnconfiguredModel()
    {
        // Arrange
        var services = Services(AzureOpenAISettings.Unconfigured);

        // Act
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IDiagnosisModel>();

        // Assert
        Assert.False(model.IsConfigured);
        Assert.Null(provider.GetService<IChatClient>());
    }

    [Fact]
    public void AddInfrastructure_WithAzureOpenAI_RegistersAConfiguredModel()
    {
        // Arrange
        var services = Services(new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "a-local-test-key", 30));

        // Act
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IDiagnosisModel>();

        // Assert
        Assert.True(model.IsConfigured);
        Assert.NotNull(provider.GetService<IChatClient>());
    }

    [Fact]
    public void AddInfrastructure_WithAConfiguredTimeout_RegistersTheDiagnoseStageWithThatTimeout()
    {
        // Arrange
        var services = Services(new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 45));

        // Act
        using var provider = services.BuildServiceProvider();
        var stage = provider.GetRequiredService<Diagnose>();

        // Assert
        Assert.NotNull(stage);
        Assert.Equal(TimeSpan.FromSeconds(45), provider.GetRequiredService<AzureOpenAISettings>().Timeout);
    }

    [Fact]
    public void AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent()
    {
        // Arrange
        var services = Services(AzureOpenAISettings.Unconfigured);

        // Act
        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<DiagnosisAgent>();

        // Assert
        Assert.NotNull(agent);
        Assert.Same(agent, provider.GetRequiredService<DiagnosisAgent>());
    }

    [Fact]
    public void AddInfrastructure_WhenTheAzureOpenAISettingsAreOmitted_KeepsTheDeterministicPath()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure(
            new EventHubSettings("", ""),
            new KustoSettings("", "", "", ""),
            new SearchSettings("", "historical-postmortems", ""),
            new ReportSettings("incidents"));

        // Act
        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IDiagnosisModel>();

        // Assert
        Assert.False(model.IsConfigured);
    }

    private static ServiceCollection Services(AzureOpenAISettings azureOpenAI)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            new EventHubSettings("", ""),
            new KustoSettings("", "", "", ""),
            new SearchSettings("", "historical-postmortems", ""),
            new ReportSettings("incidents"),
            azureOpenAI);
        return services;
    }
}
