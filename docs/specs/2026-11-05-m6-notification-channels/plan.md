# Plan: Notification channels

## Approach

Stage 7 gains a second half. `RunIncidentPipeline` keeps rendering the report and writing it
through `IReportWriter`, and then builds one `IncidentNotification` and hands it to a new
`NotifyChannels` use case. That use case walks every registered `INotifier` in order, bounds each
with a timeout, and turns every outcome — success, unconfigured, error — into a
`NotificationDelivery`. It never throws and never names a channel.

Everything channel-specific is one small Infrastructure class pair: a **pure payload builder** that
maps `IncidentNotification` to the vendor's JSON, and a **notifier** that performs one call and maps the response back to
a delivery. Nothing else in the solution knows that Teams, Slack,
Discord or email exist. Adding a fifth channel is a new pair plus one registration, which is the
open/closed rule in [AGENTS.md § SOLID](../../../AGENTS.md#solid) made real — and it is asserted by
a test (AC-12), not just asserted in prose here.

Teams, Slack and Discord are each a single HTTP POST with a JSON body — Teams to a Workflows
trigger URL ([D3](requirement.md#decisions)), Slack with a bot token, Discord to a channel
webhook — so they share one thin pattern over `IHttpClientFactory` and no vendor SDK. Azure
Communication Services Email is a
long-running operation with a real .NET SDK, used behind a thin Infrastructure-local interface,
which is also what makes the email notifier unit-testable without a resource.

That interface is also the seam for the 2026-11-08 revision (task group 8): SMTP is a second
`IEmailTransport`, built on MailKit ([D15](requirement.md#decisions)), and configuration selects
which one the email channel uses ([D14](requirement.md#decisions)). The selection is made once, in
the composition; `EmailNotifier` never asks which transport it holds, and each transport turns its
own failures into a transport-neutral reason ([D16](requirement.md#decisions)).

The notification itself is pure and lives in Domain next to `IncidentReport`, with no packages, so
the content every channel shows is unit-tested once rather than four times (decision
[D1](requirement.md#decisions)). The vendor shapes stay at the Infrastructure boundary
([D10](requirement.md#decisions)), like every other external shape in this codebase.

Two documents are template-driven: the report and the email. A pure Domain renderer substitutes
`{{placeholder}}` tokens from named values ([D12](requirement.md#decisions)), the embedded
defaults reproduce today's wording byte for byte, and an optional configured path, re-read on
every render, lets a customer change either document by editing a file instead of deploying
([D13](requirement.md#decisions)). The chat payloads stay code-built: their limits and truncation
rules are vendor-specific and asserted by tests, and nothing in M6 needs them customisable.

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
   `src/DevOpsCopilot.Domain/Reporting/`, next to the notification — `IncidentWorkflowResult`
   lives in Domain and carries the deliveries, and Domain cannot reference Application.
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
7. Write the template-renderer tests from validation.md, then add `TemplateRenderer` to
   `src/DevOpsCopilot.Domain/Reporting/`: pure `{{placeholder}}` substitution per
   [D12](requirement.md#decisions), lists expanding to bullet lines, unknown tokens left visible.
8. Re-express `IncidentReport.Render` as the embedded default report template plus a values map
   over the renderer, asserted byte-identical to the previous hard-coded layout by a test. The
   method keeps its signature and gains an optional template argument.
9. Add `ITemplateSource` to `src/DevOpsCopilot.Application/Interfaces/` — `Task<string?>
   GetAsync(string name, CancellationToken)`, null meaning "use the embedded default" — and let
   `RunIncidentPipeline` fetch the `report` template through it before rendering. With no source
   registered the default is used, which keeps every existing test green.

Groups 1 to 7 are implemented on this branch; group 8, the revision of 2026-11-08, is
implemented too. The default report template is the embedded resource
`src/DevOpsCopilot.Domain/Reporting/Templates/report.md`, which is also the starting point to copy
for a custom template.

### 2. Microsoft Teams (M6.2, Infrastructure)

1. Write the Teams builder, notifier and settings tests from validation.md, against
   `StubHttpMessageHandler`.
2. Add `TeamsSettings(WebhookUrl)`, redacting — the trigger URL carries a signature and is a
   credential — blank means unconfigured.
3. Add `TeamsAdaptiveCard.Build(notification)`: an Adaptive Card 1.5 document wrapped as
   `{"type":"message","attachments":[{"contentType":"application/vnd.microsoft.card.adaptive",
   "contentUrl":null,"content":<card>}]}`, the shape the Workflows trigger expects. Truncates to
   stay within the 28 KB card payload limit and always keeps the incident id and the report
   location.
4. Add `TeamsNotifier : INotifier`: `POST`s the body to the trigger URL. `202 Accepted` (or any
   `2xx`) is a `sent` delivery with no message id ([D3](requirement.md#decisions)); any other
   status is `failed` with the status code as the reason. Neither the reason nor an exception
   message carries the URL.
5. Write `SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard`, gated on `Category=RequiresTeams`.

### 3. Slack (M6.3, Infrastructure)

1. Write the Slack builder and notifier tests from validation.md.
2. Add `SlackSettings(BotToken, ChannelId)`, redacting, blank means unconfigured.
3. Add `SlackBlocks.Build(notification)`: a header block, section blocks for cause and impact, and
   section blocks for evidence, similar incidents, suggested steps and open questions, each within
   3 000 characters, at most 50 blocks, plus a `text` fallback for notification previews.
4. Add `SlackNotifier : INotifier`: `POST` to the configured `chat.postMessage` URL (moved from a
   constant into `SlackSettings.PostMessageUrl` on 2026-11-07) with the bot
   token as a bearer, the channel id and the blocks. **The response body decides the outcome:**
   `"ok": false` on a `200` is a `failed` delivery carrying Slack's `error` code; `"ok": true`
   yields a `sent` delivery whose message id is `ts`.
5. Write `SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage`, gated on
   `Category=RequiresSlack`.

### 4. Discord (M6.4, Infrastructure)

1. Write the Discord builder and notifier tests from validation.md, including the four rate-limit
   tests: one retry then success, a second `429` fails, a `429` without `Retry-After` is not
   retried, and a `Retry-After` beyond the budget is not waited for.
2. Add `DiscordSettings(WebhookUrl)`, redacting — the URL is a credential — blank means
   unconfigured.
3. Add `DiscordEmbed.Build(notification)`: one embed with a title, a description (incident line
   and likely cause), fields for impact, evidence, similar incidents, suggested steps, open
   questions and the report location, and a colour chosen from the confidence. Within 256
   characters of title, 4 096 of description, 25 fields of 1 024, 6 000 across the embed and at
   most 10 embeds; tighter budgets (`DiscordEmbedBudget`) are tried until the total fits. The
   payload sets `allowed_mentions.parse` to empty, because diagnosis text comes from logs and an
   `@everyone` in it must never ping the channel.
4. Add `DiscordNotifier : INotifier`: `POST {webhookUrl}?wait=true` — appended to any existing
   query, such as `thread_id` — so Discord returns the created message, and map its `id` into the
   delivery. Implement the single `Retry-After` retry of [D8](requirement.md#decisions) here,
   where the rate limit is tightest: only when the header is present and its delay is within
   the configured `DiscordSettings.MaxRetryDelay` (a constant until 2026-11-07; 10 seconds in the
   shipped `appsettings.json`, matching the per-channel timeout); a second `429` is a `failed`
   delivery. The wait is an injected delay so the tests do not sleep. No reason carries the URL.
5. Write `SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed`, gated on `Category=RequiresDiscord`.

### 5. Email (M6.5, Infrastructure)

1. Write the email builder and notifier tests from validation.md, against a fake transport and a
   fake template source.
2. Add `EmailSettings(ConnectionString, Endpoint, SenderAddress, Recipients)`, redacting the
   connection string and printing recipients as a count only. Recipients are trimmed, blank ones
   dropped and duplicates removed without regard to case. A blank connection and endpoint, a blank
   sender, or no non-blank recipient means unconfigured. An
   endpoint with no connection string means managed identity, as the Azure OpenAI settings already
   do for its key.
3. Add the three embedded default email templates — subject, HTML body, plain-text body — and
   `EmailBody.Build(notification, templates)`, which fills each template's placeholders from the
   notification through the Domain renderer ([D12](requirement.md#decisions)) and returns an
   `EmailContent`. Values are HTML-encoded in the HTML body (log text is never trusted as markup),
   lists become `<ul>` there and `- ` lines in plain text, and line breaks are stripped from the
   subject so no value can inject a mail header. The defaults are embedded resources under
   `src/DevOpsCopilot.Infrastructure/Reporting/Templates/` (`email-subject.txt`, `email.html`,
   `email.txt`), held by `EmailTemplates.Default`. The defaults name
   the incident and its confidence in the subject and carry the same facts in both bodies; a
   custom template resolved through `ITemplateSource` replaces any of the three
   ([D13](requirement.md#decisions)).
4. Add a thin Infrastructure-local `IEmailTransport` over `Azure.Communication.Email`'s
   `EmailClient` (`CommunicationServicesEmailTransport`, taking an `OutgoingEmail`; access key from
   the connection string, otherwise `DefaultAzureCredential` on the endpoint) so the notifier is
   unit-testable, and `EmailNotifier : INotifier` which resolves the three templates — a source
   that returns nothing or throws falls back to the default — builds the message, sends one message with every configured address in
   `To` ([D6](requirement.md#decisions)) using `WaitUntil.Started`, and reports the operation id.
   A `RequestFailedException` is a `failed` delivery naming its status and error code; any other
   failure names only the exception type, so the connection string cannot reach a reason.
   `Azure.Communication.Email` 1.1.0 is recorded in tech-stack.md in the same change, as
   AGENTS.md requires.
   Nothing here touches the leftover sample project `src/DevOpsCopilot.EmailNotifier`.
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
5. Add `FileTemplateSource` to Infrastructure and register it as `ITemplateSource`: it maps the
   template names (`report`, `email-subject`, `email-html`, `email-text`) to the configured paths,
   re-reads the file on every call, and answers null — after one warning naming the path — when a
   path is unset, missing or unreadable ([D13](requirement.md#decisions)).

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

### 8. Email over SMTP (M6.7, Infrastructure and Host) — revision of 2026-11-08

Tests first, from [validation.md § Email over SMTP](validation.md#testsdevopscopilotinfrastructuretests--the-four-channels-no-network)
and [§ configuration checks](validation.md#testsdevopscopilotworkertests--configuration-checks-ac-17-d17).
Each step leaves `dotnet build` and `dotnet test` green.

1. **Make the transport carry its own configuration and failures (refactor, no behaviour
   change).**
   - Split `EmailSettings` into addressing and transport: `EmailSettings(SenderAddress,
     Recipients)` keeps the recipient clean-up and its `IsConfigured` becomes "a sender and at
     least one recipient"; the connection string and endpoint move to a new redacting
     `CommunicationServicesEmailSettings(ConnectionString, Endpoint)`.
   - `IEmailTransport` gains `bool IsConfigured { get; }`. `EmailNotifier.IsConfigured` becomes
     addressing configured **and** transport configured. `UnconfiguredEmailTransport` answers
     false.
   - Add `EmailTransportException(string reason)`. Move the `RequestFailedException` mapping from
     `EmailNotifier` into `CommunicationServicesEmailTransport`, which now throws
     `EmailTransportException` with the same reason text. `EmailNotifier` catches
     `EmailTransportException` and records its reason; its catch-all for any other exception and
     its cancellation carve-out stay. `EmailNotifier` no longer references `Azure`.
   - Existing email tests move with the code: the `RequestFailedException` notifier test becomes
     the transport test plus the transport-neutral notifier test listed in validation.md.
2. **Add MailKit.** `MailKit` 4.18.2 (the latest stable at the time of writing; re-check and pin
   the resolved version) to `src/DevOpsCopilot.Infrastructure` only. MimeKit arrives with it.
   Record it in [tech-stack.md](../tech-stack.md) in the same commit, and add it to the
   architecture test's notification-package list and `Smtp`, `MailKit` and `MimeKit` to its
   channel-name list.
3. **SMTP settings.** Add `SmtpSecurity` (`StartTls`, `SslOnConnect`, `None`) and the redacting
   `SmtpSettings(Host, Port, Security, Username, Password)`: configured when the host is not blank
   and the port is positive; prints the host, port, security and whether a username and password
   are set, never the password.
4. **Pure SMTP pieces**, each its own file in `src/DevOpsCopilot.Infrastructure/Reporting/`:
   - `SmtpMessage.Build(OutgoingEmail) : MimeMessage` — `From` the sender, every recipient in `To`
     ([D6](requirement.md#decisions)), the subject, and a `BodyBuilder` with `TextBody` and
     `HtmlBody`, which produces `multipart/alternative` with plain text first. MimeKit generates a
     fresh `Message-ID` per message.
   - `SmtpSecurity` → MailKit `SecureSocketOptions` mapping: `StartTls` → `StartTls` (required,
     never `StartTlsWhenAvailable`), `SslOnConnect` → `SslOnConnect`, `None` → `None`
     ([D17](requirement.md#decisions)).
   - `SmtpFailure.Describe(Exception) : string` per [D16](requirement.md#decisions):
     `SmtpCommandException` → `The SMTP server answered {code} ({ErrorCode}).`;
     MailKit `AuthenticationException` → `The SMTP server rejected the login.`;
     `SslHandshakeException` → `The TLS handshake with the SMTP server failed.`; anything else →
     `The SMTP send failed with {TypeName}.` Never the server's text, the username or the
     password.
5. **`SmtpEmailTransport : IEmailTransport`.** Per send: create a MailKit `SmtpClient`,
   `ConnectAsync(host, port, option)`, `AuthenticateAsync(username, password)` only when both are
   set, `SendAsync(message)`, `DisconnectAsync(quit: true)`, dispose; every call takes the
   cancellation token, so the stage's per-channel timeout bounds connect, login and send together
   and SMTP adds no timing setting. Returns the message's `Message-ID`
   ([D17](requirement.md#decisions)). An `OperationCanceledException` propagates; any other
   exception becomes `EmailTransportException(SmtpFailure.Describe(exception))`. One connection
   per report is deliberate: reports are rare, and a pooled connection would need its own idle
   timeout and health handling.
6. **Selection in the composition** ([D14](requirement.md#decisions)). Add
   `EmailTransportKind` (`CommunicationServices`, `Smtp`). `AddInfrastructure` takes the kind, the
   `CommunicationServicesEmailSettings` and the `SmtpSettings`, and registers exactly one
   `IEmailTransport`: for Communication Services, `CommunicationServicesEmailTransport.Create` when
   its settings are configured and `UnconfiguredEmailTransport` otherwise (today's rule); for SMTP,
   `SmtpEmailTransport`, which reports its own `IsConfigured`. This is the single place the kind
   is examined, and it is composition, not channel logic.
7. **Host configuration.** `EmailOptions` gains `Transport` (string) and an `Smtp` section
   (`SmtpOptions`: `Host`, `Port`, `Security`, `Username`, `Password`); the Communication Services
   keys stay where they are (see the open question in requirement.md). Move the startup checks
   from local functions in `Program.cs` into `Options/IncidentOptionsValidator.Validate`, which
   throws naming the key when: the transport is not one of the two values; with SMTP selected, the
   security mode is unknown or the port is not positive; the username is set without the password
   or the reverse; or any timeout or the retry cap is not positive (the 2026-11-07 checks).
   `appsettings.json` ships `"Transport": "CommunicationServices"` and
   `"Smtp": { "Host": "", "Port": 587, "Security": "StartTls", "Username": "", "Password": "" }`.
8. **Integration test.** `SendAsync_AgainstAnSmtpServer_DeliversOneMessage` with
   `SmtpRequiredFactAttribute` and `SmtpConfiguration`, layered over the worker's
   `appsettings.json` like the other gated tests, `Category=RequiresSmtp`. Run it against
   any SMTP server; no tool is prescribed.
9. **Docs.** Run the manual checks, fill in the 2026-11-08 block of validation.md's **Results**,
   mark roadmap M6.7 and M6 done, and set this requirement's status back to Done.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | `Reporting/IncidentNotification`, `Reporting/NotificationDelivery`, `Reporting/NotificationStatus`, `Reporting/TemplateRenderer`, the embedded default report template, `Pipeline/IncidentWorkflowResult` (carries the deliveries) | No packages. `IncidentReport` keeps its signature and its default output byte for byte; it renders through the default template. |
| Application | `src/DevOpsCopilot.Application` | `Interfaces/INotifier`, `Interfaces/ITemplateSource`, `Reporting/NotifyChannels`, `Pipeline/RunIncidentPipeline` (notifies after writing; fetches the report template) | No Azure SDK, no HTTP client, no channel named. |
| Infrastructure (2026-11-08) | `src/DevOpsCopilot.Infrastructure` | New: `Reporting/SmtpEmailTransport`, `SmtpSettings`, `SmtpSecurity`, `SmtpMessage`, `SmtpFailure`, `CommunicationServicesEmailSettings`, `EmailTransportKind`, `EmailTransportException`. Changed: `EmailSettings` (addressing only), `IEmailTransport` (gains `IsConfigured`), `EmailNotifier` (transport-neutral catch), `CommunicationServicesEmailTransport` (maps its own failures), `UnconfiguredEmailTransport`, `InfrastructureServiceCollectionExtensions` | Adds `MailKit`. Still one top-level type per file. |
| Host (2026-11-08) | `src/DevOpsCopilot.Worker` | New: `Options/SmtpOptions`, `Options/IncidentOptionsValidator`. Changed: `Options/EmailOptions`, `Program.cs`, `appsettings.json` | Configuration checks become testable; still no business logic. |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `Reporting/TeamsNotifier`, `TeamsAdaptiveCard`, `TeamsSettings`, `SlackNotifier`, `SlackBlocks`, `SlackSettings`, `DiscordNotifier`, `DiscordEmbed`, `DiscordSettings`, `EmailNotifier`, `EmailBody`, `EmailSettings`, `IEmailTransport` and its ACS implementation `CommunicationServicesEmailTransport`, `OutgoingEmail`, `EmailContent`, `EmailTemplates` and the three embedded default email templates, `FileTemplateSource`, the truncation budgets `TeamsCardBudget` and `DiscordEmbedBudget`, the embedded default email templates, `InfrastructureServiceCollectionExtensions` | Adds `Azure.Communication.Email`; uses the existing `Azure.Identity` only for the email channel's managed identity. One top-level type per file. |
| Host | `src/DevOpsCopilot.Worker` | `Options/NotificationOptions` and four per-channel option classes (the email one carrying its template paths), the report template path, `Options/IncidentOptions`, `Program.cs`, `appsettings.json` | Wiring and configuration only. |
| Tests | `tests/DevOpsCopilot.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Architecture.Tests`, `.AppHost.Tests` | New tests in existing projects, plus `Common/StubHttpMessageHandler`, `Common/RecordedRequest`, `Common/Notifications` and `Common/FakeNotifier` | No new test project. |

Dependencies still point inwards only.

## Interfaces

| Interface | Responsibility | Implemented by |
|---|---|---|
| `INotifier` (Application) | Deliver one notification to one channel, or report itself unconfigured. Never decides what the other channels do, never retries more than its own budget, never throws by contract. | `TeamsNotifier`, `SlackNotifier`, `DiscordNotifier`, `EmailNotifier` in Infrastructure; `FakeNotifier` in tests |
| `IEmailTransport` (Infrastructure-local) | Hand one built message to the selected transport and return its message id; report whether the transport is configured; turn every failure except cancellation into an `EmailTransportException` with a safe reason ([D16](requirement.md#decisions)). Was ACS-only until 2026-11-08; never leaves Infrastructure. | `CommunicationServicesEmailTransport`, `SmtpEmailTransport`, `UnconfiguredEmailTransport`; a fake in tests |
| `ITemplateSource` (Application) | Return the configured template text for one name, or null to mean "use the embedded default". Never throws: an unset path is a silent null, a missing or unreadable file is one warning and a null. | `FileTemplateSource` in Infrastructure; a fake in tests |

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
   and in the contract gains `notification`, `channel`, `delivery` and `template`.
6. **A report-template sentence is added** per [D13](requirement.md#decisions): "The seven-section
   layout is the embedded default report template. A deployment may configure its own report and
   email templates; a template changes wording and layout only — every fact still comes from the
   diagnosis and the handoffs, and no template adds a claim."

## Tech-stack changes

| Item | Change |
|---|---|
| Report channels table | Microsoft Teams moves from "Planned (M6)" to built, naming the Teams Workflows webhook. Slack moves from "Planned (Phase 2)" to built, naming `chat.postMessage` and Block Kit. Two new rows: Discord (channel webhook, embeds) and Email (Azure Communication Services). |
| Package | `Azure.Communication.Email` 1.1.0 added to `src/DevOpsCopilot.Infrastructure`, pinned at the version resolved during implementation and recorded in the same pull request. |
| Package (2026-11-08) | `MailKit` 4.18.2 added to `src/DevOpsCopilot.Infrastructure` for the SMTP transport ([D15](requirement.md#decisions)); MimeKit comes with it. `System.Net.Mail.SmtpClient` is listed under "Not used". |
| Testing (2026-11-08) | A fifth gated channel category, `RequiresSmtp`, run against any SMTP server the developer chooses. No local test server is prescribed or added to the AppHost. |
| Package | `Microsoft.Extensions.Http` for `IHttpClientFactory` in Infrastructure, if it is not already available transitively. |
| Identity | `Azure.Identity` 1.21.0 gains a second use: the email channel's managed identity when `Endpoint` is set without a connection string. No new package. |
| Not used | A note that `Microsoft.Graph` is not used — Teams posts through a Workflows webhook ([D3](requirement.md#decisions)) — and that Slack and Discord have no first-party .NET SDK, so all three are plain HTTP over `IHttpClientFactory`, and that no templating package is used ([D12](requirement.md#decisions)). |
| Templates | The report and the email render from templates with embedded defaults; a custom template is a file named in configuration, re-read on every render ([D13](requirement.md#decisions)). |
| Testing | Four new gated categories: `RequiresTeams`, `RequiresSlack`, `RequiresDiscord`, `RequiresEmail`, skipped without configuration and never run in CI. |

## Configuration and secrets

Read only in `src/DevOpsCopilot.Worker/Program.cs`, under the existing `Incident` section.
Credentials and addressing default to blank, and blank means the channel is unconfigured. Since
2026-11-07, timings, caps and endpoints have no value in code: `appsettings.json` supplies them,
and a missing or invalid one stops the worker at startup naming the key.

- `Incident:Notifications:TimeoutSeconds` – per channel; `10` in `appsettings.json`.
- `Incident:Notifications:Teams:WebhookUrl` – the Workflows trigger URL
- `Incident:Notifications:Slack:BotToken`, `:ChannelId`, and `:PostMessageUrl`
  (`https://slack.com/api/chat.postMessage` in `appsettings.json`)
- `Incident:Notifications:Discord:WebhookUrl`, and `:MaxRetryDelaySeconds` (`10` in
  `appsettings.json`)
- `Incident:Notifications:Email:SenderAddress`, `:Recipients` (an array) – addressing, whichever
  transport sends
- `Incident:Notifications:Email:Transport` – `CommunicationServices` or `Smtp`
  (`CommunicationServices` in `appsettings.json`; added 2026-11-08, [D14](requirement.md#decisions))
- `Incident:Notifications:Email:ConnectionString`, `:Endpoint` – Communication Services
- `Incident:Notifications:Email:Smtp:Host`, `:Port`, `:Security` (`StartTls`, `SslOnConnect` or
  `None`), `:Username`, `:Password` – SMTP (`Port` `587` and `Security` `StartTls` in
  `appsettings.json`; [D17](requirement.md#decisions))
- `Incident:Report:TemplatePath` – optional path to a custom report template.
- `Incident:Notifications:Email:SubjectTemplatePath`, `:HtmlTemplatePath`, `:TextTemplatePath` –
  optional paths to custom email templates.

A blank template path is not an unconfigured channel: it means the embedded default template. A
set path is re-read on every render, so editing the file changes the next report or email with no
restart and no deployment ([D13](requirement.md#decisions)); in Azure, mount the template files
from an Azure Files share so an edit needs no image rebuild.

`appsettings.json` ships the shape with empty values. Every settings record redacts its secret in
`PrintMembers`, so no token, client secret, webhook URL or connection string can reach a log line.
Set them locally with user secrets, never in a file under source control:

```bash
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Slack:BotToken" "xoxb-…"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Discord:WebhookUrl" "https://discord.com/api/webhooks/…"
```

Email through SMTP. For local testing use any SMTP test server you like; the values below are
placeholders for its host and port. None of them is a secret, but all are environment-specific:

```bash
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:Transport" "Smtp"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:Smtp:Host" "localhost"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:Smtp:Port" "<port>"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:Smtp:Security" "None"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:SenderAddress" "devopscopilot@example.test"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:Notifications:Email:Recipients:0" "oncall@example.test"
```

The same keys, set on `tests/DevOpsCopilot.Infrastructure.Tests`, switch on the four gated
integration tests. In Azure the secrets come from the environment's configuration; the email
channel can use the managed identity by setting `Endpoint` and leaving `ConnectionString` blank.

### Operations prerequisites

These are not code and are done alongside M7.3:

- **Teams:** in the target channel, create a flow from the Workflows template "Post to a channel
  when a webhook request is received" and copy its trigger URL ([D3](requirement.md#decisions)).
  The flow runs as the account that created it, so use a service account rather than a person.
- **Slack:** a Slack app with the `chat:write` bot scope, installed to the workspace, and the bot
  invited to the target channel.
- **Discord:** a webhook created on the target channel.
- **Email:** a Communication Services resource with a verified sender domain, **or**, with the
  SMTP transport, an account on the organisation's SMTP relay (or a hosted SMTP service) allowed
  to send as the sender address, reachable from the worker on the configured port. If the relay
  accepts by network instead of login, leave the username and password blank.

## Risks

| Risk | Mitigation |
|---|---|
| The Workflows flow is disabled, its owner leaves, or tenant policy blocks Power Automate | The trigger stops answering `202`, so the channel is `failed` with the status code and the other three still deliver (AC-4). A `202` only means the flow accepted the card, so a broken flow can still look `sent`; the manual check covers this, and a later Graph notifier is a new `INotifier` and no change anywhere else. |
| Teams messages cannot be updated in place, because the webhook returns no message id | Accepted ([D3](requirement.md#decisions)); recorded against the Phase 2 update rule. |
| Four channels in one milestone is a large branch | The six task groups are independent after group 1 and each is a commit; groups 2 to 5 can also be split into separate pull requests if review prefers, with group 1 merged first. |
| Vendor payload limits are discovered at runtime, in production, on the longest report | Limits are asserted in unit tests against a deliberately oversized diagnosis (AC-9), not trusted to the happy path. |
| A channel outage slows the consumer | Per-channel timeout, sequential but bounded, one retry only, failures never propagate ([D8](requirement.md#decisions), AC-4). |
| A credential leaks into a log, a payload or a delivered message | Redacting settings records, one test per record, one test per builder, and a manual check (AC-10). |
| Replays post duplicate messages | Accepted and documented for M6 ([D7](requirement.md#decisions)); removed by the stable `incidentId` in M12 and the state store in M9. Recorded in the roadmap so it is not forgotten. |
| Slack's `200 OK` with `ok:false` is read as success | A named test exists for precisely this (AC-6), because it is the classic way to ship a silently broken Slack integration. |
| The SMTP password leaks, or is sent in clear | Redacting `SmtpSettings` with a test; reasons never carry server text or credentials ([D16](requirement.md#decisions)); TLS is required unless `None` is set explicitly, with no opportunistic mode ([D17](requirement.md#decisions)). |
| An SMTP relay accepts the message and drops it later (spam filter, SPF or DKIM failure) | Like a Teams `202`, an SMTP `250` means "accepted", not "delivered". The manual check against a real relay covers it; sender-domain authentication is the operator's prerequisite. |
| A misspelt transport silently disables email | The transport value is validated at startup and an unknown one stops the worker naming the key (AC-17). |
| A customer edits a template into something broken, or the file disappears at runtime | Any text is a valid template — an unknown token stays visible instead of failing — and a missing or unreadable file falls back to the embedded default with one warning ([D13](requirement.md#decisions), AC-15). |

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
- [ ] **Revision of 2026-11-08 (M6.7):** AC-16 to AC-18 covered and passing; AC-8, AC-10 and
      AC-13 re-proven; `SendAsync_AgainstAnSmtpServer_DeliversOneMessage` run against an SMTP server;
      MailKit recorded in tech-stack.md; roadmap M6.7 marked done; the 2026-11-08 Results block
      filled in
