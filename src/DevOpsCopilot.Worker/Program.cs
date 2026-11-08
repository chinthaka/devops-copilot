using DevOpsCopilot.Application.Events;
using DevOpsCopilot.Application.Grounding;
using DevOpsCopilot.Application.Interfaces;
using DevOpsCopilot.Application.Kql;
using DevOpsCopilot.Application.Pipeline;
using DevOpsCopilot.Application.Reporting;
using DevOpsCopilot.Domain.Kql;
using DevOpsCopilot.Infrastructure;
using DevOpsCopilot.Infrastructure.Diagnoses;
using DevOpsCopilot.Infrastructure.Events;
using DevOpsCopilot.Infrastructure.Grounding;
using DevOpsCopilot.Infrastructure.Kql;
using DevOpsCopilot.Infrastructure.Reporting;
using DevOpsCopilot.Worker;
using DevOpsCopilot.Worker.Options;
using DevOpsCopilot.Worker.Status;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Composition root. Configuration is read only here; every layer below takes plain values.
var incident = builder.Configuration.GetSection("Incident").Get<IncidentOptions>() ?? new IncidentOptions();

// Timings and retry caps have no defaults in code; appsettings.json supplies them. A missing or
// non-positive value stops the worker at startup instead of silently running unbounded.
RequirePositive(incident.AzureOpenAI.TimeoutSeconds, "Incident:AzureOpenAI:TimeoutSeconds");
RequirePositive(incident.Notifications.TimeoutSeconds, "Incident:Notifications:TimeoutSeconds");
RequirePositive(incident.Notifications.Discord.MaxRetryDelaySeconds, "Incident:Notifications:Discord:MaxRetryDelaySeconds");
if (string.IsNullOrWhiteSpace(incident.Notifications.Slack.PostMessageUrl))
{
    throw new InvalidOperationException("Configuration value Incident:Notifications:Slack:PostMessageUrl is required.");
}

builder.Services.AddInfrastructure(
    new EventHubSettings(
        builder.Configuration.GetConnectionString("eventhubs") ?? "",
        builder.Configuration.GetConnectionString("checkpoints") ?? ""),
    new KustoSettings(
        incident.Kusto.ClusterUri,
        incident.Kusto.Database,
        incident.Kusto.ExceptionsTable,
        incident.Kusto.DependenciesTable),
    new SearchSettings(
        incident.Search.Endpoint,
        incident.Search.Index,
        incident.Search.ApiKey),
    new ReportSettings(incident.DraftsDirectory),
    new AzureOpenAISettings(
        incident.AzureOpenAI.Endpoint,
        incident.AzureOpenAI.Deployment,
        incident.AzureOpenAI.ApiKey,
        incident.AzureOpenAI.TimeoutSeconds),
    new TeamsSettings(incident.Notifications.Teams.WebhookUrl),
    new SlackSettings(
        incident.Notifications.Slack.BotToken,
        incident.Notifications.Slack.ChannelId,
        incident.Notifications.Slack.PostMessageUrl),
    new DiscordSettings(
        incident.Notifications.Discord.WebhookUrl,
        incident.Notifications.Discord.MaxRetryDelaySeconds),
    new EmailSettings(
        incident.Notifications.Email.ConnectionString,
        incident.Notifications.Email.Endpoint,
        incident.Notifications.Email.SenderAddress,
        incident.Notifications.Email.Recipients.ToArray()),
    new TemplateSettings(
        incident.Report.TemplatePath,
        incident.Notifications.Email.SubjectTemplatePath,
        incident.Notifications.Email.HtmlTemplatePath,
        incident.Notifications.Email.TextTemplatePath));

builder.Services.AddSingleton(new LogQueryTargets(
    incident.Kusto.ExceptionsTable,
    incident.Kusto.DependenciesTable));
builder.Services.AddSingleton<GatherEvidence>();
builder.Services.AddSingleton<GroundInHistory>();

// Stage 7's fan-out: the notifiers come from AddInfrastructure, the timeout from configuration.
builder.Services.AddSingleton(provider => new NotifyChannels(
    provider.GetServices<INotifier>(),
    TimeSpan.FromSeconds(incident.Notifications.TimeoutSeconds),
    provider.GetRequiredService<ILogger<NotifyChannels>>()));

// Stage 6 is registered by AddInfrastructure: the Azure OpenAI model when the deployment is
// configured, and the deterministic path when it is not.
builder.Services.AddSingleton<RunIncidentPipeline>();
builder.Services.AddSingleton<ProcessLogEvents>();
builder.Services.AddHostedService<Worker>();

// Host telemetry. Off means the no-op observer: nothing is collected, health is still served.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WorkerUptime>();
if (builder.Configuration.GetValue("HostTelemetry:Enabled", true))
{
    builder.Services.AddSingleton<WorkerStatistics>();
    builder.Services.AddSingleton<IProcessingObserver>(
        provider => provider.GetRequiredService<WorkerStatistics>());
}
else
{
    builder.Services.AddSingleton<IProcessingObserver>(NullProcessingObserver.Instance);
}

builder.Services.AddHealthChecks()
    .AddCheck<EventHubsHealthCheck>("eventhubs", tags: [WorkerStatusEndpoint.DependencyTag])
    .AddCheck<CheckpointStorageHealthCheck>("storage", tags: [WorkerStatusEndpoint.DependencyTag]);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapWorkerStatus();

app.Run();

static void RequirePositive(int value, string key)
{
    if (value <= 0)
    {
        throw new InvalidOperationException($"Configuration value {key} must be a positive number of seconds.");
    }
}
