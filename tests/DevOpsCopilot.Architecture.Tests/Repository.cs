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
