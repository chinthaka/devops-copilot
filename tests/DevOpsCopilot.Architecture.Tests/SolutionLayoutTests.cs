namespace DevOpsCopilot.Architecture.Tests;

public class SolutionLayoutTests
{
    [Fact]
    public void Daemon_project_no_longer_exists_in_the_solution()
    {
        // Arrange
        const string solutionPath = "src/DevOpsCopilot.sln";

        // Act
        var solution = Repository.ReadText(solutionPath);

        // Assert
        Assert.DoesNotContain("DevOpsCopilot.Daemon", solution, StringComparison.Ordinal);
        Assert.False(Repository.Exists("src/DevOpsCopilot.Daemon/DevOpsCopilot.Daemon.csproj"));
        Assert.False(Repository.Exists("tests/DevOpsCopilot.Daemon.Tests/DevOpsCopilot.Daemon.Tests.csproj"));
    }

    [Fact]
    public void The_app_host_runs_the_worker()
    {
        // Arrange
        var appHost = Repository.Project("src/DevOpsCopilot.AppHost/DevOpsCopilot.AppHost.csproj");

        // Act
        var composition = Repository.ReadText("src/DevOpsCopilot.AppHost/AppHost.cs");

        // Assert
        Assert.Contains("DevOpsCopilot.Worker", appHost.ProjectReferences);
        Assert.DoesNotContain("DevOpsCopilot.Daemon", appHost.ProjectReferences);
        Assert.Contains("Projects.DevOpsCopilot_Worker", composition, StringComparison.Ordinal);
        Assert.Contains("WithReference(eventHub, \"eventhubs\")", composition, StringComparison.Ordinal);
        Assert.Contains("WithReference(checkpoints, \"checkpoints\")", composition, StringComparison.Ordinal);
    }
}
