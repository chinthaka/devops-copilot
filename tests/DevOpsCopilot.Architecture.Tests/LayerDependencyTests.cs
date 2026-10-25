using System.Reflection;

namespace DevOpsCopilot.Architecture.Tests;

/// <summary>
/// Enforces the strict Clean Architecture rules in AGENTS.md. Dependencies point inwards only.
/// Each rule is checked against the project file, which is what a contributor edits, and against
/// the built assembly, which is what the compiler actually produced.
/// </summary>
public class LayerDependencyTests
{
    private const string Domain = "DevOpsCopilot.Domain";
    private const string Application = "DevOpsCopilot.Application";
    private const string Infrastructure = "DevOpsCopilot.Infrastructure";
    private const string Worker = "DevOpsCopilot.Worker";

    /// <summary>Package name prefixes that must never be reachable from Domain or Application.</summary>
    private static readonly string[] ExternalServicePrefixes =
    [
        "Azure.",
        "Kusto.",
        "Microsoft.Azure.",
        "Microsoft.Agents.",
    ];

    [Fact]
    public void Every_layer_project_targets_net10_and_references_only_inwards()
    {
        // Arrange
        (string Project, string[] Allowed)[] layers =
        [
            (Domain, []),
            (Application, [Domain]),
            (Infrastructure, [Domain, Application]),
            // The host also wires the shared service defaults (health checks, OpenTelemetry).
            (Worker, [Domain, Application, Infrastructure, "DevOpsCopilot.ServiceDefaults"]),
        ];

        // Act
        var projects = layers
            .Select(layer => (layer.Project, layer.Allowed, File: Repository.Project($"src/{layer.Project}/{layer.Project}.csproj")))
            .ToArray();

        // Assert
        foreach (var (name, allowed, file) in projects)
        {
            Assert.Equal("net10.0", file.TargetFramework);
            Assert.Equal(allowed.Order(), file.ProjectReferences.Order());
        }
    }

    [Fact]
    public void Domain_references_no_external_framework()
    {
        // Arrange
        var project = Repository.Project($"src/{Domain}/{Domain}.csproj");

        // Act
        var referenced = ReferencedAssemblyNames(Domain);

        // Assert
        Assert.Empty(project.PackageReferences);
        Assert.DoesNotContain(referenced, IsExternalService);
        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name is "System.Text.Json" or "Newtonsoft.Json");
    }

    [Fact]
    public void Application_references_no_azure_sdk_or_agent_framework()
    {
        // Arrange
        var project = Repository.Project($"src/{Application}/{Application}.csproj");

        // Act
        var referenced = ReferencedAssemblyNames(Application);

        // Assert
        Assert.DoesNotContain(project.PackageReferences, IsExternalService);
        Assert.DoesNotContain(referenced, IsExternalService);
        Assert.Contains(Domain, project.ProjectReferences);
    }

    [Fact]
    public void The_azure_openai_client_lives_only_in_infrastructure()
    {
        // Arrange
        string[] openAIPackages = ["Azure.AI.OpenAI", "Azure.Identity", "Microsoft.Extensions.AI.OpenAI"];
        var infrastructure = Repository.Project($"src/{Infrastructure}/{Infrastructure}.csproj");
        string[] others =
        [
            $"src/{Domain}/{Domain}.csproj",
            $"src/{Application}/{Application}.csproj",
            $"src/{Worker}/{Worker}.csproj",
        ];

        // Act
        var elsewhere = others.Select(Repository.Project).ToArray();

        // Assert
        Assert.Contains("Azure.AI.OpenAI", infrastructure.PackageReferences);
        foreach (var project in elsewhere)
        {
            Assert.DoesNotContain(project.PackageReferences, openAIPackages.Contains);
        }

        Assert.DoesNotContain(ReferencedAssemblyNames(Application), name => name.Contains("OpenAI", StringComparison.Ordinal));
        Assert.DoesNotContain(ReferencedAssemblyNames(Domain), name => name.Contains("OpenAI", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_does_not_reference_application_or_infrastructure()
    {
        // Arrange
        var project = Repository.Project($"src/{Domain}/{Domain}.csproj");

        // Act
        var referenced = ReferencedAssemblyNames(Domain);

        // Assert
        Assert.DoesNotContain(Application, project.ProjectReferences);
        Assert.DoesNotContain(Infrastructure, project.ProjectReferences);
        Assert.DoesNotContain(Application, referenced);
        Assert.DoesNotContain(Infrastructure, referenced);
        Assert.DoesNotContain(Worker, referenced);
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_host()
    {
        // Arrange
        var project = Repository.Project($"src/{Infrastructure}/{Infrastructure}.csproj");

        // Act
        var referenced = ReferencedAssemblyNames(Infrastructure);

        // Assert
        Assert.DoesNotContain(Worker, project.ProjectReferences);
        Assert.DoesNotContain(Worker, referenced);
    }

    [Fact]
    public void Application_tests_reference_no_infrastructure_project()
    {
        // Arrange
        string[] inwardTestProjects =
        [
            $"tests/{Domain}.Tests/{Domain}.Tests.csproj",
            $"tests/{Application}.Tests/{Application}.Tests.csproj",
        ];

        // Act
        var projects = inwardTestProjects.Select(Repository.Project).ToArray();

        // Assert
        foreach (var project in projects)
        {
            Assert.DoesNotContain(Infrastructure, project.ProjectReferences);
            Assert.DoesNotContain(Worker, project.ProjectReferences);
            Assert.DoesNotContain(project.PackageReferences, IsExternalService);
        }
    }

    private static bool IsExternalService(string name) =>
        ExternalServicePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// The simple names of the assemblies the built layer assembly actually depends on. Loading by
    /// name works because every layer project is referenced by this test project, so its output
    /// sits next to the test assembly.
    /// </summary>
    private static string[] ReferencedAssemblyNames(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? "")
            .ToArray();
}
