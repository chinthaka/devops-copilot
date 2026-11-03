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
