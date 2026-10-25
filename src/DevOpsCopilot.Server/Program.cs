using DevOpsCopilot.Server;

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
builder.Services.AddHttpClient<WorkerStatusClient>(client =>
{
    client.BaseAddress = new Uri("http://worker");
    client.Timeout = TimeSpan.FromSeconds(5);
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
