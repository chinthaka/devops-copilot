using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsCopilot.Infrastructure.Reporting;

/// <summary>
/// Maps each template name to its configured path and reads the file on every call, so an edited
/// template applies to the next event with no restart and no deployment (requirement D13). An
/// unset path is a silent null — the embedded default — and a missing or unreadable file is one
/// warning naming the path, then null, so a template problem never costs the report or the email.
/// </summary>
public sealed class FileTemplateSource : ITemplateSource
{
    private readonly TemplateSettings _settings;
    private readonly ILogger<FileTemplateSource> _logger;

    public FileTemplateSource(TemplateSettings settings, ILogger<FileTemplateSource>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? NullLogger<FileTemplateSource>.Instance;
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var path = name switch
        {
            TemplateNames.Report => _settings.ReportPath,
            TemplateNames.EmailSubject => _settings.EmailSubjectPath,
            TemplateNames.EmailHtml => _settings.EmailHtmlPath,
            TemplateNames.EmailText => _settings.EmailTextPath,
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "The {Name} template at {Path} could not be read ({Reason}); the embedded default is used.",
                name,
                path,
                exception.GetType().Name);
            return null;
        }
    }
}
