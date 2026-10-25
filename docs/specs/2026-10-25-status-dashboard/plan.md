# Plan: Status dashboard

## Approach

The worker owns its statistics; everything else only reads them. A small `IProcessingObserver` interface in Application is called by `ProcessLogEvents` at the four points it already knows about (event read, skipped, checkpointed, failed), with a no-op default so nothing changes when no observer is registered. The host implements the observer as a thread-safe in-memory `WorkerStatistics` and exposes it, together with its own dependency health, on an HTTP status endpoint. `DevOpsCopilot.Server` resolves the worker over Aspire service discovery, merges worker status and the enumerated service health into one JSON document, and the React frontend polls and renders that document.

This keeps host telemetry out of the domain logic (decision 5 in [requirement.md](requirement.md)): Domain is untouched, Application gains only the observer abstraction, and all counting, clocks, HTTP and health-check code lives in the hosts. The alternative — counting inside the Event Hubs consumer in Infrastructure — was rejected because skipping is decided in Application, and the consumer would have to guess outcomes it does not own.

The worker currently uses `Host.CreateApplicationBuilder` and has no HTTP surface. It moves to the ASP.NET Core minimal host (`WebApplication.CreateBuilder`) and onto `ServiceDefaults`, keeping the `BackgroundService`, so it gets `/health` and the status endpoint the same way the server does. The AppHost gives the worker an HTTP endpoint and references it from the server.

## Task groups

Tests first within every group, per [AGENTS.md](../../AGENTS.md). Each group leaves the build and all tests green.

### 1. Rename the frontend (S, first)

1. Move `src/frontend` to `src/DevOpsCopilot.Frontend` and rename `frontend.esproj` to `DevOpsCopilot.Frontend.esproj`. The npm package becomes `devopscopilot.frontend` (npm forbids capitals).
2. Update the solution, the AppHost's `AddViteApp` path, the CI workflow paths, and every document that names the old path (AGENTS.md, roadmap, tech-stack, this spec).

### 2. M1.1 – Counters in the worker (S)

1. Add `IProcessingObserver` to Application (read, skipped, checkpointed, error) with a no-op default, and call it from `ProcessLogEvents`. Application unit tests from [validation.md](validation.md) first.
2. Create `tests/DevOpsCopilot.Worker.Tests` and write the `WorkerStatistics` unit tests, then the thread-safe `WorkerStatistics` (interlocked counters, `TimeProvider` for last-processed time and uptime) implementing the observer.
3. Move the worker to the ASP.NET Core minimal host with `ServiceDefaults`; map `/health` and the status endpoint serving the statistics plus the health-check results for its `eventhubs` and `storage` connections. The health checks are `IHealthCheck` implementations in Infrastructure (it owns the Azure SDKs); the worker only registers them.
4. Wire the `HostTelemetry:Enabled` flag (default `true`): enabled registers `WorkerStatistics` as the observer, disabled registers the no-op observer and the status endpoint reports `telemetryEnabled: false` with `statistics: null`.
5. Give the worker an HTTP endpoint in the AppHost. Integration tests `StatusEndpoint_ReturnsStatisticsAndDependencyHealth` and `StatusEndpoint_WhenTelemetryDisabled_ReportsDisabledWithoutStatistics`.

### 3. M1.2 – Status API (S)

1. Create `tests/DevOpsCopilot.Server.Tests` and write the mapping unit tests (running, unreachable, unhealthy dependency) against a stubbed HTTP handler.
2. In the server: a typed `HttpClient` for `http://worker` via service discovery, the status-document record types, the mapping (unreachable worker → `not-running`, zeros, `unknown` dependencies), and the status API endpoint. Remove the weather endpoint and the `WeatherForecast` record.
3. Reference the worker from the server in the AppHost. Create `tests/DevOpsCopilot.AppHost.Tests` on `Aspire.Hosting.Testing` with `StatusApi_AgainstAppHost_ReturnsOneDocument`.

### 4. M1.3 – Dashboard UI (S)

1. Add Vitest and React Testing Library to `src/DevOpsCopilot.Frontend`; write the three component tests from validation.md.
2. Build the dashboard: a polling hook (interval is a frontend constant), a status view showing worker running state, uptime, the six statistics and the three named service health states. Delete the weather screen and all weather code.
3. Wire `npm run test` into CI next to the existing lint and build steps.

### 5. End-to-end test through the emulator

1. In `tests/DevOpsCopilot.AppHost.Tests`, add `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts`: start the AppHost, produce one incident-starting event and one `Warning`-only event to hub `logs`, poll the status API until the counts are live, assert read, skipped, checkpointed and last-processed time.
2. Mark the AppHost tests as requiring Docker so CI and local runs without the emulators skip, not fail, them.

### 6. Documentation

1. Update [tech-stack.md](../tech-stack.md) per the table below.
2. Update [roadmap.md](../roadmap.md): mark M1 (and M1.1–M1.3) done once the **Done when** check passes.
3. Set this requirement's status to Done with the validation results filled in.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Application | `src/DevOpsCopilot.Application` | `IProcessingObserver` (+ no-op default); `ProcessLogEvents` calls it | No new package; dependencies still point inwards |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `IHealthCheck` implementations for the `eventhubs` and `storage` connections | Adds only `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` |
| Host | `src/DevOpsCopilot.Worker` | `WorkerStatistics`, status endpoint, minimal-host `Program.cs`, `ServiceDefaults` reference, health-check registration, `HostTelemetry:Enabled` wiring | All telemetry lives here |
| Host | `src/DevOpsCopilot.Server` | Status-document types, worker status client, status API endpoint; weather endpoint deleted | Reads, never writes, worker state |
| Frontend | `src/DevOpsCopilot.Frontend` | Status dashboard components, polling hook, Vitest setup; weather screen deleted | Polling interval is a constant |
| AppHost | `src/DevOpsCopilot.AppHost` | Worker HTTP endpoint; server references worker | |
| Tests | `tests/DevOpsCopilot.Worker.Tests`, `tests/DevOpsCopilot.Server.Tests`, `tests/DevOpsCopilot.AppHost.Tests` | New projects | Application tests extend the existing project |

Domain is untouched. The architecture tests' allowed-reference list for the worker gains `DevOpsCopilot.ServiceDefaults` (host wiring, same as the server).

## Interfaces

| Interface | Responsibility | Implemented by |
|---|---|---|
| `IProcessingObserver` (Application) | Receive the four processing outcomes as notifications; no-op by default | `WorkerStatistics` in `DevOpsCopilot.Worker` |

The server's worker-status client is internal to the server, not an Application interface: it is host-to-host plumbing, not domain behaviour.

## Contract changes

None. The dashboard observes [pipeline.md § 1](../../pipeline.md#1-receive) without changing it.

## Tech-stack changes

Applied to [tech-stack.md](../tech-stack.md) in this pull request (task group 6):

| Change | Where in tech-stack.md |
|---|---|
| Worker host model: ASP.NET Core minimal host carrying the `BackgroundService`, so it can expose `/health` and the status endpoint | Runtime and language → Host model |
| Frontend path: `src/DevOpsCopilot.Frontend` (renamed from `src/frontend`) | Dashboard, Leftover projects |
| `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` in Infrastructure for the dependency health checks | Solution structure |
| `Microsoft.AspNetCore.TestHost` for the worker status-endpoint tests | Testing |
| Dashboard section: the frontend is the status dashboard; the "currently the weather sample" note goes | Dashboard |
| Leftover projects: `DevOpsCopilot.Server` and `src/DevOpsCopilot.Frontend` leave the leftovers list; `ApiService`, `Functions`, `EmailNotifier` remain | Leftover projects |
| `Aspire.Hosting.Testing` for the AppHost and end-to-end tests (version pinned when added) | Testing |
| Vitest + React Testing Library for frontend component tests (versions pinned when added) | Testing / Dashboard |

## Configuration and secrets

- `HostTelemetry:Enabled` (bool, default `true`) in the worker's configuration toggles statistics collection; no secret involved.
- No new secrets. The worker's status endpoint path and the server's worker base address (`http://worker`, resolved by service discovery) are conventions, not configuration.
- The dashboard polling interval is a frontend constant, deliberately not configuration (decision 6).
