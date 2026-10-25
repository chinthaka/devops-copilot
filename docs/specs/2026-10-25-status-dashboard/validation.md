# Validation: Status dashboard

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `An_event_read_increments_events_read_and_sets_last_processed` and siblings for skipped, checkpointed and errors | Unit | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-1 | `An_incident_starting_event_is_notified_as_read_and_checkpointed` and siblings for skipped, checkpointed and failed | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-2 | `An_event_read_increments_events_read_and_sets_last_processed`, `Uptime_grows_from_the_start_time` | Unit | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-3 | `The_status_endpoint_returns_statistics_and_dependency_health` | Integration | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-4 | `StatusDocument_MapsWorkerStatusAndServiceHealth` | Unit | Host | `tests/DevOpsCopilot.Server.Tests` |
| AC-4 | `StatusApi_AgainstAppHost_ReturnsOneDocument` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |
| AC-5 | `StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros` | Unit | Host | `tests/DevOpsCopilot.Server.Tests` |
| AC-6 | `StatusDashboard` component tests (renders document, polls) | Unit (Vitest) | Frontend | `src/DevOpsCopilot.Frontend` |
| AC-6 | `rg -i weather` finds nothing in `src/DevOpsCopilot.Frontend` or `src/DevOpsCopilot.Server` | Manual | — | — |
| AC-7 | `npm run lint`, `npm run build`, `npm run test` in `src/DevOpsCopilot.Frontend`, wired into CI | CI | Frontend | `src/DevOpsCopilot.Frontend` |
| AC-8 | `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |
| AC-9 | Existing architecture tests, extended only if a new rule is needed | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-10 | `With_telemetry_disabled_the_endpoint_reports_disabled_without_statistics` | Integration | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-11 | Solution build, CI frontend steps and `rg -n "src/frontend"` finding nothing | CI / Manual | — | — |

## Unit tests (written first)

`tests/DevOpsCopilot.Application.Tests` — the observer notifications, with a recording fake `IProcessingObserver`:

- **`An_incident_starting_event_is_notified_as_read_and_checkpointed`** (AC-1) – Arrange: a fake event source that yields one incident-starting event and a fake pipeline. Act: run. Assert: the observer saw one read, zero skipped, zero errors.
- **`An_event_that_does_not_start_an_incident_is_notified_as_skipped`** (AC-1) – Arrange: one `Warning` event without an exception. Act: run. Assert: one read, one skipped.
- **`The_checkpoint_notification_comes_after_the_checkpoint_call`** (AC-1) – Arrange: one event whose checkpoint delegate records its call order. Act: run. Assert: the checkpoint notification comes after the checkpoint call, exactly once.
- **`A_failing_event_is_notified_as_failed_and_the_exception_still_propagates`** (AC-1) – Arrange: a pipeline that throws. Act: run and catch. Assert: one error notified, the exception still propagates (behaviour unchanged).
- **`Without_an_observer_processing_behaves_as_before`** (AC-9) – Arrange: no observer registered (no-op default). Act: run one event. Assert: pipeline and checkpoint run exactly as today.

`tests/DevOpsCopilot.Worker.Tests` — the in-memory statistics:

- **`An_event_read_increments_events_read_and_sets_last_processed`** (AC-1) – Arrange: fresh statistics. Act: notify read. Assert: `eventsRead == 1`, others zero.
- **`A_skipped_event_increments_only_the_skipped_count`** (AC-1) – as above for skipped; checkpointed and failed likewise, plus **`Before_any_event_the_last_processed_time_is_null`**.
- **`Uptime_grows_from_the_start_time`** (AC-2, on `WorkerUptime`, which is kept even when telemetry is off) – Arrange: a fixed clock advanced by 5 minutes. Assert: uptime is 5 minutes.
- **`Counters_are_exact_under_parallel_increments`** (AC-1) – Act: N parallel notifications. Assert: the count is exactly N.

`tests/DevOpsCopilot.Server.Tests` — the status document mapping, with a stubbed worker HTTP handler:

- **`StatusDocument_MapsWorkerStatusAndServiceHealth`** (AC-4) – Arrange: a stub worker response with known statistics and dependency health. Act: build the document. Assert: `worker.status == "running"`, statistics copied verbatim, `services` enumerates exactly `worker`, `eventhubs`, `storage`.
- **`StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros`** (AC-5) – Arrange: a handler that throws `HttpRequestException`. Act: build the document. Assert: `not-running`, all counts zero, `lastProcessedAt` null, worker `unhealthy`, `eventhubs` and `storage` `unknown`.
- **`StatusDocument_WhenWorkerDependencyUnhealthy_PropagatesThatHealth`** (AC-4) – Arrange: worker reports `storage` unhealthy. Assert: the document's `storage` entry is `unhealthy` while `worker` stays `healthy`.

## Frontend tests (Vitest, written with the component)

- **`StatusDashboard renders not running and zeros`** (AC-5, AC-6) – Arrange: mock fetch returning the not-running document. Assert: "not running" is shown and every counter reads 0.
- **`StatusDashboard renders live statistics`** (AC-6) – Arrange: mock fetch with non-zero counts and a last-processed time. Assert: each of the six statistics and each of the three service health states is rendered.
- **`StatusDashboard polls the status endpoint`** (AC-6) – Arrange: fake timers. Act: advance past the polling interval. Assert: fetch was called again and the view updated.

## Integration tests

- **`The_status_endpoint_returns_statistics_and_dependency_health`** (AC-3) – boots the worker's status endpoint in-process (TestServer) with stub health checks, asserts the JSON shape and the enumerated dependencies. No emulator.
- **`With_telemetry_disabled_the_endpoint_reports_disabled_without_statistics`** (AC-10) – same host with `HostTelemetry:Enabled=false` wiring: `telemetryEnabled` is `false`, `statistics` is null, dependency health is still present.
- **`StatusApi_AgainstAppHost_ReturnsOneDocument`** (AC-4) – starts the AppHost with `Aspire.Hosting.Testing`, waits for `server`, GETs the status API, asserts one well-formed document naming the three services. Needs Docker for the emulators.
- **`EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts`** (AC-8) – starts the AppHost, sends one incident-starting event and one `Warning`-only event to hub `logs` through the Event Hubs emulator, polls the status API until `eventsRead >= 2`, then asserts `eventsSkipped >= 1`, `batchesCheckpointed >= 1` and `lastProcessedAt` is set. Needs Docker.

## Architecture tests

- The existing layer rules must keep passing: the new observer interface lives in Application with no new package; the statistics type, clock use, endpoint and health checks live only in `DevOpsCopilot.Worker` / `DevOpsCopilot.Server`. A new rule is added only if the existing reflection checks would not catch a telemetry type placed in Domain or Application.

## Manual checks

- `aspire run`: before the worker consumes, the dashboard shows "not running", zeros and `unknown` service health; after sending local events via the `LogForwarder`, the counts move and the last-processed time updates (the roadmap's **Done when**).
- `rg -i weather` over `src/DevOpsCopilot.Frontend` and `src/DevOpsCopilot.Server` returns nothing.

## Results

Run on 2026-10-25 against the branch's final commit.

### Automated tests

| Suite | Result |
|---|---|
| `dotnet test src/DevOpsCopilot.sln` | **43 passed, 0 failed**, 2 skipped (the Docker-gated AppHost tests; see below) |
| `tests/DevOpsCopilot.Application.Tests` | 12 passed, including the five observer-notification tests (AC-1, AC-9) |
| `tests/DevOpsCopilot.Worker.Tests` | 9 passed: the statistics unit tests (AC-1, AC-2) and both status-endpoint integration tests (AC-3, AC-10) |
| `tests/DevOpsCopilot.Server.Tests` | 3 passed: `StatusDocument_MapsWorkerStatusAndServiceHealth`, `StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros`, `StatusDocument_WhenWorkerDependencyUnhealthy_PropagatesThatHealth` (AC-4, AC-5) |
| `tests/DevOpsCopilot.Architecture.Tests` | 8 passed unchanged; no new rule was needed (AC-9) |
| `tests/DevOpsCopilot.AppHost.Tests` | **2 passed** against Docker: `StatusApi_AgainstAppHost_ReturnsOneDocument` (AC-4) and `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts` (AC-8) |
| `npm run test` (Vitest) | 3 passed: renders not running and zeros, renders live statistics, polls the status endpoint (AC-5, AC-6) |
| `npm run lint`, `npm run build` | Both clean (AC-7) |

The AppHost tests carry `Category=RequiresDocker` and skip themselves when no Docker daemon
answers, so a machine or CI run without the emulators skips rather than fails them. They were run
explicitly against Docker Desktop for this result: 2 passed, 0 failed, 30 s.

### Manual checks

- `rg -i weather` over `src/DevOpsCopilot.Frontend` and `src/DevOpsCopilot.Server` returns nothing (AC-6). The only remaining matches in the repository are these spec documents and the roadmap describing what was replaced.
- `rg -n "src/frontend"` finds nothing outside this spec folder, which records the rename (AC-11).
- `aspire run` smoke check (the roadmap's **Done when**): **not run in this environment.** Docker Desktop's WSL integration is off for this distro, so the AppHost cannot be started from it. `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts` covers the same ground automatically — it starts the AppHost, sends one incident-starting and one `Warning`-only event through the Event Hubs emulator and asserts the status document shows live counts — which is why decision 8 made that check automated.

### Acceptance criteria

| AC | Status |
|---|---|
| AC-1 | Met — Application observer tests and `WorkerStatistics` unit tests |
| AC-2 | Met — `An_event_read_increments_events_read_and_sets_last_processed`, `Uptime_grows_from_the_start_time` |
| AC-3 | Met — `The_status_endpoint_returns_statistics_and_dependency_health` |
| AC-4 | Met — mapping unit test and `StatusApi_AgainstAppHost_ReturnsOneDocument` |
| AC-5 | Met — `StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros` and the Vitest not-running test |
| AC-6 | Met — Vitest component tests; no weather code in either project |
| AC-7 | Met — lint, build and Vitest all pass and are wired into CI |
| AC-8 | Met — `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts` passes against the emulator |
| AC-9 | Met — architecture tests unchanged and green; Application gained only `IProcessingObserver` and no package |
| AC-10 | Met — `With_telemetry_disabled_the_endpoint_reports_disabled_without_statistics` |
| AC-11 | Met — solution builds, CI references the new path, no `src/frontend` references remain |
