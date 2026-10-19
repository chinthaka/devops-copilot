var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.DevOpsCopilot_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

var eventHub = builder.AddAzureEventHubs("eventhubs")
    .RunAsEmulator()
    .AddHub("logs");

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


// Api porject - reference both queue and table connections
var api = builder.AddProject<Projects.DevOpsCopilot_ApiService>("api")
	.WithReference(queues)
	.WithReference(tables)
	.WaitFor(storage);


builder.AddProject<Projects.DevOpsCopilot_Daemon>("daemon");

builder.AddAzureFunctionsProject<Projects.DevOpsCopilot_Functions>("functions")
	.WithHostStorage(storage)
	.WithEnvironment("queues", "UseDevelopmentStorage=true").WaitFor(queues)
	.WithEnvironment("tables", "UseDevelopmentStorage=true").WaitFor(tables)
	.WaitFor(storage);

builder.Build().Run();
