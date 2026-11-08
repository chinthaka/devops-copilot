using DevOpsCopilot.Server.Status;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// The status API reads the worker over HTTP; service discovery resolves http://worker. The short
// timeout caps the retries the shared resilience handler adds: a status probe should report the
// worker as not running quickly rather than hold the dashboard's poll open.
builder.Services.AddSingleton(TimeProvider.System);
var workerBaseAddress = builder.Configuration["WorkerStatus:BaseAddress"];
var workerTimeoutSeconds = builder.Configuration.GetValue<int>("WorkerStatus:TimeoutSeconds");
if (string.IsNullOrWhiteSpace(workerBaseAddress) || workerTimeoutSeconds <= 0)
{
    throw new InvalidOperationException(
        "Configuration values WorkerStatus:BaseAddress and a positive WorkerStatus:TimeoutSeconds are required.");
}

builder.Services.AddHttpClient<WorkerStatusClient>(client =>
{
    client.BaseAddress = new Uri(workerBaseAddress);
    client.Timeout = TimeSpan.FromSeconds(workerTimeoutSeconds);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api");
api.MapGet("status", (WorkerStatusClient worker, CancellationToken cancellationToken) =>
    worker.GetStatusDocumentAsync(cancellationToken))
.WithName("GetStatus");

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
