using DevOpsCopilot.Daemon;
using DevOpsCopilot.Daemon.Incident;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddIncidentWorkflow(builder.Configuration);
builder.Services.AddSingleton<IEventHubLogConsumer, EventHubLogConsumer>();
builder.Services.AddSingleton<LogEventProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
