using DevOpsCopilot.Infrastructure.Reporting.Email;
using Microsoft.Extensions.Configuration;

namespace DevOpsCopilot.Infrastructure.Tests.Reporting.Email;

/// <summary>
/// Reads the SMTP server and the addressing from user secrets or the environment, layered over
/// the worker's <c>appsettings.json</c> for the shipped port and security mode. Missing simply
/// means "not set". Any SMTP server will do; no local test tool is prescribed.
/// </summary>
internal static class SmtpConfiguration
{
    private static readonly Lazy<(EmailSettings, SmtpSettings)?> Lazy = new(Read);

    /// <summary>The addressing, or null unless both it and the SMTP server are set.</summary>
    public static EmailSettings? Settings => Lazy.Value?.Item1;

    public static SmtpSettings? Smtp => Lazy.Value?.Item2;

    private static (EmailSettings, SmtpSettings)? Read()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("worker.appsettings.json", optional: false)
            .AddUserSecrets(typeof(SmtpConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var section = configuration.GetSection("Incident:Notifications:Email");
        var smtp = section.GetSection("Smtp");
        var settings = new EmailSettings(
            section["SenderAddress"] ?? "",
            section.GetSection("Recipients").GetChildren().Select(child => child.Value ?? "").ToArray());
        if (!Enum.TryParse<SmtpSecurity>(smtp["Security"], ignoreCase: true, out var security))
        {
            return null;
        }

        var server = new SmtpSettings(
            smtp["Host"] ?? "",
            int.TryParse(smtp["Port"], out var port) ? port : 0,
            security,
            smtp["Username"] ?? "",
            smtp["Password"] ?? "");
        return settings.IsConfigured && server.IsConfigured ? (settings, server) : null;
    }
}
