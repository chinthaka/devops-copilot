using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests;

/// <summary>
/// A fact that needs a real Azure OpenAI deployment. Without one the test is skipped, not failed,
/// so CI and local runs with no secrets stay green. Mirrors <c>DockerRequiredFactAttribute</c> in
/// the AppHost tests.
/// </summary>
public sealed class AzureOpenAIRequiredFactAttribute : FactAttribute
{
    public AzureOpenAIRequiredFactAttribute()
    {
        if (AzureOpenAIConfiguration.Settings is null)
        {
            Skip = "Requires an Azure OpenAI deployment. Set Incident:AzureOpenAI:Endpoint and :Deployment in user secrets.";
        }
    }
}

/// <summary>
/// Reads the Azure OpenAI configuration from user secrets or the environment. Nothing is read from
/// the repository and no value is defaulted, so a missing configuration simply means "not set".
/// </summary>
internal static class AzureOpenAIConfiguration
{
    private static readonly Lazy<AzureOpenAISettings?> Lazy = new(Read);

    public static AzureOpenAISettings? Settings => Lazy.Value;

    private static AzureOpenAISettings? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AzureOpenAIConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:AzureOpenAI");
        var endpoint = section["Endpoint"] ?? "";
        var deployment = section["Deployment"] ?? "";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return null;
        }

        return new AzureOpenAISettings(
            endpoint,
            deployment,
            section["ApiKey"] ?? "",
            int.TryParse(section["TimeoutSeconds"], out var seconds) ? seconds : 0);
    }
}
