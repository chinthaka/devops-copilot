# Validation: Status dashboard

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `WorkerStatistics_WhenEventRead_IncrementsEventsRead` and siblings for skipped, checkpointed and errors | Unit | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-1 | `ProcessLogEvents_WhenEventHandled_NotifiesObserverRead` and siblings for skipped, checkpointed and failed | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-2 | `WorkerStatistics_WhenEventRead_SetsLastProcessedAt`, `WorkerStatistics_UptimeGrowsFromStartTime` | Unit | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-3 | `StatusEndpoint_ReturnsStatisticsAndDependencyHealth` | Integration | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-4 | `StatusDocument_MapsWorkerStatusAndServiceHealth` | Unit | Host | `tests/DevOpsCopilot.Server.Tests` |
| AC-4 | `StatusApi_AgainstAppHost_ReturnsOneDocument` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |
| AC-5 | `StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros` | Unit | Host | `tests/DevOpsCopilot.Server.Tests` |
| AC-6 | `StatusDashboard` component tests (renders document, polls) | Unit (Vitest) | Frontend | `src/DevOpsCopilot.Frontend` |
| AC-6 | `rg -i weather` finds nothing in `src/DevOpsCopilot.Frontend` or `src/DevOpsCopilot.Server` | Manual | — | — |
| AC-7 | `npm run lint`, `npm run build`, `npm run test` in `src/DevOpsCopilot.Frontend`, wired into CI | CI | Frontend | `src/DevOpsCopilot.Frontend` |
| AC-8 | `EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |
| AC-9 | Existing architecture tests, extended only if a new rule is needed | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-10 | `StatusEndpoint_WhenTelemetryDisabled_ReportsDisabledWithoutStatistics` | Integration | Host | `tests/DevOpsCopilot.Worker.Tests` |
| AC-11 | Solution build, CI frontend steps and `rg -n "src/frontend"` finding nothing | CI / Manual | — | — |

## Unit tests (written first)

`tests/DevOpsCopilot.Application.Tests` — the observer notifications, with a recording fake `IProcessingObserver`:

- **`ProcessLogEvents_WhenEventHandled_NotifiesObserverRead`** (AC-1) – Arrange: a fake event source that yields one incident-starting event and a fake pipeline. Act: run. Assert: the observer saw one read, zero skipped, zero errors.
- **`ProcessLogEvents_WhenEventDoesNotStartIncident_NotifiesObserverSkipped`** (AC-1) – Arrange: one `Warning` event without an exception. Act: run. Assert: one read, one skipped.
- **`ProcessLogEvents_AfterCheckpoint_NotifiesObserverCheckpointed`** (AC-1) – Arrange: one event whose checkpoint delegate records its call order. Act: run. Assert: the checkpoint notification comes after the checkpoint call, exactly once.
- **`ProcessLogEvents_WhenPipelineThrows_NotifiesObserverErrorAndRethrows`** (AC-1) – Arrange: a pipeline that throws. Act: run and catch. Assert: one error notified, the exception still propagates (behaviour unchanged).
- **`ProcessLogEvents_WithoutObserver_BehavesAsBefore`** (AC-9) – Arrange: no observer registered (no-op default). Act: run one event. Assert: pipeline and checkpoint run exactly as today.

`tests/DevOpsCopilot.Worker.Tests` — the in-memory statistics:

- **`WorkerStatistics_WhenEventRead_IncrementsEventsRead`** (AC-1) – Arrange: fresh statistics. Act: notify read. Assert: `eventsRead == 1`, others zero.
- **`WorkerStatistics_WhenEventSkipped_IncrementsSkipped`** (AC-1) – as above for skipped; **`…Checkpointed…`** and **`…Error…`** likewise.
- **`WorkerStatistics_WhenEventRead_SetsLastProcessedAt`** (AC-2) – Arrange: a fixed clock. Act: notify read. Assert: `lastProcessedAt` equals the clock's time.
- **`WorkerStatistics_UptimeGrowsFromStartTime`** (AC-2) – Arrange: a fixed clock advanced by 5 minutes. Assert: uptime is 5 minutes.
- **`WorkerStatistics_IsThreadSafe_UnderParallelIncrements`** (AC-1) – Act: N parallel notifications. Assert: the count is exactly N.

`tests/DevOpsCopilot.Server.Tests` — the status document mapping, with a stubbed worker HTTP handler:

- **`StatusDocument_MapsWorkerStatusAndServiceHealth`** (AC-4) – Arrange: a stub worker response with known statistics and dependency health. Act: build the document. Assert: `worker.status == "running"`, statistics copied verbatim, `services` enumerates exactly `worker`, `eventhubs`, `storage`.
- **`StatusDocument_WhenWorkerUnreachable_ReportsNotRunningAndZeros`** (AC-5) – Arrange: a handler that throws `HttpRequestException`. Act: build the document. Assert: `not-running`, all counts zero, `lastProcessedAt` null, worker `unhealthy`, `eventhubs` and `storage` `unknown`.
- **`StatusDocument_WhenWorkerDependencyUnhealthy_PropagatesThatHealth`** (AC-4) – Arrange: worker reports `storage` unhealthy. Assert: the document's `storage` entry is `unhealthy` while `worker` stays `healthy`.

## Frontend tests (Vitest, written with the component)

- **`StatusDashboard renders not running and zeros`** (AC-5, AC-6) – Arrange: mock fetch returning the not-running document. Assert: "not running" is shown and every counter reads 0.
- **`StatusDashboard renders live statistics`** (AC-6) – Arrange: mock fetch with non-zero counts and a last-processed time. Assert: each of the six statistics and each of the three service health states is rendered.
- **`StatusDashboard polls the status endpoint`** (AC-6) – Arrange: fake timers. Act: advance past the polling interval. Assert: fetch was called again and the view updated.

## Integration tests

- **`StatusEndpoint_ReturnsStatisticsAndDependencyHealth`** (AC-3) – boots the worker's status endpoint in-process (TestServer) with stub health checks, asserts the JSON shape and the enumerated dependencies. No emulator.
- **`StatusEndpoint_WhenTelemetryDisabled_ReportsDisabledWithoutStatistics`** (AC-10) – same host with `HostTelemetry:Enabled=false` wiring: `telemetryEnabled` is `false`, `statistics` is null, dependency health is still present.
- **`StatusApi_AgainstAppHost_ReturnsOneDocument`** (AC-4) – starts the AppHost with `Aspire.Hosting.Testing`, waits for `server`, GETs the status API, asserts one well-formed document naming the three services. Needs Docker for the emulators.
- **`EndToEnd_EventsThroughEmulator_StatusShowsLiveCounts`** (AC-8) – starts the AppHost, sends one incident-starting event and one `Warning`-only event to hub `logs` through the Event Hubs emulator, polls the status API until `eventsRead >= 2`, then asserts `eventsSkipped >= 1`, `batchesCheckpointed >= 1` and `lastProcessedAt` is set. Needs Docker.

## Architecture tests

- The existing layer rules must keep passing: the new observer interface lives in Application with no new package; the statistics type, clock use, endpoint and health checks live only in `DevOpsCopilot.Worker` / `DevOpsCopilot.Server`. A new rule is added only if the existing reflection checks would not catch a telemetry type placed in Domain or Application.

## Manual checks

- `aspire run`: before the worker consumes, the dashboard shows "not running", zeros and `unknown` service health; after sending local events via the `LogForwarder`, the counts move and the last-processed time updates (the roadmap's **Done when**).
- `rg -i weather` over `src/DevOpsCopilot.Frontend` and `src/DevOpsCopilot.Server` returns nothing.

## Results

<!-- Filled in when the work is done. -->
