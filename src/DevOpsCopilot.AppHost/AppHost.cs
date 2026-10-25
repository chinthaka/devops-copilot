var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.DevOpsCopilot_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../DevOpsCopilot.Frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

var eventHub = builder.AddAzureEventHubs("eventhubs")
    .RunAsEmulator()
    .AddHub("logs");
eventHub.AddConsumerGroup("devops-copilot");

var storage = builder
	.AddAzureStorage("storage")
	.RunAsEmulator(emulator =>
	{
		emulator.WithBlobPort(10000)
				.WithQueuePort(10001)
				.WithTablePort(10002);
		//.WithLifetime(ContainerLifetime.Persistent)
		//.WithDataVolume();


		IEnumerable<EndpointAnnotation> endpoints = emulator.Resource.Annotations
			.OfType<EndpointAnnotation>()
			.Where(endpoint => endpoint.Name is "blob" or "queue" or "table");

		foreach (EndpointAnnotation endpoint in endpoints)
		{
			endpoint.IsProxied = false;
		}

	});

var queues = storage.AddQueues("queues");
var tables = storage.AddTables("tables");
var checkpoints = storage.AddBlobs("checkpoints");


// Api porject - reference both queue and table connections
var api = builder.AddProject<Projects.DevOpsCopilot_ApiService>("api")
	.WithReference(queues)
	.WithReference(tables)
	.WithReference(eventHub, "eventhubs")
	.WaitFor(eventHub)
	.WaitFor(storage);


var worker = builder.AddProject<Projects.DevOpsCopilot_Worker>("worker")
	.WithReference(eventHub, "eventhubs")
	.WithReference(checkpoints, "checkpoints")
	.WaitFor(eventHub)
	.WaitFor(checkpoints);

// The server's status API reads the worker's status endpoint; no WaitFor, because the
// document must report not-running while the worker is down.
server.WithReference(worker);

builder.AddAzureFunctionsProject<Projects.DevOpsCopilot_Functions>("functions")
	.WithHostStorage(storage)
	.WithEnvironment("AzureWebJobsSecretStorageType", "Files")
	.WithReference(queues).WaitFor(queues)
	.WithReference(tables).WaitFor(tables)
	.WaitFor(storage);

builder.Build().Run();
