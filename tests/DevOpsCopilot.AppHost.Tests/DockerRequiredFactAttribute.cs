namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// A fact that needs a running Docker daemon for the Event Hubs and Azurite emulators. Without
/// one the test is skipped, not failed, so CI and local runs without Docker stay green.
/// </summary>
public sealed class DockerRequiredFactAttribute : FactAttribute
{
    public DockerRequiredFactAttribute()
    {
        if (!Docker.IsAvailable)
        {
            Skip = "Requires a running Docker daemon for the Event Hubs and Azurite emulators.";
        }
    }
}
