
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Queues;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddSingleton(ResolveQueueClient);
builder.Services.AddSingleton(ResolveTableClient);

//var credential = new DefaultAzureCredential();

// Aspire appends non-standard keys like ";QueueName=queues" or ";TableName=tables" to the
// injected Azurite connection string, which causes Azure SDK connection string parsing to fail.
static string StripAspireKeys(string raw, params string[] prefixes) =>
    string.Join(';', raw.Split(';').Where(p =>
        !string.IsNullOrEmpty(p) && !prefixes.Any(k => p.StartsWith(k, StringComparison.OrdinalIgnoreCase))));

static QueueServiceClient ResolveQueueClient(IServiceProvider sp)
{
    var raw = sp.GetRequiredService<IConfiguration>().GetConnectionString("queues") ?? "";
    if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        return new QueueServiceClient(uri, new Azure.Identity.DefaultAzureCredential());
    return new QueueServiceClient(StripAspireKeys(raw, "QueueName="));
}

static TableServiceClient ResolveTableClient(IServiceProvider sp)
{
    var raw = sp.GetRequiredService<IConfiguration>().GetConnectionString("tables") ?? "";
    if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        return new TableServiceClient(uri, new Azure.Identity.DefaultAzureCredential());
    return new TableServiceClient(StripAspireKeys(raw, "TableName="));
}

builder.Services.AddLogging(logging =>
{
	logging.AddJsonConsole(opts =>
	{
		opts.JsonWriterOptions = new() { Indented = false };
		opts.IncludeScopes = true;
	});
});


var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();

var loa = app.MapGroup("/loa").WithTags("Loa");

loa.MapPost("/", AddLoaRequest).WithName("SubmitLoaRequest");

loa.MapGet("/{trackingId}", GetLoaStatus).WithName("GetLoaStatus");

app.Run();


static async Task<IResult> AddLoaRequest(
	LoaRequest request,
	QueueServiceClient queueClient,
	TableServiceClient tableClient,
	ILogger<Program> logger,
	CancellationToken cancellationToken)
{

	if (string.IsNullOrWhiteSpace(request.MemeberEmail) ||
		string.IsNullOrWhiteSpace(request.AdviserName))
	{
		logger.LogWarning("Invalid request received: {Request}", request);

		return Results.ValidationProblem(new Dictionary<string, string[]>
		{
			["request"] = ["MemberEmail and AdviserName are required"]
		});
	}

	using var scope = logger.BeginScope(new Dictionary<string, object>
	{
		["AdviserName"] = request.AdviserName,
		["RequestType"] = "LoaSubmission"
	});

	IResult result;

	try
	{

		var trackingId = Guid.CreateVersion7().ToString("N");

		logger.LogInformation("LoA request received for adviser {Adviser}", request.AdviserName);

		var table = tableClient.GetTableClient("LoaAudit");
		await table.CreateIfNotExistsAsync(cancellationToken);
		await table.AddEntityAsync(new LoaAutidEntity
		{
			PartitionKey = DateTime.UtcNow.ToString("yyyy-MM-dd"),
			RowKey = trackingId,
			Status = "Received",
			AdviserName = request.AdviserName,
			MememberEmail = request.MemeberEmail,
			SchemeName = request.SchemeName ?? "(unspecified)"
		}, cancellationToken: cancellationToken);

		var queue = queueClient.GetQueueClient("loa-request");
		await queue.CreateIfNotExistsAsync();

		var payload = JsonSerializer.Serialize(new
		{
			TrackingId = trackingId,
			MemberEmail = request.MemeberEmail,
			AdviserName = request.AdviserName,
			SchemeName = request.SchemeName
		});
		await queue.SendMessageAsync(payload, cancellationToken);

		result = Results.Accepted($"/loa/{trackingId}", new { trackingId, status = "Received" });

		logger.LogInformation("LoA request {TrackingId} queued successfully", trackingId);

	}
	catch(Exception e)
	{
		result = Results.BadRequest("Please try again later");
		logger.LogError(e, "Error submitting LoA request");
	}

	return result;
}

static async Task<IResult> GetLoaStatus(
	string trackingId,
	TableServiceClient tableClient,
	CancellationToken cancellationToken)
{
	var table = tableClient.GetTableClient("LoaAudit");
	var results = table.QueryAsync<LoaAutidEntity>(e => e.RowKey == trackingId, null, null, cancellationToken).OrderBy(e => e.Timestamp);

	var events = new List<LoaAutidEntity>();

	await foreach (var entity in results)
	{
		events.Add(entity);
	}

	if (events.Count == 0)
	{
		return Results.NotFound();
	}

	return Results.Ok(new
	{
		trackingId,
		currentStatus = events[^1].Status,
		history = events
	});
}

public record LoaRequest(string AdviserName, string MemeberEmail, string? SchemeName);