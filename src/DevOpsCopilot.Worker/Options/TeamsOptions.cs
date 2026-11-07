namespace DevOpsCopilot.Worker.Options;

/// <summary>The <c>Incident:Notifications:Teams</c> section: the Workflows trigger URL, a secret.</summary>
public sealed class TeamsOptions
{
    public string WebhookUrl { get; set; } = "";
}
