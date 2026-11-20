using DevOpsCopilot.Architecture.Tests.Common;

namespace DevOpsCopilot.Architecture.Tests;

public class SolutionLayoutTests
{
    [Fact]
    public void Solution_AfterTheDaemonRemoval_ContainsNoDaemonProject()
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
    public void AppHost_WhenComposed_RunsTheWorkerWithItsEventHubAndCheckpointReferences()
    {
        // Arrange
        var appHost = Repository.Project("src/DevOpsCopilot.AppHost/DevOpsCopilot.AppHost.csproj");

        // Act
        var composition = Repository.ReadText("src/DevOpsCopilot.AppHost/AppHost.cs");

        // Assert
        // Since Aspire 17 the worker is added by path, so the AppHost needs no project reference
        // to it and there is no generated Projects.* type. The obsolete generic registration only
        // warns, so it is ruled out here to keep it from coming back.
        Assert.Contains(
            "AddDotnetProject(\"worker\", \"../DevOpsCopilot.Worker/DevOpsCopilot.Worker.csproj\")",
            composition,
            StringComparison.Ordinal);
        Assert.True(Repository.Exists("src/DevOpsCopilot.Worker/DevOpsCopilot.Worker.csproj"));
        Assert.DoesNotContain("AddProject<", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("DevOpsCopilot.Worker", appHost.ProjectReferences);
        Assert.DoesNotContain("DevOpsCopilot.Daemon", appHost.ProjectReferences);
        Assert.DoesNotContain("DevOpsCopilot.Daemon", composition, StringComparison.Ordinal);
        Assert.Contains("WithReference(eventHub, \"eventhubs\")", composition, StringComparison.Ordinal);
        Assert.Contains("WithReference(checkpoints, \"checkpoints\")", composition, StringComparison.Ordinal);
    }
}
