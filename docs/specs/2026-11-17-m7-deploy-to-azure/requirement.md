# Requirement: Deploy to Azure

| | |
|---|---|
| **Branch** | `features/2026-11-17-m7-deploy-to-azure` |
| **Milestone** | M7 – Deploy to Azure, covering M7.1 to M7.3 ([roadmap](../roadmap.md#m7--deploy-to-azure-m)) |
| **Status** | Draft |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-11-17 |

## Summary

Today DevOpsCopilot runs only on a developer's machine, against an emulated event stream. This
feature gives it a real home in Azure. Every change merged to the main branch, once its automated
checks pass, is deployed automatically to Azure Container Apps: the worker, the status dashboard,
a real Azure Event Hubs stream and the storage that records the worker's progress. After each
deployment the job checks that the worker is healthy and fails loudly if it is not.

The deployed worker signs in to Azure services with its own Azure identity instead of stored keys,
has read-only access to the systems it investigates, and never feeds its own logs back into the
stream it reads. The only secrets left are the ones for channels outside Azure (Teams, Slack,
Discord and an SMTP server), and those live in GitHub and Azure settings, never in the repository.

## Why

The goal of Phase 1 in the [roadmap](../roadmap.md#phase-1--straight-through-path) is a report
produced "from a deployed Azure environment", and the roadmap's gap table still says "Nothing
deploys to Azure". Three things are blocked until it does:

- **Real sources cannot reach the product.** Azure Monitor and Application Insights diagnostic
  settings can only send to a real event stream in Azure, not to the emulator on a laptop. This is
  how the [mission](../mission.md#who-it-serves) promises zero-touch onboarding, and it has never
  been exercised.
- **M6 is only half proven.** The notification channels were built and tested with no channel
  configured; the configured path ("one Teams card, one Slack message, one Discord embed and one
  email") was deliberately left to be checked alongside M7.3, where the channel settings exist.
- **Later milestones assume it.** The quality check (M8) runs in automation with access to Azure
  OpenAI, and production readiness (M17) is about running more than one copy of a deployed worker.

Reading the code to write this spec turned up four things that would stop a naive deployment from
working or make it unsafe. The spec deals with each one rather than leaving it to be found later:

1. **The worker would investigate itself.** Whenever the worker has an event stream connection, it
   also sends its own logs to that stream (this is how test events are produced locally). Deployed,
   every error the worker logged would become a new event for it to investigate, and a failing
   investigation would log another error. See **D4**.
2. **The worker can only connect with keys.** It reaches the event stream and its progress storage
   only through full connection strings containing shared keys. An Aspire deployment to Azure gives
   the worker an address and an Azure identity instead, so as written it would not start. The log
   query connection has no sign-in at all, and the past-incident search accepts only a key. See
   **D5**.
3. **There is nothing to check health against.** The health check addresses are only switched on
   for local development, so a deployed worker has none, and the platform cannot tell a working
   worker from a stuck one. See **D6**.
4. **Real records would not produce a report yet.** The worker understands only the log
   forwarder's event format. Reading the format that diagnostic settings send is M10. Until then,
   a real record from a connected resource is logged as unreadable and skipped, exactly as
   [the contract's receive section](../../pipeline.md#stream-and-worker) requires, so the roadmap's
   current "Done when" for M7 cannot be met by M7 alone. See **D2**.

## Scope

**In scope**

- Deciding what is deployed, and generating the deployment from the existing local environment
  definition, with the leftover sample projects and the local test log forwarder left out (M7.1).
- The changes the product needs in order to run in Azure: signing in with the deployed identity,
  health checks in every environment, the log forwarder off unless the local environment switches
  it on, and a writable place for the report file (M7.1, and M7.3 for the log query and search
  sign-in).
- An automated deployment job that runs after the main branch's checks pass, its settings and
  secrets, and a health check after every deployment (M7.2).
- The one-time Azure and GitHub setup, written up so that another person can repeat it (M7.2).
- Connecting at least one Azure resource and one Application Insights instance to the deployed
  stream, connecting the worker to a real log workspace, past-incident search, Azure OpenAI and at
  least one notification channel, and running M6's configured-path manual checks (M7.3,
  operations).
- Small clarifications to [the pipeline contract](../../pipeline.md) about the deployed
  environment (see [plan.md § Contract changes](plan.md#contract-changes)).

**Out of scope**

- **Reading diagnostic-settings records.** That is M10. This milestone proves that real records
  *arrive* and are handled safely; turning them into reports is M10's job (**D2**).
- **Creating the organisation's Azure services.** The log workspace, past-incident search, Azure
  OpenAI and Communication Services are existing services that the deployed worker is connected to
  by configuration. M7 creates only the product's own parts (**D3**).
- **Writing infrastructure templates by hand, or committing generated ones** (**D1**).
- More than one environment. There is one deployed environment; staging environments come later.
- More than one copy of the worker. That needs shared state (M9) and is proven in M17.
- Automatic rollback. A failed deployment fails the job and is fixed by a new change (**D9**).
- Alerts on worker lag or on how often services are unavailable. That is M17.
- Upgrading the Aspire packages. The current version has published security advisories, and the
  upgrade must land before the first deployment, but on its own branch (**D12**).
- Re-running the end-to-end tests that need Docker as part of the deployment job.

## Decisions

**D1 – The deployment is generated from the local environment definition and is never committed.**
The product already has one definition of how its parts fit together: the local environment that
starts everything with one command. The deployment is generated from that same definition at
deploy time by the deployment tool, so there is only one description of the system and it cannot
drift. Nobody writes infrastructure templates by hand, and nothing the tool generates is committed.
The only new deployment file in the repository is the tool's small project file, which points at
the local environment definition and describes no resources itself.

This keeps the [out-of-scope rule on infrastructure as code](../roadmap.md#out-of-scope) intact.
That rule is about what DevOpsCopilot produces for the systems it investigates: it must never
suggest or produce a template that changes one of them. Deploying DevOpsCopilot's own parts is a
different thing, and generating that deployment instead of writing templates keeps the repository
free of them altogether. This reading needs the product owner's confirmation (see
[Open questions](#open-questions)).

**D2 – M7 is proven with a staged event, and "a real error produces a report" moves to M10.**
Real diagnostic-settings records cannot become reports until M10 teaches the worker their format.
So M7 is proven in two halves:

- **The deployed path works end to end.** A staged error event, in the format the worker already
  understands, is sent by hand to the deployed stream and produces a report in every configured
  channel. This is also the configured-path check M6 left for M7.3.
- **Real sources are connected and handled safely.** Records from a real Azure resource and a real
  Application Insights instance arrive in the deployed stream. The dashboard shows them being read
  and skipped, the worker keeps running, and its progress keeps advancing.

The roadmap's M10 "Done when" gains the sentence this milestone can no longer honour: a real error
from a connected resource produces a report. The alternative was to do M10 before M7.3. That was
rejected because M10 needs real recorded samples of each record format, and those can only be
captured once a real stream exists, which is what M7 delivers. This changes the roadmap and needs
the product owner's approval (see [Open questions](#open-questions)).

**D3 – Exactly four parts are deployed.**
The deployment contains the worker, the status dashboard (its back end, which also serves the
built front end), an Azure Event Hubs namespace holding the log stream and the worker's reading
group, and a storage account holding the worker's progress checkpoints. Nothing else is deployed:
not the three leftover sample projects, not the storage queues and tables that only the samples
use, not the local test log forwarder, and not the developer telemetry dashboard that the local
environment shows (it displays full logs and traces, and the platform's own log collection is
enough for M7).

The organisation's log workspace, past-incident search, Azure OpenAI and Communication Services
are not created by the deployment. They usually exist already, they are shared with other systems,
and creating them would be the product choosing an organisation's services for it. The worker is
connected to them by configuration, as it is locally.

**D4 – The worker never sends its own logs to the stream it reads.**
Sending logs to the stream is how test events are produced locally, and it stays that way. It is
switched on explicitly by the local environment and is off everywhere else, so a deployed worker
can never send its own logs back to itself. For the same reason, DevOpsCopilot's own Azure
resources are never connected as a source: an operator connecting diagnostic settings must leave
them out. This becomes a rule in [the contract's sources section](../../pipeline.md#sources).

**D5 – Azure services are reached with the worker's Azure identity; keys stay optional.**
One rule for every Azure service the worker uses: given only an address, the worker signs in with
the Azure identity of the environment it runs in; given an address and a key or full connection
string, it uses that, as it does locally against the emulators. Azure OpenAI and Communication
Services already behave this way. M7 brings the event stream, the progress storage, the log query
connection and the past-incident search into line. In the deployed environment no Azure key is
stored anywhere, and the remaining secrets belong only to the channels outside Azure.

**D6 – Health checks are served in every environment, and reveal nothing but the overall state.**
The worker and the dashboard answer a readiness check (is it connected to what it needs?) and a
liveness check (is the process responsive?) in every environment, not only during development. The
worker's readiness includes the event stream and the progress storage. The answer is only
"healthy", "degraded" or "unhealthy", with no detail. The worker accepts no traffic from outside
the environment at all; only the dashboard and the platform's health probes can reach it.

**D7 – The deployment runs automatically after the main branch's checks pass.**
A change merged to the main branch runs the existing checks first. Only if they pass does the
deployment run, for that same version of the code. Pull requests, other branches and failed checks
never deploy. One deployment runs at a time: a later merge waits for the earlier one to finish.
There is no manual approval step, because merging to the main branch after review is the approval.
A person can re-run the deployment by hand. The job signs in to Azure through a trust set up
between GitHub and Azure, so no Azure password or key is stored in GitHub.

**D8 – Secrets live in GitHub environment secrets, and reach the worker as Azure secrets.**
Settings that are secret (the Teams and Discord webhook addresses, the Slack token, the SMTP
password, and any Azure key an organisation chooses to use instead of the identity) are stored as
secrets in a GitHub environment for the deployment. Settings that are not secret (addresses,
names, the email recipients) are stored as plain variables in the same GitHub environment. The
deployment job passes them to the deployment tool, which stores the secret ones as secrets of the
container app, never as plain values. Nothing is committed, printed in the job's log or shown on
the dashboard. A key vault is not added in M7; it can replace GitHub as the store later without
changing the worker.

**D9 – A failed health check fails the deployment job; there is no automatic rollback.**
After deploying, the job waits a fixed time for the new version of the worker and the dashboard to
become healthy. If either does not, the job fails and names which part is unhealthy. Recovery is a
new change to the main branch (usually a revert), which deploys again. Automatic rollback is more
moving parts than one environment and one worker need.

**D10 – One worker, always running.**
Exactly one copy of the worker runs, and it is never scaled down to zero, because it must be
listening for events at all times. Two copies need shared state (M9) and are proven in M17.

**D11 – The report file is written to temporary storage in the deployed worker.**
[The contract](../../pipeline.md#channels) says the report file is always written before any
channel is called. That stays true when deployed, but the file is written to the container's
temporary storage, which is lost when the container restarts. It is a safety net, not a record;
the channels are the delivery. The location is set by configuration, because the container runs as
a user without administrator rights who may not be allowed to write next to the application.
Locally saved reports from a developer's machine can never be deployed: the deployment builds only
from a clean copy of the main branch, and the reports folder is never in the repository.

**D12 – No deployment while a deployment package has a critical or high security advisory.**
The build currently warns that the Aspire packages at the version in use have one critical and one
high published security advisory, in the developer dashboard and in the hosting packages. Those
packages are exactly what generates and runs the deployment. The upgrade is done on its own branch
and merged before M7.2 deploys anything, and the
[tech-stack package audit](../tech-stack.md#package-audit) is corrected at the same time (it still
says no package is vulnerable).

**D13 – The dashboard is not open to anonymous visitors.**
The deployed dashboard is the only part reachable from the internet. It shows the worker's state,
its processing counts and the health of its services. That is not secret, but it tells a stranger
what the organisation runs and when it is struggling. The dashboard therefore requires sign-in with
the organisation's Microsoft Entra ID, using the sign-in built into Azure Container Apps, which is
configured once on the platform and needs no change to the product. This needs the product owner's
confirmation (see [Open questions](#open-questions)).

## Inputs and outputs

| | Description | Defined by |
|---|---|---|
| **Input** | A change merged to the main branch whose automated checks have passed | This spec, **D7** |
| **Input** | The deployment settings and secrets held in the GitHub environment | This spec, **D8**; [plan.md § Configuration and secrets](plan.md#configuration-and-secrets) |
| **Input** | Error events from connected Azure resources and Application Insights | [Receive](../../pipeline.md#1-receive) |
| **Output** | A running Azure environment with the four parts in **D3** | This spec |
| **Output** | A pass or fail result from the deployment job, naming any unhealthy part | This spec, **D9** |
| **Output** | Reports delivered to the configured channels, unchanged from M6 | [Report and notify](../../pipeline.md#7-report-and-notify) |

## Contract and rules touched

- [pipeline.md – Sources](../../pipeline.md#sources): DevOpsCopilot's own resources are never a
  source (**D4**). A contract change.
- [pipeline.md – Stream and worker](../../pipeline.md#stream-and-worker): the stream is emulated
  locally and a real Event Hubs namespace when deployed. An unreadable record is still logged and
  skipped, which is what makes connecting real sources before M10 safe (**D2**). A clarification.
- [pipeline.md – Channels](../../pipeline.md#channels): when deployed, the report file goes to
  temporary storage (**D11**). A clarification; the rule itself is unchanged.
- [pipeline.md – Evidence rule](../../pipeline.md#evidence-rule): an Azure service that is not
  configured, or that refuses the worker's identity, is still reported as unavailable. Unchanged,
  now exercised in Azure.
- [mission.md – Principles](../mission.md#principles), principle 1 "Diagnose, don't act": the
  deployed identity can read the systems it investigates and change nothing in them.

## Domain language

This feature adds no product concepts. It does not touch the incident model, so nothing is added
to the pipeline contract's vocabulary. The terms below are operational and are used only in this
spec and in the deployment notes.

| Term | Meaning in this feature |
|---|---|
| Deployed environment | The single Azure environment that the deployment job keeps up to date with the main branch. |
| Local environment definition | The existing description of how the product's parts fit together, which starts everything locally with one command and is also what the deployment is generated from. |
| Identity | The Azure identity the deployed worker runs as. It is granted permissions on Azure services the way a person would be, and needs no stored key. |
| Source | An Azure resource or Application Insights instance whose diagnostic settings send records to the deployed stream. |
| Staged event | An error event in the format the worker already understands, sent to the deployed stream by hand to prove the deployed path. |

## Acceptance criteria

**AC-1** – A merge to the main branch deploys the product
- **Given** a change merged to the main branch
- **When** its automated checks pass
- **Then** the same version of the code is deployed to the deployed environment, and the platform
  shows the running worker and dashboard as built from that version

**AC-2** – Nothing else deploys
- **Given** a pull request, a change on any other branch, or a change on the main branch whose
  checks fail
- **When** the automation runs
- **Then** no deployment is started, and two merges in quick succession deploy one after the
  other, never at the same time

**AC-3** – The deployment holds exactly the product's four parts
- **Given** the deployment generated from the local environment definition
- **When** it is inspected
- **Then** it contains the worker, the dashboard, the event stream with its log hub and the
  worker's reading group, and the checkpoint storage, and none of the leftover samples, the storage
  pieces only they use, the log forwarder or the developer telemetry dashboard

**AC-4** – No infrastructure template is written or committed
- **Given** the repository after this feature is merged
- **When** it is searched for infrastructure templates
- **Then** none exists, and the only new deployment file points at the local environment
  definition without describing any resource

**AC-5** – The worker connects to Azure without stored keys
- **Given** a deployed worker that is given only the addresses of the event stream, the checkpoint
  storage, the log workspace, the past-incident search and Azure OpenAI
- **When** it starts and handles an event
- **Then** it signs in to each with its own identity, and no Azure key exists in its settings, in
  GitHub or in the repository
- **And** given an address with a key or full connection string, as in local runs against the
  emulators, it uses that instead

**AC-6** – The worker never investigates itself
- **Given** a deployed worker connected to the stream
- **When** it logs an error of its own
- **Then** nothing is sent to the stream, and no report is ever produced about the worker itself
- **And** in a local run the log forwarder still sends test logs exactly as before

**AC-7** – Every deployment is checked for health
- **Given** a deployment that has just finished
- **When** the new version of the worker or the dashboard is not healthy within the configured
  waiting time
- **Then** the deployment job fails and names the unhealthy part; when both are healthy, it passes

**AC-8** – Health checks answer in every environment and reveal nothing else
- **Given** the worker or the dashboard running in a non-development environment
- **When** its readiness or liveness check is called
- **Then** it answers with only the overall state, and the worker's readiness is unhealthy while
  the event stream or the checkpoint storage cannot be reached

**AC-9** – No secret is stored in the repository, printed or shown
- **Given** a deployment with every channel configured
- **When** the repository, the deployment job's log, the generated deployment, the worker's log and
  the dashboard are examined
- **Then** no webhook address, token, password, key or connection string appears in any of them,
  and the deployed worker holds each secret as a container app secret, not a plain value

**AC-10** – Unconfigured services and channels stay optional when deployed
- **Given** a deployment with no channel, log workspace, search or Azure OpenAI configured
- **When** a staged error event arrives
- **Then** the deployment still succeeds and is healthy, the report is produced, and every missing
  service and channel is reported as unavailable, as it is locally

**AC-11** – The report file goes to temporary storage, and local reports are never deployed
- **Given** the deployed worker handling a staged error event
- **When** it writes the report file
- **Then** the write succeeds in the configured temporary location before any channel is called
- **And** the deployment is built only from a clean copy of the main branch, so no report saved on
  a developer's machine can be included

**AC-12** – The deployed identity can read monitored systems and change nothing
- **Given** the permissions granted to the worker's identity
- **When** they are listed
- **Then** the identity can only receive from the stream, read and write its own checkpoint
  storage, read the log workspace, read the past-incident search, use Azure OpenAI and send email,
  and holds no permission to change any other resource

**AC-13** – The deployed path works end to end (M7.3)
- **Given** the deployed environment with Azure OpenAI, the log workspace, the past-incident
  search and at least one channel configured
- **When** a staged error event is sent to the deployed stream
- **Then** one report is delivered to every configured channel with the same text as the report
  file, its evidence comes from the real log workspace, and M6's configured-path manual checks pass

**AC-14** – Real sources are connected and handled safely (M7.3)
- **Given** one Azure resource and one Application Insights instance, neither belonging to
  DevOpsCopilot, sending records to the deployed stream through diagnostic settings
- **When** records arrive
- **Then** the dashboard's count of events read rises, each unreadable record is logged and
  skipped, the worker keeps running, and its progress keeps advancing

**AC-15** – Local runs are unchanged
- **Given** a developer starting the local environment with one command
- **When** an error event is produced
- **Then** the emulators, the log forwarder, the sample projects and the report file behave exactly
  as before, and the existing end-to-end tests pass

**AC-16** – The dashboard is not open to anonymous visitors
- **Given** the deployed dashboard's address
- **When** someone opens it without signing in
- **Then** they are asked to sign in with the organisation's account and see no status
  information until they do

**AC-17** – One worker, always running
- **Given** the deployed environment
- **When** it has had no events for an hour
- **Then** exactly one copy of the worker is still running and still listening

## Non-functional requirements

- **Read-only towards monitored systems.** The deployed identity's permissions are part of the
  product's promise in [mission.md](../mission.md#principles). Any permission beyond those listed
  in AC-12 needs a spec.
- **No configuration in code.** As in M6, timings, addresses and the waiting time for the health
  check come from configuration. A missing or invalid required value stops the worker at startup
  and names the setting, which surfaces in the deployment's health check rather than as a silent
  failure.
- **Bounded cost.** The smallest options that meet the requirements: a standard tier event stream
  with one unit of capacity (the basic tier cannot hold a named reading group), one worker always
  running, and one dashboard copy. The organisation's existing services are not duplicated.
- **Repeatable.** The one-time Azure and GitHub setup is written down step by step, so someone other
  than the author can rebuild the environment from nothing.
- **Deployment time.** A deployment, including its health check, finishes within 20 minutes, or
  fails with a clear message when the waiting time runs out.

## Open questions

These need the product owner's answer before approval:

1. **Approve moving "a real error from a connected resource produces a report" from M7 to M10
   (D2).** The alternative is to reorder the roadmap so M10 comes before M7.3.
2. **Confirm the reading of the infrastructure-as-code rule (D1):** generating DevOpsCopilot's own
   deployment from the local environment definition, with nothing committed, is allowed. If you
   want the out-of-scope section to say so explicitly, the wording is proposed in
   [plan.md § Contract changes](plan.md#contract-changes).
3. **Dashboard access (D13):** sign-in with Microsoft Entra ID (recommended), reachable only from
   inside the environment, or public as it is locally.
4. **Who owns the Azure subscription and the GitHub environment,** and which region to use. The
   one-time setup needs someone with rights to create resources and to grant permissions in that
   subscription.
5. **Aspire upgrade (D12):** confirm it is done on its own branch before M7.2, rather than inside
   this milestone.
