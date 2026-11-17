using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Infrastructure.Tests.Common;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class FileTemplateSourceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("devops-copilot-templates").FullName;

    [Fact]
    public async Task GetAsync_WithAConfiguredPath_RereadsTheFileOnEveryCall()
    {
        // Arrange
        var path = Path.Combine(_directory, "report.md");
        await File.WriteAllTextAsync(path, "First wording: {{Title}}");
        var source = new FileTemplateSource(new TemplateSettings(path, "", "", ""));

        // Act
        var first = await source.GetAsync(TemplateNames.Report, CancellationToken.None);
        await File.WriteAllTextAsync(path, "Edited wording: {{Title}}");
        var second = await source.GetAsync(TemplateNames.Report, CancellationToken.None);

        // Assert: the edit applies to the next render with no restart — the no-deployment guarantee.
        Assert.Equal("First wording: {{Title}}", first);
        Assert.Equal("Edited wording: {{Title}}", second);
    }

    [Fact]
    public async Task GetAsync_WithNoConfiguredPath_ReturnsNull()
    {
        // Arrange
        var logger = new RecordingLogger<FileTemplateSource>();
        var source = new FileTemplateSource(TemplateSettings.Unconfigured, logger);

        // Act
        var template = await source.GetAsync(TemplateNames.Report, CancellationToken.None);

        // Assert: unset means the embedded default, silently.
        Assert.Null(template);
        Assert.Empty(logger.Lines);
    }

    [Fact]
    public async Task GetAsync_WithAMissingFile_WarnsOnceAndReturnsNull()
    {
        // Arrange
        var path = Path.Combine(_directory, "does-not-exist.md");
        var logger = new RecordingLogger<FileTemplateSource>();
        var source = new FileTemplateSource(new TemplateSettings(path, "", "", ""), logger);

        // Act
        var template = await source.GetAsync(TemplateNames.Report, CancellationToken.None);

        // Assert
        Assert.Null(template);
        var (level, line) = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains(path, line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithAnUnknownName_ReturnsNull()
    {
        // Arrange
        var source = new FileTemplateSource(TemplateSettings.Unconfigured);

        // Act and assert
        Assert.Null(await source.GetAsync("not-a-template", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WithEachEmailName_ReadsItsOwnFile()
    {
        // Arrange
        var subject = Path.Combine(_directory, "subject.txt");
        var html = Path.Combine(_directory, "body.html");
        var text = Path.Combine(_directory, "body.txt");
        await File.WriteAllTextAsync(subject, "s");
        await File.WriteAllTextAsync(html, "h");
        await File.WriteAllTextAsync(text, "t");
        var source = new FileTemplateSource(new TemplateSettings("", subject, html, text));

        // Act and assert
        Assert.Equal("s", await source.GetAsync(TemplateNames.EmailSubject, CancellationToken.None));
        Assert.Equal("h", await source.GetAsync(TemplateNames.EmailHtml, CancellationToken.None));
        Assert.Equal("t", await source.GetAsync(TemplateNames.EmailText, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
