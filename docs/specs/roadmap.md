# Roadmap

The order of work for DevOpsCopilot, in small milestones. Each milestone is one or a few feature branches, each with its own spec folder under `docs/specs/YYYY-MM-DD-feature-name/`. A milestone is done only when its **Done when** check passes.

This file sets the order. It does not change the rules in [docs/pipeline.md](../pipeline.md), [mission.md](mission.md) or [tech-stack.md](tech-stack.md). If a milestone needs a rule to change, the spec changes the rule in the same pull request.

Sizes are relative: **S** fits in one short feature branch, **M** is a few days of focused work, **L** is a week or more and should be split further when its spec is written.

Last updated: 2026-10-25.

## Where the code is now

Built and merged on `main`:

- Aspire AppHost with the Event Hubs emulator (hub `logs`, consumer group `devops-copilot`), Azurite (blob container `checkpoints`), and the sample API, Functions and weather frontend from the template.
- `DevOpsCopilot.LogForwarder`, an OpenTelemetry exporter that JSON-encodes application logs to hub `logs`. Wired through `ServiceDefaults` when Aspire supplies the `eventhubs` connection. Local test producer only.
- Four projects in strict Clean Architecture: `DevOpsCopilot.Domain`, `DevOpsCopilot.Application`, `DevOpsCopilot.Infrastructure` and the `net10.0` worker `DevOpsCopilot.Worker`. Architecture tests fail the build when a layer references something it must not.
- The worker consumes hub `logs`, reads `TraceId` and `SpanId`, runs the pipeline for each event, and checkpoints to Blob Storage after handling. On restart it resumes from the checkpoint.
- `RunIncidentPipeline` in Application runs the evidence stage, the grounding stage and the report stage in sequence for **one log event**. `Warning` without an exception does not start it. The three agents are Agent Framework types in Infrastructure, behind the Application interfaces.
- Read-only Kusto queries (three fixed questions, `.` commands rejected) and Azure AI Search over `historical-postmortems` (five-hit cap, overlap filter). Without configuration both report `unavailable` and return nothing.
- A draft written to `incidents/YYYY-MM-DD-<event-id>.md`, which is gitignored.
- Tests: hub event parsing, checkpoint resume, warning does not start the team, both tools unavailable, five-hit cap, draft sections, and a KQL string containing `.drop` is not executed.
- CI on pull requests and `main`: Release build, tests, frontend lint and build.
- Spec templates in `docs/specs/_template/` and the pipeline contract in `docs/pipeline.md`.

Known gaps between the code and the contract, closed by the milestones below:

| Gap | Where | Closed by |
|---|---|---|
| The draft has Slack, Teams and **Bicep** sections, and the agent is named "remediation". The contract forbids infrastructure code and defines seven report sections with a diagnosis in the middle. | `RemediationAgent.cs` | M4 |
| Handoffs are keyed by `eventId`; the contract keys them by `incidentId` | `DiagnosticHandoff`, `GroundingHandoff` | M12, M13 |
| No LLM is wired; agents are deterministic | `InfrastructureServiceCollectionExtensions.cs` | M5 |
| Only the `LogForwarder` JSON shape is parsed, not diagnostic-settings `records` arrays | `LogEventParser.cs` | M10 |
| No triage, correlation, notification, work item or learn stage | — | M6, M11 to M16 |
| Nothing deploys to Azure | `.github/workflows/` | M7 |
| The dashboard is still the weather sample | `src/frontend`, `src/DevOpsCopilot.Server` | M1 |

---

## Phase 0 – Foundations (done)

**M0 – Walking skeleton.** Event in, draft out, locally, with the tools reporting `unavailable`. Done: everything under "Where the code is now".

---

## Phase 1 – Straight-through path

Goal: one error event in production produces one trustworthy report in Teams, from a deployed Azure environment, with diagnosis quality measured in CI. No triage or correlation yet; one event is one report.

### M1 – Status dashboard (M)

Replace the weather UI with a dashboard that shows whether the worker is running, the health of the AppHost services, and processing statistics: events read, events skipped, batches checkpointed, and the time of the last processed event. Until the daemon is consuming hub `logs`, status is "not running" and the counts are zero.

Split into:

1. **M1.1 – Counters in the worker (S).** The daemon keeps the four statistics in memory and exposes them on a health or status endpoint. Tests prove the counters move when events are handled and skipped.
2. **M1.2 – Status API (S).** `DevOpsCopilot.Server` reads the daemon's status and service health and serves them as one JSON document. Unit tests for the mapping; integration test against the AppHost.
3. **M1.3 – Dashboard UI (S).** The React frontend renders the status document and polls it. The weather forecast screen is gone. ESLint and the build pass.

**Done when:** `aspire run` opens a dashboard that shows "not running" and zeros before the daemon starts, then live counts once it consumes `logs`. No weather code remains in `src/frontend`.

### M2 – Prove the consumer (S, done)

Tests for parsing a hub log event and for resuming after a checkpoint already exist, in `tests/DevOpsCopilot.Infrastructure.Tests` and `tests/DevOpsCopilot.Application.Tests`. Remaining: one test that a record which cannot be parsed is logged and skipped without stopping the consumer, per the contract.

**Done when:** the unparseable-record test passes alongside the existing parse and resume tests.

### M3 – Clean Architecture split (M, done)

Split `DevOpsCopilot.Daemon` into `DevOpsCopilot.Domain`, `DevOpsCopilot.Application`, `DevOpsCopilot.Infrastructure` and `DevOpsCopilot.Worker`, as described in [AGENTS.md](../../AGENTS.md#architecture). Behaviour does not change; all existing tests keep passing.

Split into:

1. **M3.1 – Create the projects and architecture tests (S, done).** Four empty projects with the correct references, plus `tests/DevOpsCopilot.Architecture.Tests` that fail when a layer references something it must not. The tests pass on empty projects.
2. **M3.2 – Move the domain and the interfaces (S, done).** The event, handoffs and value objects go to Domain; `IEventSource`, `ILogQuery`, `IKnowledgeSearch`, `IReportWriter` and the stage orchestration go to Application. Existing tests are moved to `tests/DevOpsCopilot.Domain.Tests` and `tests/DevOpsCopilot.Application.Tests`.
3. **M3.3 – Move the implementations and the host (S, done).** Event Hubs consumer, Kusto, AI Search and the Agent Framework agents go to Infrastructure; `Program.cs` and DI go to Worker. The AppHost references Worker. `DevOpsCopilot.Daemon` is deleted.

**Done when:** `dotnet build` and `dotnet test` are clean, the architecture tests pass, Application has no Azure SDK or Agent Framework package reference, and the AppHost runs the Worker.

### M4 – Report matches the contract (S)

Rename the remediation agent to the diagnosis agent and make the written report follow the seven sections in [pipeline.md § 7](../pipeline.md#7-report-and-notify): header, impact, likely cause with confidence, evidence, similar incidents, suggested steps, open questions. Remove the Bicep section and the resource-change detection. For a single event, impact is the event's resource and a one-event window.

**Done when:** the draft test asserts the seven contract sections, no test or code mentions Bicep, and the agent's instructions forbid infrastructure code.

### M5 – Azure OpenAI diagnosis (M)

Wire an `IChatClient` for Azure OpenAI into the diagnosis agent so it produces the structured `Diagnosis` object from [pipeline.md § 6](../pipeline.md#6-diagnose-diagnosis-agent): title, likely cause, confidence, evidence citations, suggested steps and open questions. Confidence is `low` when both KQL and search are `unavailable`. Without an Azure OpenAI configuration the agent falls back to the deterministic report and says so.

Split into:

1. **M5.1 – Diagnosis type and renderer (S).** The `Diagnosis` value type in Domain and a renderer that turns it into the seven-section report. Unit tests, no LLM.
2. **M5.2 – Chat client wiring and prompt (S).** `IDiagnosisModel` in Application, the Azure OpenAI implementation in Infrastructure, configuration from Aspire or user secrets. Integration test behind a configuration flag.
3. **M5.3 – Citation check (S).** A guard rejects a diagnosis whose claims cite a finding, row or hit that does not exist, and falls back to `low` confidence with an open question. Unit tests.

**Done when:** with Azure OpenAI configured, a staged error event produces a report whose every claim cites real evidence; without it, the report says the model was unavailable.

### M6 – Teams notification (M)

`INotifier` in Application with a Microsoft Teams implementation that posts one Adaptive Card per report. An unconfigured channel is recorded as `unavailable` and does not fail the run. When no channel is configured the report is still written to `incidents/`.

Split into:

1. **M6.1 – Notifier interface and card renderer (S).** Card JSON built from the `Diagnosis`; unit tests check the text matches the diagnosis.
2. **M6.2 – Teams delivery (S).** Webhook or Graph delivery, configuration, `unavailable` handling, one message per report. Integration test against a test channel behind a flag.

**Done when:** a staged error event in a configured environment produces one Teams card whose text matches the report file.

### M7 – Deploy to Azure (M)

A GitHub Actions deployment workflow that runs after CI passes on `main` and deploys the AppHost resources to Azure Container Apps: Event Hubs with hub `logs`, the worker, the dashboard and storage for checkpoints. Connection strings and keys stay in GitHub or Azure configuration. The workflow never deploys anything from `incidents/`.

Split into:

1. **M7.1 – Publish manifest (S).** `aspire publish` or `azd` output committed as configuration, with the leftover sample projects excluded from publishing.
2. **M7.2 – Deployment workflow (S).** The GitHub Actions job, environment secrets, and a smoke check that the worker is healthy after deploy.
3. **M7.3 – Connect real sources (S, operations).** Diagnostic settings on at least one Azure resource and one Application Insights instance stream to the deployed hub. Log Analytics and Azure AI Search endpoints are configured for the worker.

**Done when:** a push to `main` produces a running Azure environment, and a real error from a connected resource produces a report.

### M8 – Diagnosis evaluation set (M)

A set of staged incidents with known causes, each a recorded event plus canned KQL rows and search hits, and a CI job that runs the diagnosis agent over them and scores the result. A prompt change that lowers the score fails the build.

Split into:

1. **M8.1 – Staged incident format and runner (S).** The fixture shape, five staged incidents, a runner that uses fake tools. Runs without an LLM and checks the deterministic path.
2. **M8.2 – Scoring and CI gate (S).** A score per incident (cause named, evidence cited, no invented facts), a threshold, and a CI job that runs when Azure OpenAI secrets are present.

**Done when:** CI reports an evaluation score on every pull request that touches the agents or their prompts, and a deliberately broken prompt fails it.

**Phase 1 is complete** when M1 to M8 are done. The product then delivers a Teams report per error event from a deployed environment, with measured quality.

---

## Phase 2 – Full incident workflow

Goal: hundreds of related errors become one incident, the agents reason about the incident rather than a single event, the report goes to every configured channel and a work item, and confirmed reports feed future diagnoses.

### M9 – Shared state store (S)

Choose the state store (Azure Table Storage or Cosmos DB), record the choice in [tech-stack.md](tech-stack.md), and add `IStateStore` in Application with an Infrastructure implementation and an in-memory test double. Triage and correlation state must survive a restart and be shared across replicas.

**Done when:** an integration test writes state from one worker instance and reads it from another against the emulator.

### M10 – Normalised events from diagnostic settings (M)

Parse the `records` array that Azure Monitor and Application Insights diagnostic settings write to Event Hubs, and map each record to the normalised event in [pipeline.md § 1](../pipeline.md#1-receive), with `source` set to `platform` or `application`. Keep the `LogForwarder` JSON shape working for local runs. A record that cannot be parsed is logged and skipped.

Split into:

1. **M10.1 – Application Insights records (S).** Traces, exceptions and requests categories to normalised events, with `traceId` and `spanId` from the record.
2. **M10.2 – Platform records (S).** Resource logs to normalised events with `resourceId` and `operationName`; `traceId` is null.

**Done when:** recorded samples of both payload shapes parse into normalised events in unit tests, and a mixed batch checkpoints only after every record is handled.

### M11 – Triage (M)

Events become candidates only when `level` is `Error` or `Critical` or `exception` is set. Fingerprint by exception type (or operation name), category, resource and a message with numbers, GUIDs and timestamps removed. Events with the same fingerprint within 5 minutes increment one candidate. State lives in the state store.

Split into:

1. **M11.1 – Fingerprint value object (S).** Pure function with table-driven unit tests for the normalisation rules.
2. **M11.2 – Candidate aggregation (S).** The 5-minute window, counts, first and last seen, trace and resource lists, backed by `IStateStore`.
3. **M11.3 – Dashboard counters (S).** Candidates opened and events collapsed appear on the dashboard.

**Done when:** a burst of 500 identical errors in two minutes produces one candidate with count 500, and `Warning` alone produces none.

### M12 – Correlate into incidents (L, split when specified)

The `Incident` aggregate with the lifecycle `open` → `settling` → `ready` → `investigating` → `reported` → `resolved`, deterministic `incidentId`, the join rules (shared `traceId`, or matching resource within 10 minutes) and the ready rule (90 seconds quiet or 5 minutes after opening). Domain events for each lifecycle change. `IIncidentRepository` in Application.

Split into:

1. **M12.1 – Incident aggregate and value objects (S).** `IncidentId`, `TraceId`, `ResourceId`, lifecycle methods and domain events, pure unit tests.
2. **M12.2 – Join and ready rules (S).** A correlation use case that attaches candidates and decides readiness from a clock abstraction.
3. **M12.3 – Repository and replay safety (S).** The repository over the state store; a replayed candidate after a restart does not open a duplicate incident.
4. **M12.4 – Late candidates (S).** A candidate arriving after `reported` attaches to the incident and marks the report for update.

**Done when:** two errors sharing a trace open one incident that becomes ready 90 seconds after the last one, and a restart and replay produce the same `incidentId`.

### M13 – Incident-based diagnosis (M)

The three agents take an `Incident` instead of an event. The diagnostic agent runs the allowed KQL questions from [pipeline.md § 4](../pipeline.md#4-gather-evidence-diagnostic-agent) with one initial round and at most two follow-ups, at most eight queries, rows capped. Handoffs carry `incidentId`. Grounding overlaps on the incident's exception types, categories, resources and services.

Split into:

1. **M13.1 – Handoff shapes (S).** Replace `eventId` keys with `incidentId`, add `round` to KQL entries, update tests.
2. **M13.2 – Bounded follow-up rounds (S).** Round and query counters enforced in Application, not in the agent prompt. A ninth query is refused.
3. **M13.3 – Evaluation set update (S).** Staged incidents become multi-event; the CI gate keeps passing.

**Done when:** an incident with three traced errors yields one report with impact across services, and no incident ever runs more than eight queries.

### M14 – Work items (M)

`IWorkItemClient` in Application with an Azure DevOps implementation. One work item per incident holding the full report; the Teams card links to it. Updates to the report update the work item.

**Done when:** a reported incident has exactly one work item whose description matches the report, and the Teams card links to it.

### M15 – Slack channel (S)

A Slack `INotifier` implementation posting one mrkdwn message per incident, updated in place when the report changes. Text matches the diagnosis and hits.

**Done when:** a reported incident produces one Slack message that matches the Teams card and the report.

### M16 – Learn loop (M)

When the work item is resolved and an engineer confirms or corrects the likely cause, index a `confirmed-report` document into `historical-postmortems` and move the incident to `resolved`. Unconfirmed reports are never indexed. DevOpsCopilot never resolves an incident on its own.

Split into:

1. **M16.1 – Confirmation input (S).** Read the confirmation from the work item (a field or comment convention) into a domain command. Unit tests.
2. **M16.2 – Index the confirmed report (S).** `IKnowledgeIndexer` in Application, AI Search implementation, the document shape from [pipeline.md § 8](../pipeline.md#8-learn). Grounding prefers `confirmed-report` hits.

**Done when:** a confirmed incident appears as a `confirmed-report` hit in the grounding of the next similar incident, and an unconfirmed one does not.

### M17 – Production readiness (M)

Two worker replicas behave identically. Message updates go to the existing Teams and Slack messages. Health checks cover the state store, Event Hubs and the configured channels. Alerts on consumer lag and on `unavailable` rates.

**Done when:** a load test with two replicas produces no duplicate incidents, reports or messages, and the dashboard shows replica-wide statistics.

### M18 – Remove leftover projects (S)

Delete `DevOpsCopilot.ApiService`, `DevOpsCopilot.Functions` and `DevOpsCopilot.EmailNotifier`, and their AppHost registrations and CI steps, once nothing depends on them. Keep `LogForwarder` as the local test producer until a spec replaces it.

**Done when:** the solution has no sample projects, CI is green, and `aspire run` still starts the worker, dashboard and emulators.

**Phase 2 is complete** when M9 to M18 are done.

---

## Later, not scheduled

- Import existing post-mortems into `historical-postmortems` as `postmortem` documents (an operations task once the index schema is final).
- Operator-triggered actions, such as re-running a diagnosis or re-sending a report, only on explicit request and never a resource change.
- A direct OpenTelemetry exporter for applications without Application Insights.
- AWS and GCP implementations of the Application interfaces.

## Out of scope

See [mission.md](mission.md#boundaries). In short: automated remediation or any generated infrastructure code, a fourth agent, KQL that changes data, `Warning` alone as an incident, and committing `incidents/`.

## Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Microsoft Agent Framework API churn | Rework in Infrastructure | Agents sit behind Application interfaces (M3); pin versions in `tech-stack.md`. |
| LLM invents evidence | Wrong report, lost trust | Citation guard (M5.3), evaluation gate (M8), `low` confidence when tools are unavailable. |
| Diagnostic-settings payloads vary by category | Parsing gaps | Record real samples per category as test fixtures (M10); skip and log unparseable records. |
| Correlation rules produce too many or too few incidents | Noise or missed context | Rules are pure functions with table tests (M12); thresholds live in configuration. |
| State store choice made late | Rework of triage and correlation | Decide in M9 before M11. |
| Azure cost of KQL and LLM calls | Budget overrun | Query and round limits enforced in code (M13.2), evaluation runs only when secrets are present. |
| Leftover sample projects confuse contributors | Wasted effort | Marked do-not-extend now; removed in M18. |

## Working on a milestone

1. Branch `features/YYYY-MM-DD-name-of-the-feature` from `main`.
2. Copy `docs/specs/_template/` to `docs/specs/YYYY-MM-DD-name-of-the-feature/` and write `requirement.md`, `validation.md` and `plan.md`. Name the milestone in the requirement header.
3. Follow [AGENTS.md](../../AGENTS.md): tests first, one feature per branch, update this file's status when the milestone's check passes.
