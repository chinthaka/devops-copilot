using DevOpsCopilot.Domain.Events;
using DevOpsCopilot.Domain.Grounding;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Domain.Reporting;
using DevOpsCopilot.Infrastructure;
using DevOpsCopilot.Infrastructure.Reporting;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting;

public class FileReportWriterTests
{
    [Fact]
    public async Task WriteAsync_ForADraft_NamesTheFileFromTheEventDayAndId()
    {
        // Arrange
        using var drafts = new DraftDirectory();
        var writer = new FileReportWriter(new ReportSettings(drafts.Path));
        var diagnostic = UnavailableDiagnostic();
        var grounding = new GroundingHandoff(diagnostic.EventId, "unavailable", PostmortemOverlap.IndexName, [""], []);
        var draft = IncidentDraft.Render(diagnostic, grounding);

        // Act
        var path = await writer.WriteAsync(diagnostic, grounding, draft, CancellationToken.None);

        // Assert
        Assert.Equal(Path.Combine(drafts.Path, "2026-10-20-evt-20261020100000000.md"), path);
        Assert.True(File.Exists(path));
        Assert.Equal(draft, await File.ReadAllTextAsync(path));
    }

    private static DiagnosticHandoff UnavailableDiagnostic()
    {
        return new DiagnosticHandoff(
            EventIdentity.EventId(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero)),
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "00f067aa0ba902b7",
            "Checkout.Api",
            "TimeoutException: timed out",
            "unavailable",
            [],
            [],
            []);
    }

    private sealed class DraftDirectory : IDisposable
    {
        public DraftDirectory()
        {
            Path = Directory.CreateTempSubdirectory("devops-copilot-incidents").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
