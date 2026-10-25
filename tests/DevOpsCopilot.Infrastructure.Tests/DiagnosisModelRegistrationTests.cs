using DevOpsCopilot.Application;
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
    public void Without_azure_openai_the_registered_model_is_unconfigured()
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
    public void With_azure_openai_the_registered_model_is_configured()
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
    public void The_diagnose_stage_is_registered_with_the_configured_timeout()
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
    public void Omitting_the_azure_openai_settings_keeps_the_deterministic_path()
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
