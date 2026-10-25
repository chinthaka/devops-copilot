using System.Xml.Linq;

namespace DevOpsCopilot.Architecture.Tests;

/// <summary>
/// Reads the repository's project files so the layer rules can be asserted against what is
/// declared, not only against what the compiler happened to keep in the built assemblies.
/// </summary>
internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static ProjectFile Project(string relativePath) =>
        ProjectFile.Load(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static bool Exists(string relativePath) =>
        Directory.Exists(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
        || File.Exists(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static string ReadText(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No repository root containing AGENTS.md was found above '{AppContext.BaseDirectory}'.");
    }
}

internal sealed class ProjectFile
{
    private ProjectFile(string path, IReadOnlyList<string> projectReferences, IReadOnlyList<string> packageReferences, string? targetFramework)
    {
        Path = path;
        ProjectReferences = projectReferences;
        PackageReferences = packageReferences;
        TargetFramework = targetFramework;
    }

    public string Path { get; }

    /// <summary>Referenced project names, without the directory or the .csproj suffix.</summary>
    public IReadOnlyList<string> ProjectReferences { get; }

    public IReadOnlyList<string> PackageReferences { get; }

    public string? TargetFramework { get; }

    public static ProjectFile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The project file '{path}' does not exist.", path);
        }

        var document = XDocument.Load(path);
        var projectReferences = document
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => System.IO.Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToArray();
        var packageReferences = document
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();
        var targetFramework = document.Descendants("TargetFramework").FirstOrDefault()?.Value;

        return new ProjectFile(path, projectReferences, packageReferences, targetFramework);
    }
}
