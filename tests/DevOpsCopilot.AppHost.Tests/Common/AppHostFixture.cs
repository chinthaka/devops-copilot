using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace DevOpsCopilot.AppHost.Tests.Common;

/// <summary>
/// Starts the AppHost once for the whole test collection, and only when a test actually asks for
/// it, so a run where every test is skipped for want of Docker starts no containers at all. The
/// worker's drafts directory is redirected to a fresh temporary directory, so a test that waits
/// for a report file sees only files written by this run.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DistributedApplication? _app;

    public string ReportsDirectory { get; } =
        Directory.CreateTempSubdirectory("devops-copilot-apphost-incidents").FullName;

    public async Task<DistributedApplication> StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_app is not null)
            {
                return _app;
            }

            var builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.DevOpsCopilot_AppHost>(cancellationToken);
            var worker = builder.Resources.OfType<ProjectResource>()
                .Single(resource => resource.Name == "worker");
            worker.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                context.EnvironmentVariables["Incident__DraftsDirectory"] = ReportsDirectory));
            var app = await builder.BuildAsync(cancellationToken);
            await app.StartAsync(cancellationToken);
            _app = app;
            return app;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _gate.Dispose();
        if (Directory.Exists(ReportsDirectory))
        {
            Directory.Delete(ReportsDirectory, recursive: true);
        }
    }
}
