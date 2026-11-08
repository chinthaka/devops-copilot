# Requirement: Notification channels

| | |
|---|---|
| **Branch** | `features/2026-11-05-m6-notification-channels` |
| **Milestone** | M6 – Notification channels, covering M6.1 to M6.6 and absorbing the former M15 ([roadmap](../roadmap.md#m6--notification-channels-l)) |
| **Status** | Done – including the revision of 2026-11-08 (SMTP email transport, M6.7) |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-11-05 |
| **Revised** | 2026-11-06 – Teams through a Workflows webhook (**D3**, replacing Graph; **D11** withdrawn); report and email templates (**D12**, **D13**) |
| **Revised** | 2026-11-07 – timeouts, the Discord retry cap and the Slack endpoint moved from code into configuration (non-functional requirements, [plan.md § Configuration](plan.md#configuration-and-secrets)) |
| **Revised** | 2026-11-08 – email can be sent through SMTP as well as Azure Communication Services, chosen in configuration (**D6** amended; **D14** to **D17**; **AC-16** to **AC-18**; roadmap M6.7) |

## Summary

Deliver stage 7's second half. The pipeline already renders the seven-section report and writes it
to a file; this feature sends that same report to the people who need it. One channel-neutral
notification is built from the `Diagnosis` and the two handoffs, and four delivery channels render
it: a Microsoft Teams Adaptive Card through a Teams Workflows webhook, a Slack Block Kit message
through `chat.postMessage`, a Discord embed through a channel webhook, and an email to every
configured address through Azure Communication Services or an SMTP server, whichever the
deployment selects (decision **D14**). The report and the email are rendered
from templates with embedded defaults, so a customer can change their wording by editing a file,
without a new deployment (decisions **D12** and **D13**).

Every channel is optional and independent. A channel that is not configured is recorded as
`unavailable`; a channel that fails is recorded as `failed`. Neither stops the other channels,
neither fails the run, and the event is still checkpointed.

## Why

[mission.md](../mission.md#what-devopscopilot-does) promises the on-call engineer a report that is
already waiting when they open the alert. Today the report is written to `incidents/` on the
worker's own disk, where nobody sees it, so every stage before this one delivers no value to a
human. [pipeline.md § 7](../../pipeline.md#7-report-and-notify) defines the channel behaviour that
the code does not yet have: one message per incident per channel, an unconfigured channel recorded
as `unavailable`, and text that matches the diagnosis and the hits.

The milestone is extended beyond the roadmap's original Teams-only M6 at the product owner's
request: Slack moves forward from M15, and Discord and email are added. The four channels are one
feature rather than four because they share everything that is hard — the channel-neutral
notification, the `INotifier` contract, the use case that fans out, the failure semantics and the
truncation rules. Only the payload shape and the transport differ per channel, which is exactly
the open/closed split [AGENTS.md § SOLID](../../../AGENTS.md#solid) asks for.

Email is included because a chat channel is a push to a room, not to a person. The requirement is
that **every incident produces an email to every configured address**, so an on-call engineer who
is not in the chat workspace still receives the report.

## Scope

**In scope**

- `IncidentNotification` in Domain: the channel-neutral content of one notification, built from the
  `Diagnosis`, the two handoffs and the written report's location. It is the single source of the
  text every channel renders, so the contract's "Teams and Slack text must match the diagnosis and
  the hits" holds by construction.
- `INotifier` in Application: one small interface per channel — its name, whether it is configured,
  and one `SendAsync`.
- `NotifyChannels` in Application: the stage-7 fan-out. It takes every registered `INotifier`,
  calls each one in a fixed order, and returns one `NotificationDelivery` per channel. It never
  throws and never knows which channels exist.
- **Microsoft Teams** (M6.2): an Adaptive Card posted to the trigger URL of a Teams Workflows
  "Post to a channel when a webhook request is received" flow, which posts it to its configured
  channel. See decision **D3**.
- **Slack** (M6.3): a Block Kit message posted with `chat.postMessage` and a bot token. See
  decision **D4**.
- **Discord** (M6.4): an embed posted to a channel webhook with `?wait=true` so the message id
  comes back. See decision **D5**.
- **Email** (M6.5): one message per report, sent to every configured address, with a subject, an
  HTML body and a plain-text alternative rendered from the email templates, through Azure
  Communication Services Email. See decisions **D6**, **D12** and **D13**.
- **Email over SMTP** (M6.7): the same email, sent through any SMTP server instead — an
  organisation's existing relay, a hosted SMTP service, or whatever local test server a developer prefers.
  Configuration selects the transport; the email channel, its content and its rules do not change.
  See decisions **D14** to **D17**.
- Templates for the report and the email: one placeholder renderer in Domain, embedded defaults
  that reproduce today's wording, and optional configured template files re-read on every render,
  so a customer changes the text without a deployment. See decisions **D12** and **D13**.
- Per-channel truncation: every channel has a hard payload limit, so each renderer truncates
  visibly and always carries the incident id and the location of the full report.
- Configuration for all four channels, read only in the Host, with redacting settings records so no
  token, secret or webhook URL can reach a log line.
- The report file keeps being written before any channel is called, byte-identical to today
  unless a custom report template is configured. See decisions **D2** and **D13**.

**Out of scope**

- **Updating a message in place.** [pipeline.md § 7](../../pipeline.md#7-report-and-notify) requires
  one message per incident per channel, updated when the incident changes. There is no incident
  (M12) and nowhere to persist a message id (M9) until Phase 2, so this feature posts one message
  per report and returns the message id without storing it. See decision **D7**.
- **The work-item link.** The contract's Teams row says the card links to the work item. Work items
  are M14; until then the card links to nothing and names the incident id and the report path.
- Threading, reactions, buttons, slash commands or any interaction coming back from a channel.
  Every channel here is one-way, which is why three of the four need no bot runtime.
- Per-channel routing rules, severity filters, quiet hours, digests or rate limiting beyond what
  the channel itself enforces. Every configured channel gets every report.
- Templating the chat payloads. The Teams card, the Slack blocks and the Discord embed stay
  code-built: their limits and truncation rules are vendor-specific and asserted by tests. If a
  customer later needs a custom card, templating it reuses the same renderer (**D12**).
- A template editor, template validation tooling or per-customer template storage. A template is
  one file on the worker's disk (in Azure, a mounted share); managing that file is operations.
- Dashboard counters for notifications. The dashboard gains counters with M11.3.
- Azure DevOps work items (M14), the learn loop (M16) and anything keyed by `incidentId`
  (M12, M13).
- The leftover sample project `src/DevOpsCopilot.EmailNotifier`. It is not extended, not reused and
  not deleted here; M18 removes it. The new email channel lives in
  `src/DevOpsCopilot.Infrastructure/Reporting/`.
- Sending one email through both transports, or failing over from one to the other. Exactly one
  transport is selected per deployment (**D14**).
- SMTP authentication other than username and password: no OAuth 2.0 (`XOAUTH2`), no NTLM or
  Kerberos, no client certificates. A relay that needs them is a later transport.
- Opportunistic TLS. SMTP over TLS is required unless the deployment explicitly says `None`
  (**D17**).
- Choosing or shipping a local SMTP test server. The project prescribes no tool: any server that
  speaks SMTP satisfies the gated integration test and the manual checks, and each developer picks
  their own. Nothing is added to the AppHost for it.
- Provisioning: creating the Teams Workflows flow and its trigger URL, the Slack
  app, the Discord webhook, the Communication Services resource and the SMTP relay account are operations tasks, listed as
  manual checks and carried out alongside M7.3.

## Decisions

**D1 – One channel-neutral notification, four renderings.**
`IncidentNotification` is built once in Domain from the diagnosis and the handoffs and holds no
channel vocabulary. Each channel renderer maps that object into its own payload and adds nothing of
its own. A renderer may drop or shorten content to fit a limit; it may never add a claim. This is
how the contract's "text must match the diagnosis and the hits" is made testable: one test per
channel asserts the payload's text comes from the notification, and no renderer sees the model.

**D2 – The report file is always written, and it is the only delivery when nothing is configured.**
Today `RunIncidentPipeline` always writes the report through `IReportWriter`. That behaviour is
kept, and the file is written *before* any channel is called, so a channel outage never costs us
the report and M5's end-to-end test keeps passing byte for byte. The contract sentence "when no
channel is configured … write the report to `incidents/`" is widened to say the file is always
written and is the only delivery when no channel is configured. This is a contract change, written
out in [plan.md § Contract changes](plan.md#contract-changes).

**D3 – Teams uses a Workflows webhook, not Microsoft Graph.**
The product owner chose the Teams **Workflows** app's "Post to a channel when a webhook request is
received" flow. Its trigger URL accepts an HTTP POST whose body carries one Adaptive Card as an
`application/vnd.microsoft.card.adaptive` attachment, and the flow posts that card to the channel
chosen when the flow was created. It needs no app registration, no client secret, no Teams app
manifest and no resource-specific consent, which removes the largest operational cost of the
earlier Graph design; the retired Office 365 connector incoming webhooks are not used. Two
consequences are stated plainly. First, the trigger URL carries a shared-access signature, so it
is a credential exactly like Discord's webhook URL (**D5**) and is redacted wherever it could be
printed. Second, the trigger answers `202 Accepted` with no body: the flow runs asynchronously, so
there is **no message id or permalink** to return, and a `202` means "accepted by the flow", not
"rendered in the channel". The Teams delivery is therefore `sent` with no message id, and editing
the message in place (**D7**, Phase 2) is not possible on this channel; a later move to Graph is a
new `INotifier` and no change anywhere else.

**D4 – Slack uses `chat.postMessage` with a bot token, not an incoming webhook.**
App-based incoming webhooks are still supported, but they are pinned to one channel and cannot
update or delete a message. `chat.postMessage` with a bot token and the `chat:write` scope takes
any channel the bot is in and is the only path that can later call `chat.update`, which is what
the contract's update-in-place rule needs in Phase 2. Both accept the same Block Kit `blocks`
payload, so choosing it now costs nothing and avoids a rewrite. Slack answers `200 OK` with
`"ok": false` on failure, so the response body is checked, not the status code.

**D5 – Discord uses a channel webhook, not a bot.**
A bot exists to receive interactions; this channel sends. A webhook is one HTTP POST, needs no
gateway connection, no application review and no bot token, and supports embeds and later editing
through `PATCH /webhooks/{id}/{token}/messages/{id}`. The webhook is executed with `?wait=true` so
Discord returns the created message and its id; without it the response is an empty `204` and
there is nothing to report back. The webhook URL is a credential: anyone holding it can post to the
channel, so it is treated exactly like a token.

**D6 – Email goes through Azure Communication Services, as one message to all recipients.**
The product is Azure-hosted and already uses `Azure.Identity`; Communication Services Email is the
Azure-native path and needs no SMTP relay or mailbox, so it stays the transport shipped in
`appsettings.json`; **D14** (2026-11-08) adds SMTP as an alternative a deployment can select.
One email is sent per report with every
configured address in `To` — the recipients are one on-call team, and a shared `To` means a reply
reaches everyone. The send is started with `WaitUntil.Started` and the operation id is recorded:
the pipeline must not block on delivery polling, and a queued message is a success for our
purposes.

**D7 – One message per report, and the id is returned but not stored.**
Message identity belongs to the incident, and incidents arrive in M12 with the state store in M9.
Until then each report posts a new message. `SendAsync` returns the channel's message id and
permalink where the channel gives one, the delivery result carries them, and `NotifyChannels` logs
them, so the plumbing an update needs already exists when M12 lands. Nothing is persisted, and no
channel is told it is an update.

**D8 – Transport failure is recorded, never propagated; one retry at most.**
A channel that throws, times out or answers with an error is recorded as `failed` with the reason,
and the next channel still runs. The event is still checkpointed. A `429` with a `Retry-After`
header is retried once, after that header's delay, capped at the per-channel timeout; nothing else
is retried. This keeps stage 7 bounded in the same way principle 3 bounds the investigation, and it
keeps a channel outage from stalling the consumer.

**D9 – Channels are notified sequentially, in a fixed order.**
Teams, Slack, Discord, then email. Sequential delivery matches "stages do not run in parallel" in
the contract, keeps the per-event cost and timing predictable, and makes the delivery list
deterministic in tests. Four short HTTP calls per incident is not a throughput problem at the
volumes triage and correlation will produce.

**D10 – The payload builders live in Infrastructure, the notification in Domain.**
An Adaptive Card, a Block Kit block and a Discord embed are vendor wire formats, so they are
mapped at the Infrastructure boundary like every other external shape. The notification itself is
the product's own content and stays in Domain next to `IncidentReport`, with no packages. All of it
is unit-testable without a network: the builders are pure functions over `IncidentNotification`.

**D11 – Withdrawn.** The 2026-11-06 revision chose the `Microsoft.Graph` SDK for Teams; the same
day the product owner replaced Graph with a Workflows webhook (**D3**). No Graph package or Teams
app-only identity is added. The number is kept so later references stay stable.

**D12 – One placeholder renderer in Domain, and no templating package.**
The report and the email render through one pure Domain function: `{{Placeholder}}` tokens are
replaced from a map of named values, a list value expands to one `- ` line per entry (or to the
section's "unavailable" sentence when the tool returned nothing), and a token that names nothing
is left visible in the output, so a typo in a custom template is seen rather than silently
swallowed. A template can reorder, relabel, drop or translate content, but it cannot add a claim,
which keeps **D1** and the evidence rule intact. Scriban, Handlebars.Net and Razor were rejected:
Domain references no package, and substitution plus lists is everything these two documents need.

**D13 – Templates are files read at render time; the embedded default is the fallback.**
Each template — the report, the email subject, the email HTML body and the email plain-text body —
has an embedded default and an optional configured path. The path is re-read on every render:
reports are rare, so the cost is nothing, and an edited file takes effect on the next event with
no restart and no deployment. A path that is configured but missing or unreadable falls back to
the embedded default and logs one warning naming the path; a template problem never costs the
report or the email. The default report template reproduces today's report byte for byte, so M5's
end-to-end test and AC-11 hold wherever no custom template is configured. In Azure the template
files come from a mounted Azure Files share, so an edit needs no image rebuild; locally they are
any path on disk.

**D14 – SMTP is a second email transport, selected in configuration; email stays one channel.**
Added 2026-11-08 at the product owner's request: not every organisation can provision
Communication Services, and most already run an SMTP relay, so the email channel can send through
either. `Incident:Notifications:Email:Transport` names the transport, `CommunicationServices` or
`Smtp`, and exactly one is used. It is not a fifth channel: the notification, the templates, the
recipients-in-`To` rule (**D6**), the delivery and its place in the fixed order (**D9**) are the
same whichever transport carries the message. SMTP is therefore a new `IEmailTransport`
implementation beside `CommunicationServicesEmailTransport`, which is the open/closed rule of
[AGENTS.md § SOLID](../../../AGENTS.md#solid): `EmailNotifier` keeps no `if` or `switch` on the
transport, Application learns nothing, and the choice is made once, in the composition. The
transport value has no default in code; `appsettings.json` ships `CommunicationServices`, which
keeps today's behaviour, and an unknown value stops the worker at startup naming the key, as the
other configuration checks do. A selected transport whose own settings are blank leaves the
channel `unavailable`, exactly as a blank connection string does today.

**D15 – MailKit, not `System.Net.Mail.SmtpClient`.**
The base class library's `SmtpClient` needs no package, but Microsoft no longer recommends it for
new development: it lacks modern protocol support and points users to MailKit. MailKit (with its
MIME library MimeKit) is the maintained .NET SMTP client, supports STARTTLS and implicit TLS
properly, accepts a `CancellationToken` on every call, and builds standards-compliant
`multipart/alternative` messages. It is referenced only by Infrastructure and recorded in
[tech-stack.md](../tech-stack.md) in the same change.

**D16 – Each transport maps its own failures; the notifier stays transport-neutral.**
Today `EmailNotifier` catches the Communication Services `RequestFailedException` itself, so a
second transport would mean a second vendor exception in the notifier. Instead each transport
turns its own failures into an `EmailTransportException` carrying a safe reason, and the notifier
records that reason as a `failed` delivery. The Communication Services reason is unchanged — its
status and error code. The SMTP reason names the server's reply code and MailKit's error category
(for example `550 (RecipientNotAccepted)`), a rejected login, or a failed TLS handshake, and
anything else names only the exception type. No reason ever carries the password, the username,
the connection string or the server's free-text reply, which can echo addresses. A cancelled caller
still propagates from every transport, because that is shutdown or the stage's timeout.

**D17 – SMTP security is explicit, and the message id is the `Message-ID` header.**
`Incident:Notifications:Email:Smtp:Security` is one of `StartTls` (upgrade on connect and refuse to
continue without it; the usual port 587), `SslOnConnect` (implicit TLS; the usual port 465) or
`None` (plain text, meant only for a local test server). There is no
"TLS when available" mode, because a downgrade would quietly send the password in clear. The
username and password are optional — an internal relay may accept the sender by network — but
are set together or not at all; one without the other stops the worker at startup. The password is
a credential and is never printed. SMTP returns no operation id, so the delivery's message id is
the message's RFC 5322 `Message-ID`, generated for each message. That is also the value a
follow-up email will reference when reports are updated (M15). The send is bounded by the stage's
per-channel timeout through the cancellation token, so SMTP adds no timing setting of its own.

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | The structured diagnosis from stage 6 | [pipeline.md § 6 – Diagnosis](../../pipeline.md#6-diagnose-diagnosis-agent) |
| **Input** | The diagnostic and grounding handoffs, for the evidence and hit text | [§ 4](../../pipeline.md#diagnostic-handoff), [§ 5](../../pipeline.md#grounding-handoff) |
| **Input** | The path of the written report | [pipeline.md § 7](../../pipeline.md#7-report-and-notify) |
| **Output** | One `IncidentNotification`, rendered to one message per configured channel | [pipeline.md § 7 – Channels](../../pipeline.md#7-report-and-notify) |
| **Output** | One `NotificationDelivery` per channel: `sent`, `unavailable` or `failed`, with the message id and permalink where the channel returns them | New; defined in [plan.md](plan.md#interfaces) |

## Contract and rules touched

- [pipeline.md § 7 – Report and notify](../../pipeline.md#7-report-and-notify) – implemented by this
  feature. **Changed:** the channel table gains Discord and email rows, the email rule is stated,
  the file-report sentence is widened per **D2**, the update-in-place rule is marked as arriving
  with M12, and the report-template sentence is added per **D13**. The wording is in
  [plan.md § Contract changes](plan.md#contract-changes).
- [pipeline.md § Evidence rule](../../pipeline.md#evidence-rule) – a channel that is not configured
  or not reachable is recorded as `unavailable` or `failed`; no renderer invents content to fill a
  gap.
- [mission.md § Principles, 1 and 2](../mission.md#principles) – no payload contains a command,
  script or template that changes a resource, and every line of every payload comes from the
  diagnosis.
- [mission.md § Boundaries](../mission.md#boundaries) – **changed:** the in-scope channel list
  gains Discord and email.

## Domain language

| Term | Meaning in this feature |
|---|---|
| **Notification** | The channel-neutral content of one message about one report. Built from the diagnosis; holds no channel vocabulary. |
| **Channel** | One delivery destination: Teams, Slack, Discord or email. Each is an `INotifier`. |
| **Transport** | How the email channel hands a message over: Azure Communication Services or SMTP. One per deployment; not a channel and not a term Application knows. |
| **Delivery** | The outcome of one channel for one report: `sent`, `unavailable` or `failed`, with a message id where there is one. |
| **Report** | Unchanged in substance: the seven-section document of [pipeline.md § 7](../../pipeline.md#report-sections-in-this-order). A notification summarises it; it does not replace it. |
| **Template** | The text shape of the report or of the email: fixed wording plus `{{placeholder}}` tokens filled from the diagnosis and the handoffs. An embedded default ships with the worker; a configured file replaces it without a deployment. |

`notification`, `channel`, `delivery` and `template` are added to the contract's vocabulary in the
same pull request.

## Acceptance criteria

**AC-1** – The notification carries the report's content and nothing else
- **Given** a diagnosis with a title, likely cause, confidence, impact, evidence, similar
  incidents, suggested steps and open questions, and the two handoffs behind it
- **When** an `IncidentNotification` is built
- **Then** every line of it is drawn from those inputs, it names the incident id, the confidence
  and where the full report was written, and it contains no text that is not in the diagnosis or
  the handoffs

**AC-2** – Every configured channel is notified once per report, in a fixed order
- **Given** four configured notifiers
- **When** the pipeline runs one error event
- **Then** each notifier is called exactly once, in the order Teams, Slack, Discord, email, and the
  result lists four deliveries with status `sent`

**AC-3** – An unconfigured channel is `unavailable` and does not fail the run
- **Given** no configuration for any channel
- **When** the pipeline runs one error event
- **Then** every channel is recorded as `unavailable`, no transport call is made, the pipeline
  returns normally and the report file is still written

**AC-4** – A failing channel does not stop the others
- **Given** four configured notifiers of which the first throws and the second answers `500`
- **When** the pipeline runs one error event
- **Then** the two are recorded as `failed` with the reason, the remaining two are still called and
  recorded as `sent`, no exception escapes the stage, and the event is still checkpointed

**AC-5** – Teams receives an Adaptive Card through a Workflows webhook
- **Given** a configured Workflows trigger URL
- **When** a report is notified
- **Then** one POST is made to that URL whose body is a `message` carrying exactly one
  `application/vnd.microsoft.card.adaptive` attachment, the card's text comes from the
  notification, a `202 Accepted` is recorded as `sent` with no message id, and any other status is
  recorded as `failed` with the status code and never with the URL

**AC-6** – Slack receives a Block Kit message, and `ok:false` is a failure
- **Given** a configured bot token and channel id
- **When** a report is notified
- **Then** one `chat.postMessage` call is made with the bot token and a `blocks` array built from
  the notification; a response of `200 OK` carrying `"ok": false` is recorded as `failed` with
  Slack's error code, and a successful response's `ts` is reported back as the message id

**AC-7** – Discord receives an embed and returns its message id
- **Given** a configured webhook URL
- **When** a report is notified
- **Then** one POST is made to that URL with `wait=true` and one embed built from the notification,
  and the created message's id is reported back

**AC-8** – Email reaches every configured address, for every report
- **Given** a configured Communication Services connection, sender address and three recipients
- **When** a report is notified
- **Then** exactly one email is sent through the selected transport, with all three addresses in `To`, a subject naming the
  incident and its confidence, an HTML body and a plain-text alternative both rendered from the
  email templates and carrying the notification's facts, and the operation id reported back — and this happens for every report, whatever
  the other channels did

**AC-9** – Oversized content is truncated, never dropped silently
- **Given** a diagnosis whose cause, steps and open questions exceed every channel's limit
- **When** the report is notified
- **Then** each payload is within that channel's documented limits (Slack 3 000 characters per text
  block and 50 blocks; Discord 4 096 per embed description, 6 000 across embeds, 10 embeds; Teams
  card body), each truncation is marked visibly, and each payload still names the incident id and
  where the full report is

**AC-10** – No secret is logged or rendered
- **Given** every channel configured with a token, a secret, a webhook URL, a connection string and
  an SMTP username and password
- **When** the settings are stringified, the stage logs a delivery, and every payload is built
- **Then** no secret value appears in any of them; each settings record prints `(set)` or `(none)`
  in place of the value, as `AzureOpenAISettings` already does

**AC-11** – The report file behaviour is unchanged
- **Given** the pipeline with no channel configured and no custom report template
- **When** one error event is processed
- **Then** the report written to `incidents/YYYY-MM-DD-<event-id>.md` is byte-identical to what M5
  produces today, and it is written before any channel is called

**AC-12** – A new channel needs no change to the use case
- **Given** the notify stage and a fake notifier registered alongside the four real ones
- **When** a report is notified
- **Then** the fake is called and appears in the deliveries, with no edit to `NotifyChannels`, no
  `if` or `switch` on a channel name anywhere in Application, and no channel type named in
  Application

**AC-13** – The layers stay clean
- **Given** the solution
- **When** the architecture tests run
- **Then** the Communication Services package, MailKit and every HTTP client are
  referenced only by Infrastructure, Application still references no Azure SDK or HTTP client,
  Domain still references no package, and all dependencies point inwards

**AC-14** – The report and the email render from templates, and an edit needs no deployment
- **Given** a running worker with a custom report template and custom email templates configured
- **When** one error event is processed, the template files are edited, and a second error event
  is processed
- **Then** the first report and email follow the first wording, the second follow the edited
  wording, every placeholder is filled from the notification or the handoffs, and the worker was
  neither restarted nor redeployed

**AC-15** – A missing or broken template never costs the report
- **Given** a configured template path whose file does not exist
- **When** an error event is processed
- **Then** the report and the email are produced from the embedded defaults, one warning names the
  missing path, and the run completes normally

**AC-16** – Email can be sent through an SMTP server
- **Given** the transport set to `Smtp`, a host, port and security mode, a sender and three
  recipients
- **When** a report is notified
- **Then** exactly one message is handed to the SMTP server, from the configured sender, with all
  three addresses in `To`, the subject from the email templates, and a `multipart/alternative` body
  carrying the plain-text and HTML renderings; the delivery is `sent` and its message id is the
  message's `Message-ID`; the username and password are used only when set

**AC-17** – Configuration selects exactly one transport
- **Given** the transport set to `CommunicationServices`, then to `Smtp`, then to an unknown value
- **When** the worker starts
- **Then** the first sends through Communication Services and never opens an SMTP connection, the
  second sends through SMTP and never creates a Communication Services client, and the third stops
  startup with an error naming `Incident:Notifications:Email:Transport`; in the first two cases the
  channel is `unavailable` when the selected transport's own settings are blank, whatever the other
  transport's settings hold

**AC-18** – An SMTP failure is recorded safely
- **Given** an SMTP server that rejects a recipient, rejects the login, fails the TLS handshake or
  cannot be reached
- **When** a report is notified
- **Then** the email delivery is `failed` with a reason naming the reply code and error category,
  the rejected login, the TLS failure or the exception type respectively; the reason contains
  neither the password, the username nor the server's free-text reply; and the other channels are
  unaffected

## Non-functional requirements

- **Bounded cost per incident.** At most one message per channel per report, at most one retry per
  channel (only on `429` with `Retry-After`, within the configured cap), and a configured
  per-channel timeout (10 seconds in the shipped `appsettings.json`). Stage 7 can therefore add at
  most four timeouts to one event — ~40 seconds with the shipped values — and the worker keeps
  consuming. The SMTP transport's connect, login and send all run under the same per-channel
  timeout.
- **No configuration in code.** Timeouts, the retry cap, endpoints and the email transport come
  from configuration with no fallback value in code; a missing or invalid value stops the worker at
  startup with an error naming the key (revision of 2026-11-07, extended by **D14** and **D17**).
- **Idempotency.** Notification is at-least-once: if the worker crashes after sending and before
  checkpointing, the event is replayed and the message is posted again. This is accepted for M6 and
  disappears when M12 gives messages a stable incident id. It is recorded as a known limitation in
  the roadmap, not hidden.
- **Secrets.** Tokens, client secrets, webhook URLs, connection strings and the SMTP password come from user secrets
  locally and from Azure configuration in a deployed environment. None is committed, logged or
  rendered.
- **Determinism.** The same notification produces byte-identical payloads, so the renderer tests
  can compare documents rather than fish for substrings.

## Open questions

- **For review of the 2026-11-08 revision:** the Communication Services keys stay where they are
  (`Email:ConnectionString`, `Email:Endpoint`) so existing secrets and docs keep working, while
  SMTP gets its own `Email:Smtp` section. Moving the first two under `Email:CommunicationServices`
  would be more symmetrical; it breaks existing secrets and is a small change if review prefers it.
- **For review of the 2026-11-08 revision:** `Security = None` is allowed in every environment and
  documented as local-only. Refusing it outside `Development` is a one-line startup check if
  review prefers it.
- None blocking otherwise. Two choices are recorded as decisions rather than questions and can be revisited
  in review: the email recipients are in `To` rather than `Bcc` (**D6**), and Teams delivery
  returns no message id, so it cannot be updated in place later (**D3**). If review
  prefers `Bcc`, it is a one-line change in the email renderer and one test name.
