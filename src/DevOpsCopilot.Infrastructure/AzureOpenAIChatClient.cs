using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Builds the chat client for the configured Azure OpenAI deployment, or nothing when there is no
/// configuration. A key is used when one is supplied, otherwise the deployment is reached with the
/// ambient Azure credential, which is how it works once deployed. No endpoint, deployment or key
/// is ever defaulted here: an unconfigured worker runs the deterministic path instead.
/// </summary>
public static class AzureOpenAIChatClient
{
    public static IChatClient? TryCreate(AzureOpenAISettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.Deployment))
        {
            return null;
        }

        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            // A malformed endpoint is a configuration mistake, not a reason to stop consuming.
            return null;
        }

        var client = string.IsNullOrWhiteSpace(settings.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(settings.ApiKey));

        return client.GetChatClient(settings.Deployment).AsIChatClient();
    }
}
