namespace DevOpsCopilot.AppHost.Tests.Common;

/// <summary>
/// One AppHost (and one set of emulator containers) shared by every Docker-gated test class, and
/// no parallel runs against it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "AppHost";
}
