using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace DevOpsCopilot.AppHost.Tests;

/// <summary>
/// Starts the AppHost once for the whole test class, and only when a test actually asks for it,
/// so a run where every test is skipped for want of Docker starts no containers at all.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DistributedApplication? _app;

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
    }
}
