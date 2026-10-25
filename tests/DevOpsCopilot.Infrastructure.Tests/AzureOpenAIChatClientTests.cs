namespace DevOpsCopilot.Infrastructure.Tests;

public class AzureOpenAIChatClientTests
{
    [Theory]
    [InlineData("", "gpt-4o", "key")]
    [InlineData("   ", "gpt-4o", "key")]
    [InlineData("https://contoso.openai.azure.com/", "", "key")]
    [InlineData("https://contoso.openai.azure.com/", "   ", "key")]
    public void Without_an_endpoint_and_a_deployment_no_client_is_built(string endpoint, string deployment, string apiKey)
    {
        // Arrange
        var settings = new AzureOpenAISettings(endpoint, deployment, apiKey, 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.Null(client);
    }

    [Fact]
    public void An_endpoint_that_is_not_a_url_builds_no_client_rather_than_throwing()
    {
        // Arrange
        var settings = new AzureOpenAISettings("not a url", "gpt-4o", "key", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.Null(client);
    }

    [Fact]
    public void An_endpoint_and_a_deployment_with_a_key_build_a_client()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "a-local-test-key", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Without_a_key_a_client_is_still_built_for_managed_identity()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 30);

        // Act
        using var client = AzureOpenAIChatClient.TryCreate(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void The_settings_never_carry_a_default_endpoint_deployment_or_key()
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
    public void A_non_positive_timeout_falls_back_to_the_default()
    {
        // Arrange
        var zero = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 0);
        var negative = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", -5);

        // Act
        var zeroTimeout = zero.Timeout;
        var negativeTimeout = negative.Timeout;

        // Assert
        Assert.Equal(AzureOpenAISettings.DefaultTimeout, zeroTimeout);
        Assert.Equal(AzureOpenAISettings.DefaultTimeout, negativeTimeout);
    }

    [Fact]
    public void A_configured_timeout_is_used()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "", 45);

        // Act
        var timeout = settings.Timeout;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(45), timeout);
    }

    [Fact]
    public void The_settings_do_not_print_the_api_key()
    {
        // Arrange
        var settings = new AzureOpenAISettings("https://contoso.openai.azure.com/", "gpt-4o", "super-secret-key", 30);

        // Act
        var text = settings.ToString();

        // Assert
        Assert.DoesNotContain("super-secret-key", text, StringComparison.Ordinal);
    }
}
