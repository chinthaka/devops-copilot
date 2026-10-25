using System.Globalization;
using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;

namespace DevOpsCopilot.Infrastructure;

/// <summary>
/// Writes the draft to the local drafts directory as <c>YYYY-MM-DD-&lt;event-id&gt;.md</c>. Used
/// when no report channel is configured.
/// </summary>
public sealed class FileReportWriter : IReportWriter
{
    private readonly ReportSettings _settings;

    public FileReportWriter(ReportSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> WriteAsync(
        DiagnosticHandoff diagnostic,
        GroundingHandoff grounding,
        string draft,
        CancellationToken cancellationToken)
    {
        var timestamp = EventIdentity.ParseEventId(diagnostic.EventId);
        var directory = _settings.DraftsDirectory;
        Directory.CreateDirectory(directory);
        var day = timestamp.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, day + "-" + diagnostic.EventId + ".md");
        await File.WriteAllTextAsync(path, draft, cancellationToken);
        return path;
    }
}
