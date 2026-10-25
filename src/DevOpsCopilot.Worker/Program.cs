using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using DevOpsCopilot.Infrastructure;
using DevOpsCopilot.Worker;

var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();
host.Run();
