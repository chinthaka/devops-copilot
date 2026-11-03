using System.Diagnostics;

namespace DevOpsCopilot.AppHost.Tests.Common;

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
