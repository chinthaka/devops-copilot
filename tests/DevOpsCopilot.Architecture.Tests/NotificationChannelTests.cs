using DevOpsCopilot.Architecture.Tests.Common;
using System.Reflection;

namespace DevOpsCopilot.Architecture.Tests;

/// <summary>
/// The M6 channel rules: channel packages and HTTP clients live only in Infrastructure (AC-13),
/// and Application never names a channel, so a new channel is a new implementation and no edit to
/// the stage (AC-12).
/// </summary>
public class NotificationChannelTests
{
    private const string Domain = "DevOpsCopilot.Domain";
    private const string Application = "DevOpsCopilot.Application";
    private const string Infrastructure = "DevOpsCopilot.Infrastructure";
    private const string Worker = "DevOpsCopilot.Worker";

    [Fact]
    public void NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure()
    {
        // Arrange
        string[] notificationPackages = ["Azure.Communication.Email", "MailKit", "Microsoft.Extensions.Http"];
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
        foreach (var package in notificationPackages)
        {
            Assert.Contains(package, infrastructure.PackageReferences);
        }

        foreach (var project in elsewhere)
        {
            Assert.DoesNotContain(project.PackageReferences, notificationPackages.Contains);
        }
    }

    [Fact]
    public void Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk()
    {
        // Arrange
        var project = Repository.Project($"src/{Application}/{Application}.csproj");

        // Act
        var referenced = Assembly.Load(Application)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? "")
            .ToArray();

        // Assert
        Assert.DoesNotContain(project.PackageReferences, name => name.StartsWith("Azure.", StringComparison.Ordinal));
        Assert.DoesNotContain(project.PackageReferences, name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Azure.", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("System.Net.Http", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.Extensions.Http", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_WhenInspectedForChannelNames_MentionsNoChannelType()
    {
        // Arrange: the stage resolves channels, it never names one (AC-12).
        string[] channelNames = ["Teams", "Slack", "Discord", "Workflows", "CommunicationServices", "Graph", "Smtp", "MailKit", "MimeKit"];
        var assembly = Assembly.Load(Application);

        // Act
        var names = assembly.GetTypes()
            .SelectMany(type => type.GetMembers(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => member.Name)
                .Append(type.Name))
            .ToArray();

        // Assert
        foreach (var channel in channelNames)
        {
            Assert.DoesNotContain(names, name => name.Contains(channel, StringComparison.OrdinalIgnoreCase));
        }
    }
}
