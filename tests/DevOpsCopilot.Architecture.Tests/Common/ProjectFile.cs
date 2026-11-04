using System.Xml.Linq;

namespace DevOpsCopilot.Architecture.Tests.Common;

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
