# Plan: Deploy to Azure

## Approach

The local environment definition already says what the product is made of and how the parts
connect. The plan is to teach that same definition a second mode, deployment, and to let the Azure
Developer CLI turn it into a running Azure Container Apps environment at deploy time (**D1**). In
deployment mode the definition includes only the four parts in **D3**, and states each part's
permissions, number of copies and reachability explicitly instead of relying on defaults. Local
mode stays exactly as it is.

Four small product changes make the worker fit to run there:

1. Connections to Azure services sign in with the worker's identity when given only an address
   (**D5**).
2. Health checks are served in every environment, answering with the overall state only (**D6**).
3. Sending the worker's own logs to the stream becomes something the local environment switches on,
   instead of something that happens whenever a stream connection exists (**D4**).
4. The deployment sets the report location to the container's temporary storage. The location is
   already a setting, so this needs no product change (**D11**).

None of these touch Domain or Application. They are all about how the outer layers connect, which
is exactly where [AGENTS.md](../../../AGENTS.md#clean-architecture-strict) says such changes belong.

A new deployment job is added to the existing automation workflow. It runs after the existing
checks job, only for changes on the main branch, one at a time, and signs in to Azure through a
trust between GitHub and Azure (**D7**). Its settings come from a GitHub environment (**D8**).
After deploying, it waits for the platform to report the new worker and dashboard as healthy, and
fails if they are not (**D9**).

**Alternatives considered**

- **Hand-written infrastructure templates.** Rejected by **D1**: a second description of the system
  that can drift from the first, and exactly the kind of file the roadmap keeps out of the
  repository.
- **Aspire's own deploy command instead of the Azure Developer CLI.** A real alternative that reads
  the same definition. The Azure Developer CLI was chosen because its unattended use from GitHub,
  and its one-time setup of the GitHub-to-Azure trust, are the better established of the two today.
  Swapping later changes only the deployment job, not the product or the definition.
- **Deploying from a developer's machine.** Rejected: not repeatable, needs personal Azure rights,
  and could pick up locally saved reports (**D11**).
- **Connecting real sources only after M10.** Rejected by **D2**: M10 needs real recorded samples,
  and those can only be captured once a real stream exists.

## Changes by layer

| Layer | What changes here | Notes |
|---|---|---|
| Domain | Nothing | |
| Application | Nothing | No new outside capability is needed; every connection already sits behind an existing one. |
| Infrastructure | The connections to the event stream, the checkpoint storage, the log workspace and the past-incident search, and the stream and storage health checks, sign in with the identity when given only an address, and keep using the key or connection string when given one. | Follows the pattern Azure OpenAI and Communication Services already use. The Azure identity library is already used here, so nothing new is added. |
| Host | The shared service defaults switch log forwarding on only when told to, and serve health checks in every environment with the overall state only. The local environment definition gains its deployment mode. The automation workflow gains the deployment job. A small project file for the Azure Developer CLI points at the local environment definition. | The worker and the dashboard back end themselves need no change beyond what the shared defaults give them. |

## What this feature needs from outside

Nothing new for the product's own use cases. The deployment needs these Azure and GitHub
capabilities, all set up once (see [One-time setup](#one-time-setup)):

| Capability | Responsibility | Provided by |
|---|---|---|
| Hosting | Runs the worker and the dashboard as containers, with secrets, health probes and the number of copies set per part | Azure Container Apps |
| Event stream | Holds the log hub and the worker's reading group, and accepts records from diagnostic settings | Azure Event Hubs, standard tier |
| Progress storage | Holds the worker's checkpoints | Azure Storage, blobs only |
| Image storage | Holds the container images the deployment builds | The container registry the Azure Developer CLI creates with the environment |
| Identity | Lets the worker sign in to Azure services without a stored key | The managed identity Azure Container Apps gives the worker |
| Job sign-in | Lets the deployment job sign in to Azure without a stored password | A Microsoft Entra application trusted by the repository's GitHub environment |
| Dashboard sign-in | Keeps anonymous visitors out of the dashboard (**D13**) | The sign-in built into Azure Container Apps, with Microsoft Entra ID |
| Settings store | Holds the deployment's variables and secrets | A GitHub environment, restricted to the main branch |

## Contract changes

Changes to [docs/pipeline.md](../../pipeline.md), made when the code lands:

- **Sources.** Add a rule: DevOpsCopilot's own resources are never connected as a source, and the
  worker never sends its own logs to the stream it reads (**D4**).
- **Stream and worker.** Say that the stream is the emulator in local runs and a real Event Hubs
  namespace in the deployed environment, reached with the worker's identity (**D5**).
- **Channels.** Add that in the deployed environment the report file is written to the container's
  temporary storage, is lost on restart, and is not a record (**D11**).

Changes to [roadmap.md](../roadmap.md). These need the product owner's approval
([requirement.md § Open questions](requirement.md#open-questions)):

- **M7's split** is rewritten to include the product changes this spec found were needed: M7.1
  gains the identity sign-in for the stream and the storage, health checks in every environment and
  the log forwarder switch; M7.3 gains the identity sign-in for the log workspace and the search.
  The numbering stays the same, so M6's reference to "the manual checks alongside M7.3" stays true.
- **M7's "Done when"** becomes: a change merged to the main branch produces a running, healthy
  Azure environment; a staged error event sent to it produces a report in every configured channel;
  and records from a connected resource arrive and are handled without stopping the worker (**D2**).
- **M10's "Done when"** gains: a real error from a connected resource produces a report.
- **Out of scope, infrastructure as code** gains one sentence: DevOpsCopilot's own deployment is
  generated from its local environment definition at deploy time, and no infrastructure template is
  written or committed (**D1**).
- **The gap table's** "Nothing deploys to Azure" row is closed when M7 is done.
- **M7's size** is reconsidered once the work is done. It is listed as medium; the product changes
  above may make it large.

[mission.md](../mission.md) does not change.

## Tech-stack changes

Each is added to [tech-stack.md](../tech-stack.md) in the pull request that first uses it:

- The Aspire hosting package for Azure Container Apps, at the same Aspire version as the rest of the
  Aspire packages. It is used only by the local environment definition.
- The Azure Developer CLI, used only by the deployment job, at a fixed version.
- The GitHub action that signs in to Azure through the GitHub-to-Azure trust.
- The "Deployment" and "Hosting" rows move from planned to built. The "Secrets" row gains the GitHub
  environment and the container app's secrets. The "Telemetry" row says the deployed worker's logs
  go to the platform's own log collection, and that the developer telemetry dashboard is not
  deployed.
- The package audit is corrected by the Aspire upgrade (**D12**), which lands before any of this.

## Configuration and secrets

Every setting the deployed worker needs becomes a named input to the deployment, marked secret
where it is one. The deployment job fills each input from the GitHub environment. An input that is
left empty leaves that service or channel unconfigured, which means unavailable, exactly as in a
local run (AC-10). No setting has a default in code (see
[requirement.md § Non-functional requirements](requirement.md#non-functional-requirements)).

**For the deployment job** (plain variables, none secret)

| Setting | What it controls |
|---|---|
| Azure subscription, tenant and region | Where the environment is created |
| Environment name | The name of the deployed environment and of the resource group that holds it |
| Deployment application's identifier | Which Microsoft Entra application the job signs in as. An identifier, not a password. |
| Health waiting time | How long the job waits for the new worker and dashboard to become healthy before failing (**D9**) |

**For the worker** (plain variables)

| Setting | What it controls | When empty |
|---|---|---|
| Log workspace address, database and table names | Where evidence questions are asked | Evidence is unavailable |
| Past-incident search address and index | Where similar incidents are searched | Search is unavailable |
| Azure OpenAI address, model deployment and timeout | Where the diagnosis is asked for | The rule-based diagnosis is used |
| Email transport, sender address and recipients | How email is sent and to whom | Email is unavailable |
| Communication Services address | The email service, when that transport is chosen | Email is unavailable |
| SMTP server, port and security | The email server, when that transport is chosen | Email is unavailable |
| Slack channel | Which channel the bot posts in | Slack is unavailable |
| Report location | Where the report file is written; set to the container's temporary storage (**D11**) | The worker refuses to start |

**For the worker** (secrets, stored as GitHub environment secrets and as container app secrets)

| Setting | What it controls |
|---|---|
| Teams webhook address | The Teams flow that posts the card. The address is itself a credential. |
| Slack bot token | The bot that posts in Slack |
| Discord webhook address | The Discord channel webhook. The address is itself a credential. |
| SMTP user name and password | The account on the SMTP server, when it needs one |
| Azure keys (search key, Azure OpenAI key, Communication Services connection string) | Only for an organisation that chooses keys over the identity. Normally left empty (**D5**). |

The event stream and checkpoint storage need no setting at all: the deployment creates them and
passes their addresses to the worker, and the worker signs in with its identity.

## One-time setup

These are operations steps, not code. They are written up in the README's deployment notes so that
someone else can repeat them, as the "Repeatable" non-functional requirement asks:

1. **Choose where.** Pick the subscription, the region and the environment name.
2. **Let the job sign in.** Create a Microsoft Entra application for the deployment job and give it a
   trust for this repository's deployment environment in GitHub. Allow it to create resources in the
   environment's resource group, and to grant access only within that resource group (the
   deployment grants the worker its rights on the stream and the storage).
3. **Store the settings.** Create the GitHub environment, allow only the main branch to use it, and
   fill in the variables and secrets above.
4. **First deployment.** Merge a change, or run the job by hand, and confirm it passes.
5. **Grant the worker read access.** Give the worker's identity read access on the log workspace,
   read access on the past-incident index, permission to use Azure OpenAI, and permission to send
   email through Communication Services if that is used. Nothing more (AC-12).
6. **Require sign-in to the dashboard.** Switch on the platform's built-in sign-in for the dashboard,
   with Microsoft Entra ID, and allow only the organisation's accounts (**D13**).
7. **Set up the channels.** Follow the
   [M6 operations prerequisites](../2026-11-05-m6-notification-channels/plan.md#operations-prerequisites).
8. **Connect the sources.** Add diagnostic settings on one Azure resource and one Application
   Insights instance, sending to the deployed stream. Never choose DevOpsCopilot's own resources
   (**D4**). Start with low-volume sources (see [Risks](#risks)).

## Implementation steps

Each step starts from failing tests listed in [validation.md](validation.md), and each task group
is one pull request.

**Before anything: the Aspire upgrade, on its own branch (D12).** Upgrade the Aspire packages to a
version without the published critical and high advisories, correct the tech-stack package audit,
and confirm the local environment and its tests are unchanged. M7.2 does not deploy until this is
merged.

### 1. Signing in with the identity (M7.1, Infrastructure)

1. Write the tests for the event stream and the checkpoint storage signing in with the identity
   when given only an address, and with the key when given a connection string (AC-5).
2. Make both connections choose their sign-in that way, using the same choice for the two health
   checks.
3. Write the tests for the settings' printed form never showing a key (AC-9), and make them pass.
4. Add the gated tests against a real stream and a real storage account.

### 2. Fit to run in Azure (M7.1, Host)

1. Write the tests for log forwarding being off unless switched on (AC-6), then change the shared
   defaults so it needs to be switched on.
2. Write the tests for readiness and liveness answering outside development with the overall state
   only, and for the worker's readiness following the stream and the storage (AC-8). Then change
   the shared defaults to serve them in every environment.
3. Confirm that a test already covers the report being written to the configured location (AC-11),
   and add one if not.

### 3. The deployment description (M7.1, Host)

1. Write the deployment-mode tests (AC-3, AC-6, AC-9, AC-17) and the local-mode test (AC-15) against
   the local environment definition.
2. Declare the Azure Container Apps environment in the definition, and switch off the developer
   telemetry dashboard in it.
3. In deployment mode, leave out the three samples and the storage queues and tables only they use.
4. State the worker's permissions explicitly: receive only on the stream, and read and write only
   on its own checkpoint blobs. Do not rely on the defaults the tool would otherwise grant (see
   [Risks](#risks)).
5. Make the worker unreachable from outside, give it exactly one copy and never scale it to zero.
   Keep the dashboard reachable from outside.
6. Declare every worker setting from [Configuration and secrets](#configuration-and-secrets) as a
   deployment input, marking the secret ones.
7. Switch log forwarding on for the worker in local mode only.
8. Add the Azure Developer CLI's project file, pointing at the definition.
9. Generate the deployment locally, inspect it against AC-3, AC-4, AC-9 and AC-12, and commit
   nothing it produced.

### 4. The deployment job (M7.2, Host and operations)

1. Do one-time setup steps 1 to 3.
2. Add the deployment job to the existing workflow: after the checks job, for changes on the main
   branch only, never for pull requests, one at a time, with the GitHub environment's settings.
3. After deploying, wait up to the health waiting time for the platform to report the new worker
   and dashboard as healthy. Fail and name the part that is not.
4. Write the deployment notes in the README: the one-time setup, the settings and how to recover
   from a failed deployment.
5. Run the deployment manual checks in [validation.md](validation.md#manual-checks).

### 5. Real services (M7.3, Infrastructure)

1. Write the tests for the log query connection signing in with the identity, and for the search
   signing in with the identity when no key is given (AC-5). Write the tests for a service that
   refuses the identity being reported as unavailable (AC-10).
2. Make them pass.
3. Add the gated tests against a real log workspace and a real search index.

### 6. Connect real sources and channels (M7.3, operations)

1. Do one-time setup steps 5 to 8.
2. Run the end-to-end, M6 and real-source manual checks in
   [validation.md](validation.md#manual-checks), and keep the sample records for M10.

### 7. Contract, documentation and verification

1. Make the [contract changes](#contract-changes) to the pipeline contract, and the approved roadmap
   changes.
2. Update [tech-stack.md](../tech-stack.md) as listed above, and the layout and build sections of
   [AGENTS.md](../../../AGENTS.md) for the new deployment file and job.
3. Build, run the full test suite, run the end-to-end tests locally with Docker, and fill in the
   Results section of [validation.md](validation.md).

## Risks

| Risk | Mitigation |
|---|---|
| The Aspire version in use has published critical and high security advisories, in exactly the packages that generate and run the deployment. | Upgrade on its own branch before any deployment (**D12**). |
| The deployment tool grants broader default permissions than AC-12 allows, for example full ownership of the stream, or rights on storage queues and tables. | The definition states the narrower permissions explicitly (step 3.4); the deployment-mode test and the read-only identity manual check both catch a regression. |
| The generated stream may refuse key-based sending, while diagnostic settings send through the stream's access policies. | Confirm in step 3.9. If so, allow key-based sending for diagnostic settings only; the worker itself still signs in with its identity. |
| The stream health check needs more than receive rights. | Covered by the gated real-stream test in task group 1. If it fails, change the check to something a receiver may do, not the worker's permissions. |
| The container runs as a user who cannot write next to the application, so the report file cannot be written. | The report location is set to the container's temporary storage (**D11**), checked by the temporary report file manual check. |
| Inside the environment, the dashboard cannot reach the worker's status the way it does locally. | Checked in the first deployment: the dashboard must show the worker running while it is healthy. If not, adjust the address the dashboard is given, not the product. |
| Turning on the dashboard's built-in sign-in by hand may be undone by the next deployment. | Checked after the second deployment. If it is undone, the sign-in is declared in the definition instead. |
| Before M10, every record from a real source is logged as unreadable, which is noisy and costs log storage. | Connect low-volume sources first, and disconnect after capturing samples if the volume is a problem. |
| The deployment job does not run the end-to-end tests that need Docker. | The health check after each deployment is the safety net. Running those tests in automation is a later improvement. |
| The deployment tool changes behaviour between versions. | Its version is pinned in the tech stack, and the generation reads only the one definition, which limits what a change can break. |
| An always-running worker and a standard tier stream cost money even when idle. | The smallest options that meet the requirements; the cost is stated in the deployment notes so the owner sees it before the first deployment. |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; the build and the full test run are clean
- [ ] No outward project references; architecture tests pass
- [ ] The Aspire upgrade is merged before the first deployment
- [ ] A change merged to the main branch deploys, and the deployment manual checks pass
- [ ] The M7.3 manual checks pass, and real sample records are saved for M10
- [ ] `docs/pipeline.md` and `docs/specs/tech-stack.md` updated if changed
- [ ] The approved roadmap changes are made
- [ ] The README's deployment notes let someone else repeat the setup
- [ ] All three spec files are in plain English, with no code syntax
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
