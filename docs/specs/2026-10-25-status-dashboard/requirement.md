# Requirement: Status dashboard

| | |
|---|---|
| **Branch** | `features/2026-10-25-status-dashboard` |
| **Milestone** | M1 – Status dashboard, covering M1.1, M1.2 and M1.3 ([roadmap](../roadmap.md#m1--status-dashboard-m)) |
| **Status** | Done ([validation results](validation.md#results)) |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-10-25 |

## Summary

Replace the weather sample UI with a status dashboard that shows whether the worker is running, the health of the AppHost services, and the worker's processing statistics. One branch and this one spec deliver all three roadmap sub-milestones: counters in the worker (M1.1), the status API in the server (M1.2) and the dashboard UI (M1.3).

## Why

The mission puts "a dashboard showing the worker's status and processing statistics" in scope ([mission.md § Boundaries](../mission.md#boundaries)). Today the frontend is still the Aspire weather sample, listed as a known gap in the roadmap and closed by M1. Without the dashboard there is no way to see, at a glance, whether the worker is consuming hub `logs` and what it has done.

## Scope

**In scope**

- Six in-memory statistics in the worker: **events read**, **events skipped**, **batches checkpointed**, **errors**, **time of the last processed event**, and **uptime**.
  - *Events read* counts every event received from hub `logs`.
  - *Events skipped* counts the subset that does not start an incident (for example `Warning` without an exception).
  - *Batches checkpointed* counts successful checkpoint calls.
  - *Errors* counts events whose handling threw; existing error behaviour does not change, the failure is only counted.
  - *Uptime* is the time since the worker process started.
- An HTTP status endpoint on the worker exposing those statistics and the health of the worker's own dependencies.
- A status API on `DevOpsCopilot.Server` that reads the worker over HTTP via Aspire service discovery and serves one JSON status document.
- The status document enumerates these specific services with a health state each: **worker**, **eventhubs** (Event Hubs), **storage** (Azurite / Blob Storage). Health states are `healthy`, `degraded`, `unhealthy` or `unknown`.
- A React dashboard that renders the status document and polls it. The weather forecast screen and the server's weather endpoint are removed.
- Until the worker is consuming hub `logs` (not started, or unreachable), the document reports status **not running**, all counts **zero**, no last-processed time, and dependency health `unknown`.
- A configuration flag that turns host telemetry off. When off, the worker collects no statistics (the no-op observer is wired) and its status endpoint reports telemetry as disabled while still serving dependency health. Default is on.
- Renaming the frontend to follow the project naming pattern: `src/DevOpsCopilot.Frontend` becomes `src/DevOpsCopilot.Frontend`, with the solution, AppHost, CI and documents updated. Done first, before M1.1.
- An automated end-to-end test that starts the AppHost, drives events through the Event Hubs emulator and asserts the status document shows live counts.
- Vitest component tests for the dashboard, alongside ESLint and the frontend build.

**Out of scope**

- Persisting statistics across worker restarts. The counters are in-memory and reset to zero on restart.
- Aggregating statistics across replicas (roadmap M17) and the triage counters "candidates opened" and "events collapsed" (roadmap M11.3).
- Authentication or authorisation on the status endpoints.
- Any change to how events are parsed, piped or checkpointed. The pipeline's behaviour is observed, not altered.
- Removing the remaining sample projects `ApiService`, `Functions` and `EmailNotifier` (roadmap M18). Only the weather code in `src/DevOpsCopilot.Frontend` and `DevOpsCopilot.Server` is replaced.

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | Processing outcomes observed as the worker handles hub events (read, skipped, checkpointed, failed), plus health-check results for the worker's Event Hubs and storage dependencies | [pipeline.md § 1 – Receive](../../pipeline.md#1-receive) |
| **Output** | One JSON status document served by `DevOpsCopilot.Server` and rendered by the dashboard | See below |

The status document:

```json
{
  "worker": {
    "status": "running | not-running",
    "telemetryEnabled": true,
    "uptimeSeconds": 0,
    "statistics": {
      "eventsRead": 0,
      "eventsSkipped": 0,
      "batchesCheckpointed": 0,
      "errors": 0,
      "lastProcessedAt": null
    }
  },
  "services": [
    { "name": "worker", "status": "healthy | degraded | unhealthy | unknown" },
    { "name": "eventhubs", "status": "…" },
    { "name": "storage", "status": "…" }
  ],
  "generatedAt": "2026-10-25T00:00:00Z"
}
```

When host telemetry is disabled, `worker.telemetryEnabled` is `false` and `worker.statistics` is `null`; service health is still reported.

## Contract and rules touched

- [pipeline.md § 1 – Receive](../../pipeline.md#1-receive): the statistics observe the receive stage (events read, skipped, checkpointed). No contract change; the dashboard is read-only over what the pipeline already does.

## Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | One branch and one spec for all of M1.1–M1.3 | Agreed with the roadmap's split; the sub-milestones are the numbered task groups in [plan.md](plan.md). |
| 2 | Statistics include errors and uptime in addition to the roadmap's four | Requested; both are cheap to keep and make a dead or failing worker visible. |
| 3 | Services are enumerated by name (`worker`, `eventhubs`, `storage`) rather than a generic health roll-up | Requested; the dashboard must show which service is unhealthy, not just that one is. |
| 4 | The server reads the worker over HTTP via Aspire service discovery | Requested; the shared state store does not exist until Phase 2 (M9), and HTTP keeps the worker the single owner of its own counters. |
| 5 | Host telemetry stays out of the domain logic: the statistics type, the endpoint and the health checks live in the host (`DevOpsCopilot.Worker`); Application only gains a small observer interface it calls with outcomes it already knows | Requested separation; dependencies keep pointing inwards and the architecture tests keep passing. |
| 6 | The dashboard polling interval is an implementation detail | Requested; a constant in the frontend, not configuration. |
| 7 | `DevOpsCopilot.Server` and `src/DevOpsCopilot.Frontend` stop being "weather sample, do not extend": M1 replaces their content with the product dashboard | Requested; [tech-stack.md](../tech-stack.md) is updated in this pull request. |
| 8 | End-to-end coverage is automated: a test boots the AppHost, produces events into the emulator and asserts the status document | Requested; `aspire run` remains only a manual smoke check. |
| 9 | Frontend behaviour is tested with Vitest component tests | Requested; added to the tech stack in this pull request. |
| 10 | A configuration flag (`HostTelemetry:Enabled`, default `true`) toggles host telemetry | Requested; a client running this on their own cloud infrastructure may want statistics collection off. Off means the no-op observer: zero collection cost, status endpoint reports telemetry disabled, health still served. |
| 11 | The frontend directory follows the project naming pattern: `src/DevOpsCopilot.Frontend` | Requested; done before M1.1 so every later change lands on the final path. The npm package name is `devopscopilot.frontend` because npm forbids capital letters. |

## Acceptance criteria

- **AC-1** The worker keeps the six statistics in memory, and the counters move when an event is handled, skipped, checkpointed or fails.
- **AC-2** The worker records the time of the last processed event and reports uptime from its start time.
- **AC-3** The worker exposes an HTTP status endpoint returning its statistics and the health of its `eventhubs` and `storage` dependencies.
- **AC-4** `DevOpsCopilot.Server` serves one JSON status document combining worker status, statistics, uptime and the enumerated health of `worker`, `eventhubs` and `storage`, resolving the worker over HTTP via Aspire service discovery.
- **AC-5** When the worker is unreachable the document reports `not-running`, zero counts, `lastProcessedAt: null`, worker `unhealthy` and dependency health `unknown`.
- **AC-6** The frontend renders the status document and polls it; no weather code remains in `src/DevOpsCopilot.Frontend` and the server has no weather endpoint.
- **AC-7** ESLint, the frontend build and the Vitest component tests pass.
- **AC-8** An automated end-to-end test starts the AppHost, sends events to hub `logs` through the emulator, and the status document shows non-zero live counts.
- **AC-9** The architecture tests keep passing: no telemetry type leaks into Domain, and Application gains no package or outward dependency.
- **AC-10** With `HostTelemetry:Enabled` set to `false` the worker collects no statistics, and the status endpoint reports `telemetryEnabled: false` with `statistics: null` while still serving dependency health. The default is enabled.
- **AC-11** The frontend lives at `src/DevOpsCopilot.Frontend`; the solution, AppHost, CI and documents reference the new path and the repository builds and lints clean.
