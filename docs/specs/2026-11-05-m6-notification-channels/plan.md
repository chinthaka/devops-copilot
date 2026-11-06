# Plan: Notification channels

## Approach

Stage 7 gains a second half. `RunIncidentPipeline` keeps rendering the report and writing it
through `IReportWriter`, and then builds one `IncidentNotification` and hands it to a new
`NotifyChannels` use case. That use case walks every registered `INotifier` in order, bounds each
with a timeout, and turns every outcome — success, unconfigured, error — into a
`NotificationDelivery`. It never throws and never names a channel.

Everything channel-specific is one small Infrastructure class pair: a **pure payload builder** that
maps `IncidentNotification` to the vendor's JSON, and a **notifier** that performs one HTTP call
and maps the response back to a delivery. Nothing else in the solution knows that Teams, Slack,
Discord or email exist. Adding a fifth channel is a new pair plus one registration, which is the
open/closed rule in [AGENTS.md § SOLID](../../../AGENTS.md#solid) made real — and it is asserted by
a test (AC-12), not just asserted in prose here.

Three of the four channels are a single authenticated HTTP POST with a JSON body, so they share
one thin pattern over `IHttpClientFactory` rather than three vendor SDKs. The one exception is
email: Azure Communication Services Email is a long-running operation with a real .NET SDK, so it
uses that SDK behind a thin Infrastructure-local interface, which is also what makes the email
notifier unit-testable without a resource. The alternative for Teams — the full `Microsoft.Graph`
v5 SDK — was rejected: it is a large dependency graph for one `POST`, and the token acquisition it
would bring is already available from `Azure.Identity`, which the solution references.

The notification itself is pure and lives in Domain next to `IncidentReport`, with no packages, so
the content every channel shows is unit-tested once rather than four times (decision
[D1](requirement.md#decisions)). The vendor shapes stay at the Infrastructure boundary
([D10](requirement.md#decisions)), like every other external shape in this codebase.

All of it goes in the existing `Reporting` folder of each layer, because the contract's stage 7 is
"report and notify". No new domain folder, so [AGENTS.md § Build](../../../AGENTS.md#build) needs no
change.

## Task groups

Tests first within every group, per [AGENTS.md](../../../AGENTS.md#how-to-work). Each group leaves
`dotnet build` and `dotnet test` green and is reviewable on its own. Groups 2 to 5 are independent
of one another and can be reordered or split across commits freely; each maps to one roadmap
sub-milestone.

### 1. The notification, the interface and the fan-out (M6.1, Domain and Application)

No network, no channel, no package. This group is the whole feature minus the transports.

1. Write the Domain notification tests and the Application stage tests from
   [validation.md](validation.md#unit-tests-written-first).
2. Add `IncidentNotification` to `src/DevOpsCopilot.Domain/Reporting/`: incident id, title, likely
   cause, confidence, impact line, evidence lines, similar incidents, suggested steps, open
   questions, report location, and `From(diagnosis, diagnostic, grounding, reportPath)`. Unavailable
   tools are stated, exactly as `IncidentReport` states them. No channel vocabulary, no packages.
3. Add `NotificationStatus` (`sent`, `unavailable`, `failed`) and
   `NotificationDelivery(Channel, Status, MessageId, Url, Reason)` to
   `src/DevOpsCopilot.Application/Reporting/`.
4. Add `INotifier` to `src/DevOpsCopilot.Application/Interfaces/`: `string Channel { get; }`,
   `bool IsConfigured { get; }`, `Task<NotificationDelivery> SendAsync(IncidentNotification,
   CancellationToken)`.
5. Add `NotifyChannels` to `src/DevOpsCopilot.Application/Reporting/`: takes
   `IEnumerable<INotifier>` and a per-channel timeout; for each, in registration order, returns
   `unavailable` without calling when `IsConfigured` is false, otherwise calls it under a linked
   timeout token and converts a throw, a timeout or a returned failure into a `failed` delivery
   with a reason. A cancelled *caller* token propagates — that is shutdown, not a channel fault.
   Logs one line per delivery, with no credential in it.
6. Call it from `RunIncidentPipeline` after the report is written, and extend
   `IncidentWorkflowResult` with the deliveries. With no notifier registered the behaviour is
   today's behaviour, which keeps every existing test green.

### 2. Microsoft Teams (M6.2, Infrastructure)

1. Write the Teams builder, notifier and settings tests from validation.md, against
   `StubHttpMessageHandler`.
2. Add `TeamsSettings(TenantId, ClientId, ClientSecret, TeamId, ChannelId)` with a redacting
   `PrintMembers`, as `AzureOpenAISettings` has. Blank tenant, client, team or channel means
   unconfigured.
3. Add `TeamsAdaptiveCard.Build(notification)`: an Adaptive Card 1.5 document, plus the
   `chatMessage` body carrying it as one `application/vnd.microsoft.card.adaptive` attachment
   referenced from `body.content` by an `<attachment id="…"></attachment>` placeholder, which is
   how Graph renders a card. Truncates to the card limit and always keeps the incident id and the
   report location.
4. Add `TeamsNotifier : INotifier`: acquires an app-only token for
   `https://graph.microsoft.com/.default` with `ClientSecretCredential` from `Azure.Identity`,
   `POST`s to `/v1.0/teams/{teamId}/channels/{channelId}/messages`, and maps the response's `id`
   and `webUrl` into the delivery. A `403` naming a missing permission is a `failed` delivery whose
   reason names the Graph error code — the symptom of the RSC Teams app not being installed
   ([D3](requirement.md#decisions)).
5. Write `SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard`, gated on `Category=RequiresTeams`.

### 3. Slack (M6.3, Infrastructure)

1. Write the Slack builder and notifier tests from validation.md.
2. Add `SlackSettings(BotToken, ChannelId)`, redacting, blank means unconfigured.
3. Add `SlackBlocks.Build(notification)`: a header block, section blocks for cause and impact, and
   section blocks for evidence, similar incidents, suggested steps and open questions, each within
   3 000 characters, at most 50 blocks, plus a `text` fallback for notification previews.
4. Add `SlackNotifier : INotifier`: `POST https://slack.com/api/chat.postMessage` with the bot
   token as a bearer, the channel id and the blocks. **The response body decides the outcome:**
   `"ok": false` on a `200` is a `failed` delivery carrying Slack's `error` code; `"ok": true`
   yields a `sent` delivery whose message id is `ts`.
5. Write `SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage`, gated on
   `Category=RequiresSlack`.

### 4. Discord (M6.4, Infrastructure)

1. Write the Discord builder and notifier tests from validation.md, including the two rate-limit
   tests.
2. Add `DiscordSettings(WebhookUrl)`, redacting — the URL is a credential — blank means
   unconfigured.
3. Add `DiscordEmbed.Build(notification)`: one embed with a title, a description, fields for
   impact, evidence, similar incidents, suggested steps and open questions, and a colour chosen
   from the confidence. Within 4 096 characters of description, 6 000 across the embed and at most
   10 embeds.
4. Add `DiscordNotifier : INotifier`: `POST {webhookUrl}?wait=true` so Discord returns the created
   message, and map its `id` into the delivery. Implement the single `Retry-After` retry of
   [D8](requirement.md#decisions) here, where the rate limit is tightest; a second `429` is a
   `failed` delivery.
5. Write `SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed`, gated on `Category=RequiresDiscord`.

### 5. Email (M6.5, Infrastructure)

1. Write the email builder and notifier tests from validation.md, against a fake transport.
2. Add `EmailSettings(ConnectionString, Endpoint, SenderAddress, Recipients)`, redacting. A blank
   connection and endpoint, a blank sender, or an empty recipient list means unconfigured. An
   endpoint with no connection string means managed identity, as the Azure OpenAI settings already
   do for its key.
3. Add `EmailBody.Build(notification)`: a subject naming the incident and its confidence, an HTML
   body and a plain-text alternative carrying the same facts.
4. Add a thin Infrastructure-local `IEmailTransport` over `Azure.Communication.Email`'s
   `EmailClient` so the notifier is unit-testable, and `EmailNotifier : INotifier` which sends one
   message with every configured address in `To` ([D6](requirement.md#decisions)) using
   `WaitUntil.Started`, and reports the operation id. Nothing here touches the leftover sample
   project `src/DevOpsCopilot.EmailNotifier`.
5. Write `SendAsync_AgainstARealCommunicationServicesResource_QueuesOneEmail`, gated on
   `Category=RequiresEmail`.

### 6. Wiring and configuration (M6.6, Host)

1. Write `AddInfrastructure_WithEachChannelConfigured_RegistersThatNotifier` and
   `AddInfrastructure_WithNoChannelConfigured_RegistersFourUnconfiguredNotifiers`.
2. Extend `AddInfrastructure` to take the four settings records and register the four notifiers as
   `INotifier`, always — configuration decides only whether each one sends. Register the typed
   `HttpClient`s through `IHttpClientFactory`.
3. Add `NotificationOptions` with its four sections to `IncidentOptions`, bind them in
   `src/DevOpsCopilot.Worker/Program.cs` and nowhere else, and ship the empty shape in
   `appsettings.json` so it is discoverable.
4. Register `NotifyChannels` and pass the per-channel timeout.

### 7. Contract, documentation and verification

1. Write the architecture tests in validation.md and make them pass.
2. Extend the AppHost end-to-end test with
   `EndToEnd_AnErrorEventWithNoChannelConfigured_WritesTheReportAndRecordsEveryChannelUnavailable`.
3. Apply the contract changes below to `docs/pipeline.md`.
4. Update [tech-stack.md](../tech-stack.md) per the table below, and
   [mission.md § Boundaries](../mission.md#boundaries) to list Discord and email as in-scope
   channels.
5. Update [roadmap.md](../roadmap.md): mark M6 and its sub-milestones done, and record the
   at-least-once limitation of [D7](requirement.md#decisions) against M12.
6. Run the manual checks, set this requirement's status to Done and fill in validation.md's
   **Results**.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | `Reporting/IncidentNotification` | No packages. `IncidentReport` untouched. |
| Application | `src/DevOpsCopilot.Application` | `Interfaces/INotifier`, `Reporting/NotifyChannels`, `Reporting/NotificationDelivery`, `Reporting/NotificationStatus`, `Pipeline/RunIncidentPipeline` (one new call), `Domain/Pipeline/IncidentWorkflowResult` (carries the deliveries) | No Azure SDK, no HTTP client, no channel named. |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `Reporting/TeamsNotifier`, `TeamsAdaptiveCard`, `TeamsSettings`, `SlackNotifier`, `SlackBlocks`, `SlackSettings`, `DiscordNotifier`, `DiscordEmbed`, `DiscordSettings`, `EmailNotifier`, `EmailBody`, `EmailSettings`, `IEmailTransport` and its ACS implementation, `InfrastructureServiceCollectionExtensions` | Adds `Azure.Communication.Email`; uses the existing `Azure.Identity`. One top-level type per file. |
| Host | `src/DevOpsCopilot.Worker` | `Options/NotificationOptions` and four per-channel option classes, `Options/IncidentOptions`, `Program.cs`, `appsettings.json` | Wiring and configuration only. |
| Tests | `tests/DevOpsCopilot.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Architecture.Tests`, `.AppHost.Tests` | New tests in existing projects, plus `Common/StubHttpMessageHandler` and `Common/FakeNotifier` | No new test project. |

Dependencies still point inwards only.

## Interfaces

| Interface | Responsibility | Implemented by |
|---|---|---|
| `INotifier` (Application) | Deliver one notification to one channel, or report itself unconfigured. Never decides what the other channels do, never retries more than its own budget, never throws by contract. | `TeamsNotifier`, `SlackNotifier`, `DiscordNotifier`, `EmailNotifier` in Infrastructure; `FakeNotifier` in tests |
| `IEmailTransport` (Infrastructure-local) | Hand one built message to Azure Communication Services. Exists only so `EmailNotifier` is testable without a resource; never leaves Infrastructure. | `CommunicationServicesEmailTransport`; a fake in tests |

## Contract changes

[docs/pipeline.md § 7](../../pipeline.md#7-report-and-notify) changes in this pull request:

1. **The channel table gains two rows and the Teams row loses a promise it cannot keep yet:**

   | Channel | Content |
   |---|---|
   | Microsoft Teams | Adaptive Card summarising the report. It links to the work item once M14 exists. |
   | Slack | Block Kit message summarising the report |
   | Discord | Embed summarising the report |
   | Email | The report summary, sent to every configured address |
   | Work item (Azure DevOps) | The full report |

2. **A new rule under the table:** "Email is sent to every configured address for every report. It
   is not a fallback for the other channels."
3. **The file sentence is widened** per [D2](requirement.md#decisions): "The report is always
   written to `incidents/YYYY-MM-DD-<incident-id>.md` before any channel is called. When no channel
   is configured, that file is the only delivery. `incidents/` is gitignored and is never committed
   unless asked."
4. **The update-in-place rule is dated:** "One message per incident per channel. Until incidents
   exist (M12), one message per report; a replayed event posts again."
5. **The vocabulary list** in [AGENTS.md § Domain-Driven Design](../../../AGENTS.md#domain-driven-design-pragmatic)
   and in the contract gains `notification`, `channel` and `delivery`.

## Tech-stack changes

| Item | Change |
|---|---|
| Report channels table | Microsoft Teams moves from "Planned (M6)" to built, naming Microsoft Graph with resource-specific consent. Slack moves from "Planned (Phase 2)" to built, naming `chat.postMessage` and Block Kit. Two new rows: Discord (channel webhook, embeds) and Email (Azure Communication Services). |
| Package | `Azure.Communication.Email` 1.1.0 added to `src/DevOpsCopilot.Infrastructure`, pinned at the version resolved during implementation and recorded in the same pull request. |
| Package | `Microsoft.Extensions.Http` for `IHttpClientFactory` in Infrastructure, if it is not already available transitively. |
| Identity | `Azure.Identity` 1.21.0 gains a second use: the app-only Graph token. No new package. |
| Not used | A note that `Microsoft.Graph` was considered and rejected for one endpoint, and that Slack and Discord have no first-party .NET SDK, so all three are plain HTTP. |
| Testing | Four new gated categories: `RequiresTeams`, `RequiresSlack`, `RequiresDiscord`, `RequiresEmail`, skipped without configuration and never run in CI. |

## Configuration and secrets

Read only in `src/DevOpsCopilot.Worker/Program.cs`, under the existing `Incident` section. Every
value defaults to blank, and blank means the channel is unconfigured:

- `Incident:Notifications:TimeoutSeconds` – per channel, default 10.
- `Incident:Notifications:Teams:TenantId`, `:ClientId`, `:ClientSecret`, `:TeamId`, `:ChannelId`
- `Incident:Notifications:Slack:BotToken`, `:ChannelId`
- `Incident:Notifications:Discord:WebhookUrl`
- `Incident:Notifications:Email:ConnectionString`, `:Endpoint`, `:SenderAddress`, `:Recipients`
  (an array)

`appsettings.json` ships the shape with empty values. Every settings record redacts its secret in
`PrintMembers`, so no token, client secret, webhook URL or connection string can reach a log line.
Set them locally with user secrets, never in a file under source control:

```bash
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Slack:BotToken" "xoxb-…"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Discord:WebhookUrl" "https://discord.com/api/webhooks/…"
```

The same keys, set on `tests/DevOpsCopilot.Infrastructure.Tests`, switch on the four gated
integration tests. In Azure the secrets come from the environment's configuration; the email
channel can use the managed identity by setting `Endpoint` and leaving `ConnectionString` blank.

### Operations prerequisites

These are not code and are done alongside M7.3:

- **Teams:** an Entra app registration, and a Teams app manifest declaring
  `ChannelMessage.Send.Group` as an `Application` resource-specific permission, installed into the
  target team. Without it Graph refuses the post ([D3](requirement.md#decisions)).
- **Slack:** a Slack app with the `chat:write` bot scope, installed to the workspace, and the bot
  invited to the target channel.
- **Discord:** a webhook created on the target channel.
- **Email:** a Communication Services resource with a verified sender domain.

## Risks

| Risk | Mitigation |
|---|---|
| The Teams RSC path is refused by tenant policy, or an administrator will not install a Teams app | The channel degrades to `failed` with the Graph error code and the other three still deliver (AC-4). If it proves unworkable, the fallback is an app-based incoming webhook, which is a new `INotifier` and no change anywhere else — exactly what the open/closed split buys. |
| Four channels in one milestone is a large branch | The six task groups are independent after group 1 and each is a commit; groups 2 to 5 can also be split into separate pull requests if review prefers, with group 1 merged first. |
| Vendor payload limits are discovered at runtime, in production, on the longest report | Limits are asserted in unit tests against a deliberately oversized diagnosis (AC-9), not trusted to the happy path. |
| A channel outage slows the consumer | Per-channel timeout, sequential but bounded, one retry only, failures never propagate ([D8](requirement.md#decisions), AC-4). |
| A credential leaks into a log, a payload or a delivered message | Redacting settings records, one test per record, one test per builder, and a manual check (AC-10). |
| Replays post duplicate messages | Accepted and documented for M6 ([D7](requirement.md#decisions)); removed by the stable `incidentId` in M12 and the state store in M9. Recorded in the roadmap so it is not forgotten. |
| Slack's `200 OK` with `ok:false` is read as success | A named test exists for precisely this (AC-6), because it is the classic way to ship a silently broken Slack integration. |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; `dotnet build` and `dotnet test` clean
- [ ] No outward project references; architecture tests pass, including the new package and
      channel-naming rules
- [ ] `docs/pipeline.md` updated with the five contract changes above
- [ ] `docs/specs/tech-stack.md` and `docs/specs/mission.md` updated
- [ ] `docs/specs/roadmap.md` marks M6 done and records the at-least-once limitation
- [ ] The four gated integration tests have each been run once against a real channel, or the
      Results section says which were not and why
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
