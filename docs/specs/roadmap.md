# Roadmap

The order of work for DevOpsCopilot, in small milestones. Each milestone is one or a few feature branches, and each branch has its own spec folder in the specs folder, named with the date, the milestone number and a short feature name (for example "2026-11-04-m4-report-contract"). A milestone is done only when its **Done when** check passes.

This file sets the order. It does not change the rules in [the pipeline contract](../pipeline.md), [mission.md](mission.md) or [tech-stack.md](tech-stack.md). If a milestone needs a rule to change, its spec changes the rule in the same pull request.

Sizes are relative: **S** fits in one short feature branch, **M** is a few days of focused work, **L** is a week or more and should be split further when its spec is written.

Last updated: 2026-11-20.

## Where the product is now

Built and merged:

- A local development environment that starts everything with one command: an emulated Azure Event Hubs stream, emulated storage for progress checkpoints, and the sample projects that came with the starter template.
- A log forwarder that sends application logs to the event stream. It is used only to produce test events locally.
- The code is split into four layers (core rules, use cases, connections to outside services, and the running host). Automated checks fail the build if one layer depends on another in the wrong direction.
- The worker reads events from the stream, starts an investigation for each error event, and records its progress so that after a restart it carries on where it stopped. A warning without an error does not start an investigation.
- For one log event, the worker gathers evidence, looks for similar past incidents, produces a diagnosis and writes a report, in that order. The diagnosis comes from Azure OpenAI when it is configured, with a check that rejects claims without real evidence. Without Azure OpenAI the worker produces a simpler rule-based diagnosis and says so.
- Read-only log queries limited to three fixed questions, with any query that could change data refused. A search of past incidents limited to five results, keeping only results that genuinely overlap with the current error. When either service is not configured, the report says it was unavailable and shows no results.
- A report saved as a local file, which is never committed to the repository.
- A status dashboard showing whether the worker is running and how many events it has handled.
- Automated tests for reading events, resuming after a restart, ignoring warnings, unavailable services, the five-result limit, the report sections and refusing a query that would delete data.
- Continuous integration on every pull request and on the main branch: build, tests, and the dashboard's checks and build.
- Spec templates and [the pipeline contract](../pipeline.md).

| Gap | Where | Closed by |
|---|---|---|
| ~~The draft has Slack, Teams and **Bicep** sections, and the agent is named "remediation".~~ Closed: M4 renamed the agent to `DiagnosisAgent` and deleted the superseded draft | `Agents/DiagnosisAgent.cs` | M4 (done) |
| Handoffs are keyed by `eventId`; the contract keys them by `incidentId` | `DiagnosticHandoff`, `GroundingHandoff` | M12, M13 |
| ~~No LLM is wired; agents are deterministic~~ Closed: M5 wired Azure OpenAI behind `IDiagnosisModel`, with the citation guard and the deterministic fallback | `InfrastructureServiceCollectionExtensions.cs` | M5 (done) |
| Only the `LogForwarder` JSON shape is parsed, not diagnostic-settings `records` arrays | `LogEventParser.cs` | M10 |
| No triage, correlation, work item or learn stage | — | M11 to M14, M16 |
| ~~A report is written to a file and delivered to nobody~~ Closed: M6 notifies Teams, Slack, Discord and email through `NotifyChannels` | `RunIncidentPipeline`, `NotifyChannels` | M6 (done) |
| ~~Email can only be sent through Azure Communication Services~~ Closed: `SmtpEmailTransport` is selectable by configuration | `CommunicationServicesEmailTransport`, `SmtpEmailTransport` | M6.7 (done) |
| Nothing deploys to Azure | `.github/workflows/` | M7 |
| The worker sends its own logs into the stream it reads, even locally, and keeps reading them back, about a thousand a minute. Real test events wait behind them until they time out, which is why the end-to-end notification test fails. Found on 2026-11-20. | The shared service defaults | M7.1 (decision D4 of the M7 spec) |
| ~~The dashboard is still the weather sample~~ Closed: the status dashboard replaced it | `src/DevOpsCopilot.Frontend`, `src/DevOpsCopilot.Server` | M1 (done) |

---

## Phase 0 – Foundations (done)

**M0 – Walking skeleton.** An event goes in and a report comes out, locally, with the outside services reported as unavailable. Done: everything under "Where the product is now".

---

## Phase 1 – Straight-through path

Goal: one error event in production produces one trustworthy report in every configured channel, from a deployed Azure environment, with diagnosis quality measured in CI. No triage or correlation yet; one event is one report.

### M1 – Status dashboard (M, done)

Replace the weather sample with a dashboard that shows whether the worker is running, the health of the supporting services, and processing statistics: events read, events skipped, progress checkpoints saved, and the time of the last processed event. Until the worker is reading events, the status is "not running" and the counts are zero.

Delivered by [2026-10-25-status-dashboard](2026-10-25-status-dashboard/requirement.md).

Split into:

1. **M1.1 – Statistics in the worker (S, done).** The worker keeps the statistics (the four above, plus errors and uptime) and makes them available to the dashboard. The feature can be switched off in configuration. Tests prove the counts change when events are handled and skipped.
2. **M1.2 – Status service (S, done).** The dashboard's back end combines the worker's statistics and the health of the services into one status summary. Tested on its own and against the running local environment.
3. **M1.3 – Dashboard screen (S, done).** The dashboard shows the status summary and refreshes it regularly. The weather screen is gone. The dashboard's checks, tests and build pass.

**Done when:** starting the local environment opens a dashboard that shows "not running" and zeros before the worker starts, then live counts once it reads events. No weather sample remains.

### M2 – Prove the event reader (S, done)

Tests for reading an event and for resuming after a restart already exist. Remaining: one test that an event which cannot be read is logged and skipped without stopping the worker, as the contract requires.

**Done when:** the unreadable-event test passes alongside the existing reading and resume tests.

### M3 – Split into layers (M, done)

Split the single worker project into the four layers described in [AGENTS.md](../../AGENTS.md#architecture). Behaviour does not change, and all existing tests keep passing.

Split into:

1. **M3.1 – Create the layers and the layer checks (S, done).** Four empty layers that depend on each other correctly, plus automated checks that fail when a layer depends on something it must not.
2. **M3.2 – Move the core rules and use cases (S, done).** The business concepts go into the core layer; the stage order and the descriptions of what each outside service must do go into the use-case layer. Existing tests move with them.
3. **M3.3 – Move the connections and the host (S, done).** The connections to Event Hubs, log queries, search and the agents move to the connections layer; start-up and wiring move to the host. The old single project is deleted.

**Done when:** the build and all tests are clean, the layer checks pass, the use-case layer has no dependency on Azure or the agent framework, and the local environment runs the new host.

### M4 – Report matches the contract (S, done)

Scope reduced by decision [D1 of M5](2026-10-25-m5-diagnosis-agent/requirement.md#decisions): M5 delivered the seven-section report described in [the contract's report section](../pipeline.md#7-report-and-notify) and the worker already writes it, so M4 removes what that replaced. Rename the remediation agent to the diagnosis agent, give it instructions that forbid changing resources, delete the old unused report with its Slack, Teams and infrastructure-template sections, and remove remaining mentions of remediation and infrastructure templates.

Delivered by [2026-11-04-m4-report-contract](2026-11-04-m4-report-contract/requirement.md).

**Done when:** only the seven-section report remains, nothing in the product is called the remediation agent, the agent's instructions forbid changing resources, and infrastructure as code is mentioned only in [Out of scope](#out-of-scope) and in historical specs.

### M5 – Azure OpenAI diagnosis (M, done)

Connect Azure OpenAI to the diagnosis agent so that it produces the structured diagnosis described in [the contract's diagnosis section](../pipeline.md#6-diagnose-diagnosis-agent): title, likely cause, confidence, evidence cited, suggested steps and open questions. Confidence is low when neither log queries nor search were available. Without Azure OpenAI configured, the agent falls back to the rule-based report and says so.

Delivered by [2026-10-25-m5-diagnosis-agent](2026-10-25-m5-diagnosis-agent/requirement.md).

Split into:

1. **M5.1 – Diagnosis and report layout (S, done).** The diagnosis and a way to turn it into the seven-section report. Tested without an AI model.
2. **M5.2 – Connect the model and write the prompt (S, done).** Azure OpenAI connected through the connections layer, with settings from the local environment or private developer settings. An optional test against a real model.
3. **M5.3 – Evidence check (S, done).** A diagnosis that cites a finding, query result or search result that does not exist is rejected; the report falls back to low confidence with an open question. Tested.

**Done when:** with Azure OpenAI configured, a test error event produces a report in which every claim cites real evidence; without it, the report says the model was unavailable.

### M6 – Notification channels (L, done)

Deliver the report to people. `INotifier` in Application and a `NotifyChannels` stage that calls every registered channel in a fixed order, with four implementations in Infrastructure: Microsoft Teams, Slack, Discord and email. Every channel is optional: an unconfigured one is recorded as `unavailable`, a failing one as `failed`, and neither stops the others or fails the run. The report is always written to `incidents/` before any channel is called, and is the only delivery when nothing is configured.

Scope extended again on 2026-11-08: email can be sent through **SMTP** as well as Azure Communication Services, chosen per deployment in configuration (M6.7), so an organisation without Communication Services can use the SMTP relay it already runs.

Scope extended on 2026-11-05 beyond the original Teams-only milestone: **Slack moves here from M15**, and Discord and email are added. Email is sent to every configured address for every report, so an engineer outside the chat workspace still gets the report. The four channels are one milestone because they share the notification, the interface, the fan-out, the failure rules and the truncation rules; only the payload and the transport differ.

Specified by [2026-11-05-m6-notification-channels](2026-11-05-m6-notification-channels/requirement.md).

Split into:

1. **M6.1 – Notification, interface and fan-out (S, done).** `IncidentNotification` in Domain built from the `Diagnosis` and the handoffs, `INotifier` in Application, and the `NotifyChannels` stage with its ordering, timeout and failure rules. Also the template renderer and the default report template, added on 2026-11-06 (decisions D12 and D13). No network, no channel, no package; unit tests only.
2. **M6.2 – Teams (S, done).** Adaptive Card posted to the trigger URL of a Teams Workflows "Post to a channel when a webhook request is received" flow (decision D3, revised on 2026-11-06 from the original Graph design; D11 withdrawn). The flow answers `202` with no message id, so Teams messages cannot be updated in place by M15. Unit tests against a stub handler; integration test behind `Category=RequiresTeams`.
3. **M6.3 – Slack (S, done).** Block Kit message posted with `chat.postMessage` and a bot token, chosen over an incoming webhook because only it can later update the message. A `200` carrying `"ok": false` is a failure.
4. **M6.4 – Discord (S, done).** Embed posted to a channel webhook with `?wait=true` so the message id comes back. One `Retry-After` retry on `429`.
5. **M6.5 – Email (S, done).** One message per report to every configured address through Azure Communication Services, HTML plus plain text, rendered from templates a deployment can replace without a new image.
6. **M6.6 – Wiring, contract and docs (S, done).** Configuration for all four channels read only in the Host, the `pipeline.md` § 7 channel table updated, `tech-stack.md` and `mission.md` updated, end-to-end test with no channel configured.
7. **M6.7 – SMTP email transport (S, done).** A second email transport over MailKit beside Azure Communication Services, selected by `Incident:Notifications:Email:Transport` (decisions D14 to D17 of the spec). The email channel, its templates, its recipients and its place in the channel order do not change; only how the message leaves the worker does. TLS is required unless the deployment explicitly configures none for a local test server. Unit tests for the message, the failure reasons and the selection; integration test against any SMTP server behind `Category=RequiresSmtp`, with no local test tool prescribed. No `pipeline.md` change: the contract's email row names no transport.

**Known limitation, closed by M9 and M12:** delivery is at-least-once and messages are not updated in place. There is no incident id and nowhere to store a message id until the state store (M9) and the `Incident` aggregate (M12) exist, so a replayed event posts a second message. Recorded in decision D7 of the spec.

**Done when:** a staged error event in a configured environment produces one Teams card, one Slack message, one Discord embed and one email, all carrying the same text as the report file; the email arrives through whichever transport is selected, Communication Services or SMTP; with nothing configured it produces the report file and four `unavailable` deliveries. The nothing-configured path is proven end-to-end in `tests/DevOpsCopilot.AppHost.Tests`; the configured path runs through the four gated integration tests and the manual checks alongside M7.3, where the channel resources are provisioned.

### M7 – Deploy to Azure (M)

An automated deployment that runs after the main branch passes its checks and deploys the product to Azure Container Apps: the event stream, the worker, the dashboard and the checkpoint storage. Connection details and keys stay in GitHub or Azure settings. Locally saved reports are never deployed.

Specified by [2026-11-17-m7-deploy-to-azure](2026-11-17-m7-deploy-to-azure/requirement.md), in review. The spec proposes changes to the split and to the "Done when" below, which are made only once they are approved.

**Prerequisite, done on 2026-11-20.** Aspire was upgraded from 13.6.4 to 17.0.0. Two security advisories had been published against the version in use, one critical and one high, in the very packages that describe and run a deployment, and nothing is deployed until they are cleared. The upgrade also made the way the local environment was described obsolete, so on the same day the local environment was moved to Aspire 17's replacement, following Aspire's migration guide. Every service, address, health check and setting stayed the same, which was checked by comparing a full listing of the local environment before and after. M7.1 can now build the deployment description on the current model. One consequence for every developer and for automation: the Aspire command-line tool is now required to build and run the local environment. If it is not installed, the build fetches the matching version by itself.

Split into:

1. **M7.1 – Deployment description (S).** A description of what to deploy, generated from the local environment, with the leftover sample projects left out.
2. **M7.2 – Deployment pipeline (S).** The deployment job, its secret settings, and a check that the worker is healthy afterwards.
3. **M7.3 – Connect real sources (S, operations).** At least one Azure resource and one Application Insights instance send their logs to the deployed stream. The worker is configured with real log query and search services.

**Done when:** a change merged to the main branch produces a running Azure environment, and a real error from a connected resource produces a report.

### M8 – Diagnosis quality check (M)

A set of test incidents with known causes, each made of a recorded event plus prepared query results and search results, and an automated job that runs the diagnosis agent over them and scores the outcome. A prompt change that lowers the score fails the build.

Split into:

1. **M8.1 – Test incident format and runner (S).** The format, five test incidents, and a runner that uses prepared answers instead of real services. Runs without an AI model and checks the rule-based path.
2. **M8.2 – Scoring and quality gate (S).** A score per incident (cause named, evidence cited, nothing invented), a pass mark, and an automated job that runs when Azure OpenAI access is available.

**Done when:** every pull request that changes the agents or their prompts shows a quality score, and a deliberately broken prompt fails the check.

**Phase 1 is complete** when M1 to M8 are done. The product then delivers a report per error event to every configured channel from a deployed environment, with measured quality.

---

## Phase 2 – Full incident workflow

Goal: hundreds of related errors become one incident, the agents reason about the whole incident rather than a single event, the report goes to every configured channel and a work item, and confirmed reports improve future diagnoses.

### M9 – Shared state storage (S)

Choose where shared working state is kept (Azure Table Storage or Cosmos DB), record the choice in [tech-stack.md](tech-stack.md), and add it to the product with a simple in-memory stand-in for tests. Triage and grouping state must survive a restart and be shared when more than one copy of the worker runs.

**Done when:** a test writes state from one copy of the worker and reads it from another, using the local emulator.

### M10 – Read events from Azure diagnostic settings (M)

Understand the event format that Azure Monitor and Application Insights diagnostic settings send, and turn each record into the common event described in [the contract's receive section](../pipeline.md#1-receive), marked as coming from the platform or from an application. The log forwarder's format keeps working for local runs. A record that cannot be read is logged and skipped.

Split into:

1. **M10.1 – Application Insights records (S).** Traces, exceptions and requests become common events, keeping the trace and span identifiers.
2. **M10.2 – Platform records (S).** Azure resource logs become common events, keeping the resource and operation; they have no trace identifier.

**Done when:** recorded samples of both formats are read correctly in tests, and a mixed batch is only marked as processed after every record in it has been handled.

### M11 – Triage (M)

Only errors, critical events and events with an exception are considered. Similar events are recognised by their exception type (or operation), category, resource and message, ignoring numbers, identifiers and timestamps in the message. Similar events within 5 minutes are counted together as one candidate. This state is kept in the shared state storage.

Split into:

1. **M11.1 – Recognising similar events (S).** The rule for deciding that two events are the same problem, with tests covering each part of the rule.
2. **M11.2 – Counting candidates (S).** The 5-minute window, counts, first and last seen times, and the lists of traces and resources, kept in the shared state storage.
3. **M11.3 – Dashboard counts (S).** Candidates opened and events merged appear on the dashboard.

**Done when:** a burst of 500 identical errors in two minutes produces one candidate with a count of 500, and a warning on its own produces none.

### M12 – Group into incidents (L, split when specified)

The `Incident` aggregate with the lifecycle `open` → `settling` → `ready` → `investigating` → `reported` → `resolved`, deterministic `incidentId`, the join rules (shared `traceId`, or matching resource within 10 minutes) and the ready rule (90 seconds quiet or 5 minutes after opening). Domain events for each lifecycle change. `IIncidentRepository` in Application. The stable `incidentId` is also what closes M6's at-least-once notification limitation (decision D7 of the M6 spec): until it exists, a replayed event posts a second message to every configured channel.

Split into:

1. **M12.1 – The incident and its stages (S).** The incident, its identifiers and its stage changes, with tests.
2. **M12.2 – Joining and readiness rules (S).** Attaching candidates to incidents and deciding when an incident is ready, using a controllable clock in tests.
3. **M12.3 – Saving incidents safely (S).** Incidents are saved in the shared state storage; replaying a candidate after a restart does not open a duplicate incident.
4. **M12.4 – Late candidates (S).** A candidate that arrives after the incident was reported joins it and marks the report for an update.

**Done when:** two errors sharing a trace open one incident that becomes ready 90 seconds after the last one, and a restart and replay produce the same incident identifier.

### M13 – Diagnose whole incidents (M)

The three agents work on an incident instead of a single event. The evidence agent asks the allowed log questions from [the contract's evidence section](../pipeline.md#4-gather-evidence-diagnostic-agent): one first round and at most two follow-up rounds, no more than eight queries in total, with a limit on rows. Information passed between the agents is tied to the incident. Past-incident search matches on the incident's exception types, categories, resources and services.

Split into:

1. **M13.1 – Tie handoffs to incidents (S).** Information passed between agents refers to the incident rather than the event, and each query records which round it belongs to.
2. **M13.2 – Limit follow-up rounds (S).** The round and query limits are enforced by the product itself, not left to the AI prompt. A ninth query is refused.
3. **M13.3 – Update the quality check (S).** Test incidents now contain several events; the quality check keeps passing.

**Done when:** an incident with three related errors produces one report that shows the impact across services, and no incident ever runs more than eight queries.

### M14 – Work items (M)

Create one Azure DevOps work item per incident containing the full report, and link to it from the Teams card. When the report changes, the work item is updated.

**Done when:** a reported incident has exactly one work item whose description matches the report, and the Teams card links to it.

### M15 – Update messages in place (S)

Was "Slack channel". Slack moved into [M6](#m6--notification-channels-l) on 2026-11-05, so what is left here is the part of [pipeline.md § 7](../pipeline.md#7-report-and-notify) that M6 could not deliver: one message per incident per channel, updated in place when the report changes, instead of one message per report. Needs the message id to be stored against the incident, so it depends on M9 and M12.

Applies to every channel that supports it: Slack `chat.update`, Graph `PATCH` on the chat message, and Discord `PATCH /webhooks/{id}/{token}/messages/{id}`. Email cannot be updated, so a changed report sends a follow-up message that references the first.

**Done when:** a late candidate joining a reported incident updates the existing Teams, Slack and Discord messages rather than posting new ones, and a replayed event posts nothing at all.

### M16 – Learning from confirmed incidents (M)

When the work item is resolved and an engineer confirms or corrects the likely cause, the confirmed report is added to the past-incidents search index and the incident is marked resolved. Unconfirmed reports are never added. DevOpsCopilot never resolves an incident on its own.

Split into:

1. **M16.1 – Reading the confirmation (S).** Read the engineer's confirmation from the work item, using an agreed field or comment format. Tested.
2. **M16.2 – Adding the confirmed report (S).** Add the confirmed report to the search index in the shape described in [the contract's learn section](../pipeline.md#8-learn). Confirmed reports are preferred when looking for similar past incidents.

**Done when:** a confirmed incident appears as a confirmed report in the past-incident results of the next similar incident, and an unconfirmed one does not.

### M17 – Production readiness (M)

Two worker replicas behave identically. Message updates go to the existing channel messages, as M15 delivered. Health checks cover the state store, Event Hubs and the configured channels. Alerts on consumer lag and on `unavailable` rates.

**Done when:** a load test with two copies of the worker produces no duplicate incidents, reports or messages, and the dashboard shows statistics across both copies.

### M18 – Remove leftover projects (S)

Delete the leftover sample projects from the starter template (the sample API, the Azure Functions sample and the email notifier sample), along with their setup and build steps, once nothing depends on them. Keep the log forwarder for local testing until a spec replaces it.

**Done when:** no sample projects remain, the automated checks pass, and the local environment still starts the worker, the dashboard and the emulators.

**Phase 2 is complete** when M9 to M18 are done.

---

## Later, not scheduled

- Import existing post-mortems into the past-incidents search index (an operations task once the index layout is final).
- Actions an operator can trigger, such as re-running a diagnosis or re-sending a report, only on explicit request and never a change to a resource.
- A direct OpenTelemetry exporter for applications that do not use Application Insights.
- Support for AWS and GCP.

## Out of scope

See [mission.md](mission.md#boundaries). In short:

- **Infrastructure as code.** DevOpsCopilot never generates, suggests or renders Bicep, ARM templates, Terraform, Kubernetes manifests or any other infrastructure as code, in reports, notifications, prompts or code. Suggested steps are written for an engineer to carry out by hand. Everywhere else the rule is stated generally, as "no commands, scripts or templates that change resources"; the technologies are named only here and in historical records.
- Automated remediation of any kind, and any other change to a resource.
- A fourth agent.
- Log queries that change data.
- A warning on its own treated as an incident.
- Committing locally saved reports to the repository.

## Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Microsoft Agent Framework API churn | Rework in Infrastructure | Agents sit behind Application interfaces (M3); pin versions in `tech-stack.md`. |
| LLM invents evidence | Wrong report, lost trust | Citation guard (M5.3), evaluation gate (M8), `low` confidence when tools are unavailable. |
| Diagnostic-settings payloads vary by category | Parsing gaps | Record real samples per category as test fixtures (M10); skip and log unparseable records. |
| Correlation rules produce too many or too few incidents | Noise or missed context | Rules are pure functions with table tests (M12); thresholds live in configuration. |
| State store choice made late | Rework of triage and correlation | Decide in M9 before M11. |
| Azure cost of KQL and LLM calls | Budget overrun | Query and round limits enforced in code (M13.2), evaluation runs only when secrets are present. |
| Teams app-only posting needs a resource-specific-consent app installed in the team | No Teams delivery | The channel degrades to `failed` and the other three still deliver (M6); an app-based incoming webhook is the fallback and is a new `INotifier`, not a rewrite. |
| The organisation cannot provision Azure Communication Services | No email delivery | SMTP transport through the organisation's existing relay (M6.7); the transport is a configuration choice, not a rebuild. |
| Four channels in one milestone grow the branch | Slow review | M6.1 merges first; M6.2 to M6.5 are independent and can be separate pull requests. |
| Replayed events post duplicate messages | Noise in the channels | Accepted and documented in M6 (decision D7); removed by the stable `incidentId` in M12 and the message ids stored in M15. |
| Leftover sample projects confuse contributors | Wasted effort | Marked do-not-extend now; removed in M18. |

## Working on a milestone

1. Create a feature branch from the main branch, named with the date, the milestone number and a short feature name (for example "features/2026-11-04-m4-report-contract").
2. Copy the spec template folder to a new spec folder with the same date and name as the branch, and write the requirement, validation and plan documents. Name the milestone in the requirement.
3. Follow [AGENTS.md](../../AGENTS.md): tests first, one feature per branch, and update this file's status when the milestone's check passes.
