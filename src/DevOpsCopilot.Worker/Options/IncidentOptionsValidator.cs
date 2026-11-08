using DevOpsCopilot.Infrastructure.Reporting.Email;
using DevOpsCopilot.Infrastructure.Reporting;

namespace DevOpsCopilot.Worker.Options;

/// <summary>
/// The startup checks for values that have no default in code (the 2026-11-07 revision and
/// decisions D14 and D17 of the M6 spec). A missing or invalid value stops the worker with an
/// error naming its key, instead of running unbounded or with email silently switched off. No
/// message ever contains a secret's value.
/// </summary>
public static class IncidentOptionsValidator
{
    private const string Email = "Incident:Notifications:Email";

    public static void Validate(IncidentOptions options)
    {
        RequirePositive(options.AzureOpenAI.TimeoutSeconds, "Incident:AzureOpenAI:TimeoutSeconds");
        RequirePositive(options.Notifications.TimeoutSeconds, "Incident:Notifications:TimeoutSeconds");
        RequirePositive(options.Notifications.Discord.MaxRetryDelaySeconds, "Incident:Notifications:Discord:MaxRetryDelaySeconds");
        if (string.IsNullOrWhiteSpace(options.Notifications.Slack.PostMessageUrl))
        {
            throw new InvalidOperationException("Configuration value Incident:Notifications:Slack:PostMessageUrl is required.");
        }

        var email = options.Notifications.Email;
        var transport = Parse<EmailTransportKind>(email.Transport, $"{Email}:Transport");

        var smtp = email.Smtp;
        if (string.IsNullOrEmpty(smtp.Username) != string.IsNullOrEmpty(smtp.Password))
        {
            throw new InvalidOperationException(
                $"Configuration values {Email}:Smtp:Username and {Email}:Smtp:Password must be set together or not at all.");
        }

        if (transport == EmailTransportKind.Smtp)
        {
            Parse<SmtpSecurity>(smtp.Security, $"{Email}:Smtp:Security");
            if (smtp.Port <= 0)
            {
                throw new InvalidOperationException($"Configuration value {Email}:Smtp:Port must be a positive port number.");
            }
        }
    }

    /// <summary>
    /// Reads an enum by name, ignoring case. Numbers are refused even though
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> would accept them, so <c>"1"</c>
    /// can never select a transport.
    /// </summary>
    public static T Parse<T>(string value, string key)
        where T : struct, Enum
    {
        var name = Enum.GetNames<T>().FirstOrDefault(name => name.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null
            ? Enum.Parse<T>(name)
            : throw new InvalidOperationException(
                $"Configuration value {key} must be one of {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static void RequirePositive(int value, string key)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Configuration value {key} must be a positive number of seconds.");
        }
    }
}
