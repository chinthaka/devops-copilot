using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using DevOpsCopilot.Infrastructure;
using DevOpsCopilot.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Composition root. Configuration is read only here; every layer below takes plain values.
var incident = builder.Configuration.GetSection("Incident").Get<IncidentOptions>() ?? new IncidentOptions();

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
    new ReportSettings(incident.DraftsDirectory));

builder.Services.AddSingleton(new LogQueryTargets(
    incident.Kusto.ExceptionsTable,
    incident.Kusto.DependenciesTable));
builder.Services.AddSingleton<GatherEvidence>();
builder.Services.AddSingleton<GroundInHistory>();
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
