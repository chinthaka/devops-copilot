namespace DevOpsCopilot.Infrastructure.Reporting.Teams;

/// <summary>
/// The trigger URL of the Teams Workflows flow that posts the card to its channel (requirement
/// D3). The URL carries a shared-access signature, so it is a credential exactly like Discord's
/// webhook URL and is never printed. Blank means the channel is unconfigured.
/// </summary>
public sealed record TeamsSettings(string WebhookUrl)
{
    public static TeamsSettings Unconfigured { get; } = new("");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(WebhookUrl);

    /// <summary>Keeps the trigger URL out of logs and error messages.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("WebhookUrl = ").Append(IsConfigured ? "(set)" : "(none)");
        return true;
    }
}
