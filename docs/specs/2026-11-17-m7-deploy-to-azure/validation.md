# Validation: Deploy to Azure

Most of this milestone is a deployment, and a deployment can only be fully proven by deploying. The
code changes it needs are small and are proven test-first like any other feature: signing in with
an identity, health checks in every environment, the log forwarder switch, and the report location.
The deployment itself is proven by inspecting what is generated and by the manual checks at the
end, run against the real environment before the pull request for M7.3 is merged.

## Coverage

| AC | Behaviour proven | Type | Layer |
|---|---|---|---|
| AC-1 | A merge to the main branch deploys that same version | Manual | Host |
| AC-2 | Pull requests, other branches and failed checks never deploy; deployments never overlap | Manual | Host |
| AC-3 | The generated deployment holds exactly the worker, the dashboard, the stream and the checkpoint storage | Integration | Host |
| AC-3 | The leftover samples, their storage pieces, the log forwarder and the developer telemetry dashboard are left out | Integration | Host |
| AC-4 | The repository holds no infrastructure template, and generated output is never committed | Manual | Host |
| AC-5 | The event stream and checkpoint storage sign in with the identity when given only an address, and use the key otherwise | Unit | Infrastructure |
| AC-5 | The log query connection signs in with the identity | Unit | Infrastructure |
| AC-5 | The past-incident search signs in with the identity when no key is given, and uses the key otherwise | Unit | Infrastructure |
| AC-5 | Each of the four connections works against a real Azure service with only an address | Integration | Infrastructure |
| AC-6 | With a stream connection but the forwarder not switched on, nothing is sent to the stream | Unit | Host |
| AC-6 | The local environment switches the forwarder on for local runs only | Integration | Host |
| AC-7 | An unhealthy deployment fails the job and names the unhealthy part | Manual | Host |
| AC-8 | Readiness and liveness answer outside development, with only the overall state | Unit | Host |
| AC-8 | The worker's readiness is unhealthy while the stream or the checkpoint storage cannot be reached | Unit | Host |
| AC-9 | No secret appears in the generated deployment, the job log, the worker log or the dashboard | Manual | Host |
| AC-9 | Settings that hold secrets are marked as secrets in the generated deployment | Integration | Host |
| AC-9 | The settings' printed form never shows a key, whichever way the connection signs in | Unit | Infrastructure |
| AC-10 | With nothing configured, the deployment is healthy and every missing service and channel is unavailable | Manual | Host |
| AC-10 | A service given an address but refusing the identity is reported as unavailable, not as a crash | Unit | Infrastructure |
| AC-11 | The report file is written to the configured location | Unit | Host |
| AC-11 | The deployed worker writes the report file to temporary storage before any channel is called | Manual | Host |
| AC-12 | The identity holds only the permissions listed in AC-12 | Manual | Host |
| AC-13 | A staged event produces a report in every configured channel, with real evidence | Manual | Host |
| AC-14 | Real records arrive, are counted, and unreadable ones are skipped without stopping the worker | Manual | Host |
| AC-15 | The local environment still starts the emulators, the forwarder and the samples | Integration | Host |
| AC-15 | The existing end-to-end tests still pass | Integration | Host |
| AC-16 | An anonymous visitor to the dashboard is asked to sign in and sees no status | Manual | Host |
| AC-17 | The worker runs as exactly one copy that is never scaled to zero | Integration | Host |
| AC-17 | After an idle hour, one copy of the worker is still running | Manual | Host |

Domain and Application are not changed by this feature, so they have no new tests. Their existing
tests keep passing unchanged.

## Unit tests (written first)

### Infrastructure: signing in to Azure services

- (AC-5) Given an event stream setting that holds only the stream's address, when the connection is
  created, then it signs in with the identity of the environment and uses no key.
- (AC-5) Given an event stream setting that holds a full connection string with a key, as the local
  emulator provides, when the connection is created, then it uses that connection string, exactly
  as today.
- (AC-5) Given a checkpoint storage setting that holds only the storage address, when the
  connection is created, then it signs in with the identity; given a full connection string, it uses
  that.
- (AC-5, AC-8) Given only addresses, when the stream and storage health checks run, then they sign
  in with the identity in the same way as the worker's own connections, so health reflects what the
  worker will actually experience.
- (AC-5) Given a log workspace address and database, when the log query connection is created, then
  it signs in with the identity. Today it has no sign-in at all.
- (AC-5) Given a past-incident search address with no key, when the search connection is created,
  then it signs in with the identity; given a key, it uses the key.
- (AC-10) Given a log workspace or a search address that is configured but refuses the identity,
  when a query or search is made, then the stage is reported as unavailable with the call it would
  have made, and the event still completes, as the
  [evidence rule](../../pipeline.md#evidence-rule) requires.
- (AC-10) Given no log workspace and no search address, when the stages run, then they are
  unavailable exactly as today.
- (AC-9) Given each connection's settings, in both the identity form and the key form, when they are
  printed for diagnostics, then no key or connection string appears, only whether one is set.

### Host: the log forwarder switch

- (AC-6) Given a stream connection and no instruction to forward logs, when the shared defaults are
  applied, then no log sending to the stream is set up.
- (AC-6, AC-15) Given a stream connection and the instruction to forward logs, when the shared
  defaults are applied, then log sending to the stream is set up, as it is today in local runs.

### Host: health checks

- (AC-8) Given the worker running in a production environment, when its readiness and liveness
  checks are called, then both answer.
- (AC-8) Given the dashboard back end running in a production environment, when its readiness and
  liveness checks are called, then both answer.
- (AC-8) Given a readiness check whose underlying checks have details or errors, when it answers,
  then the answer holds only the overall state and none of the detail.
- (AC-8) Given the stream health check failing, when the worker's readiness is called, then it is
  unhealthy, and its liveness is still healthy, so the platform waits rather than restarting a
  worker that is merely waiting for the stream.

### Host: the report location and configuration

- (AC-11) Given a configured report location, when a report is written, then it is written there.
- (NFR) Given a deployment setting that the worker requires but that is missing or invalid, when the
  worker starts, then it stops with an error naming the setting, as M6 does for its settings.

## Integration tests

### The local environment definition, in deployment mode

These build the local environment definition in the mode the deployment tool uses, and inspect the
result. They start no emulator and need no Docker or Azure, so they run in automation.

- (AC-3) In deployment mode, the definition contains the worker, the dashboard back end with the
  built front end, the event stream with its log hub and the worker's reading group, and the
  checkpoint storage, and nothing else.
- (AC-3) In deployment mode, none of the three leftover sample projects, the storage queues and
  tables only they use, or the developer telemetry dashboard is present.
- (AC-6) In deployment mode, the worker is not told to forward logs.
- (AC-9) In deployment mode, every setting that holds a secret is declared as a secret.
- (AC-17) In deployment mode, the worker accepts no traffic from outside the environment, runs as
  exactly one copy and is never scaled to zero, and the dashboard is reachable from outside.

### The local environment definition, in local mode

- (AC-6, AC-15) In local mode, the emulators, the samples and the log forwarder are all present
  exactly as before, and the worker is told to forward logs.
- (AC-15) The existing end-to-end tests pass unchanged. They need Docker for the emulators and run
  locally, as today.

### Real Azure services

Each of these is skipped unless the settings for that Azure service are present on the developer's
machine, the same way the existing Azure OpenAI test is gated. They never run in automation. The
developer runs them while signed in to Azure with their own account, which stands in for the
deployed identity.

- (AC-5) Reading one event from a real event stream, given only its address.
- (AC-5) Writing and reading a checkpoint in a real storage account, given only its address.
- (AC-5) Running one of the allowed read-only questions against a real log workspace.
- (AC-5) Searching a real past-incident index, given only its address.

## Architecture tests

The existing architecture tests must keep passing. This feature adds no new layer rule, but it is
checked that:

- the Azure identity library is still used only by Infrastructure;
- Domain and Application gain no reference of any kind.

## Evaluation

Not applicable. This feature does not change how the diagnosis is produced.

## Manual checks

These are run against the real deployed environment. The deployment checks are run when the
deployment job is merged (M7.2); the source and channel checks are run before the M7.3 pull request
is merged.

**Deployment (M7.2)**

- **Merge deploys (AC-1).** Merge a small change. The checks run, then the deployment, and the
  platform shows the worker and the dashboard as built from that change.
- **Nothing else deploys (AC-2).** Open a pull request: the checks run, the deployment does not.
  Merge a change that fails a check: nothing is deployed. Merge two changes within a minute: the
  second deployment waits for the first.
- **Unhealthy deployment fails (AC-7).** Point the worker at a checkpoint storage address that does
  not exist and deploy. The job fails within the waiting time and names the worker as unhealthy.
  Restore the setting; the next deployment passes.
- **Deployment time (NFR).** A normal deployment, including the health check, finishes within 20
  minutes.
- **No infrastructure template (AC-4).** Search the repository for infrastructure templates and find
  none. Generate the deployment locally to inspect it, then confirm the working copy shows no new
  file to commit.
- **Secrets stay secret (AC-9).** With every channel configured, read the deployment job's full log,
  the locally generated deployment, the worker's log after a staged event, and the dashboard. No
  webhook address, token, password, key or connection string appears. In Azure, the worker's
  settings show each of them as a secret reference, not a value.
- **Nothing configured (AC-10).** With no channel, log workspace, search or Azure OpenAI configured,
  deploy and send a staged event. The deployment is healthy, the report is written, and every
  service and channel is logged as unavailable.
- **Temporary report file (AC-11).** Send a staged event and confirm from the worker's log that the
  report file was written to the configured temporary location before any channel was called.
- **Read-only identity (AC-12).** List every permission held by the worker's identity. It matches
  AC-12 exactly, and nothing grants a change to any resource other than the worker's own checkpoint
  storage.
- **Dashboard sign-in (AC-16).** Open the dashboard's address in a private browser window. A sign-in
  page appears and no status is shown; after signing in with an organisation account, the status
  appears.
- **Always running (AC-17).** After an hour with no events, exactly one copy of the worker is
  running, and a staged event sent then is handled within a minute.

**Real sources and channels (M7.3)**

- **End to end (AC-13).** With Azure OpenAI, the log workspace, the past-incident search and every
  channel the organisation uses configured, send one staged error event to the deployed stream. One
  report arrives in every configured channel, its text matches the report file, and its evidence
  section shows real rows from the log workspace.
- **M6's configured path (AC-13).** Run every manual check in
  [the M6 validation](../2026-11-05-m6-notification-channels/validation.md#manual-checks) that
  needs a configured channel, against the deployed environment.
- **Real sources (AC-14).** Connect one Azure resource and one Application Insights instance, neither
  belonging to DevOpsCopilot, to the deployed stream through diagnostic settings, and cause an error
  in each. The dashboard's count of events read rises, the worker logs each unreadable record with
  its category and skips it, the worker keeps running, and its progress keeps advancing. Keep a copy
  of a few real records of each kind for M10's test samples, with anything sensitive removed.
- **Repeatable setup (NFR).** Someone other than the author reads the setup notes and confirms each
  step is clear enough to rebuild the environment from nothing.

---

## Results

<!-- Fill in after implementation. Include this section in the pull request. -->

| AC | Behaviour proven | Result | Notes |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |
| AC-8 | | | |
| AC-9 | | | |
| AC-10 | | | |
| AC-11 | | | |
| AC-12 | | | |
| AC-13 | | | |
| AC-14 | | | |
| AC-15 | | | |
| AC-16 | | | |
| AC-17 | | | |

- **Build:**
- **Tests:**
- **Evaluation run:** not applicable
- **Date:**
