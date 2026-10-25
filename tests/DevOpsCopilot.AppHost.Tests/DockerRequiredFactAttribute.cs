using System.Diagnostics;

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

internal static class Docker
{
    private static readonly Lazy<bool> Available = new(Probe);

    public static bool IsAvailable => Available.Value;

    private static bool Probe()
    {
        try
        {
            using var docker = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (docker is null)
            {
                return false;
            }

            if (!docker.WaitForExit(10_000))
            {
                docker.Kill(entireProcessTree: true);
                return false;
            }

            return docker.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
