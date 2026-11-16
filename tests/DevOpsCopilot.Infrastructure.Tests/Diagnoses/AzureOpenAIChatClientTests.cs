using DevOpsCopilot.Infrastructure.Diagnoses;

namespace DevOpsCopilot.Infrastructure.Tests.Diagnoses;

public class AzureOpenAIChatClientTests
{
    [Theory]
    [InlineData("", "gpt-4o", "key")]
    [InlineData("   ", "gpt-4o", "key")]
    [InlineData("https://contoso.openai.azure.com/", "", "key")]
    [InlineData("https://contoso.openai.azure.com/", "   ", "key")]
    public void TryCreate_WithoutAnEndpointOrADeployment_ReturnsNull(string endpoint, string deployment, string apiKey)
    {
        // Arrange
        var settings = new AzureOpenAISettings(endpoint, deployment, apiKey, 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.Null(client);
    }

    [Fact]
    public void TryCreate_WithAnEndpointThatIsNotAUrl_ReturnsNullRatherThanThrowing()
    {
        // Arrange
        var settings = new AzureOpenAISettings("not a url", "gpt-4o", "key", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.Null(client);
    }

    [Fact]
    public void TryCreate_WithAnEndpointADeploymentAndAKey_BuildsAClient()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "a-local-test-key", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void TryCreate_WithoutAKey_StillBuildsAClientForManagedIdentity()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void TryCreate_WithTheUnconfiguredSettings_ReturnsNullAndTheSettingsCarryNoDefaults()
    {
        // Arrange
        var settings = AzureOpenAISettings.Unconfigured;

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.Equal("", settings.Endpoint);
        Assert.Equal("", settings.Deployment);
        Assert.Equal("", settings.ApiKey);
        Assert.Null(client);
    }

    [Fact]
    public void Timeout_WithAPositiveConfiguredValue_ReturnsTheConfiguredValue()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 45);

        // Act
        var timeout = settings.Timeout;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(45), timeout);
    }

    [Fact]
    public void ToString_WithAnApiKey_DoesNotPrintTheKey()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "super-secret-key", 30);

        // Act
        var text = settings.ToString();

        // Assert
        Assert.DoesNotContain("super-secret-key", text, StringComparison.Ordinal);
    }
}
