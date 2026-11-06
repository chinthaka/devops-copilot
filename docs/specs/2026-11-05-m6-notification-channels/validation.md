# Validation: Notification channels

Every acceptance criterion in [requirement.md](requirement.md#acceptance-criteria) appears here at
least once. Tests are written before the code they cover, per
[AGENTS.md](../../../AGENTS.md#how-to-work). Live channels cannot be unit tested, so each one has a
gated integration test and the whole payload-and-failure surface is covered by Infrastructure unit
tests driven by a stub `HttpMessageHandler`, in the way M5's `FakeChatClient` covers Azure OpenAI.

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `From_WithACompleteDiagnosis_CarriesTheTitleCauseConfidenceImpactAndReportLocation`, `From_WithADiagnosis_AddsNoTextThatIsNotInTheDiagnosisOrTheHandoffs`, `From_WhenBothToolsWereUnavailable_SaysSoInsteadOfListingEvidence` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-2 | `NotifyAsync_WithFourConfiguredChannels_CallsEachOnceInTheContractOrder`, `RunAsync_WithAConfiguredChannel_NotifiesItAfterWritingTheReport` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-3 | `NotifyAsync_WithNoConfiguredChannel_RecordsEveryChannelUnavailableWithoutCallingIt`, `RunAsync_WithNoConfiguredChannel_StillWritesTheReportAndReturnsNormally` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-4 | `NotifyAsync_WhenAChannelThrows_RecordsItFailedAndStillCallsTheRest`, `NotifyAsync_WhenAChannelTimesOut_RecordsItFailedWithTheTimeoutAsTheReason`, `RunAsync_WhenEveryChannelFails_StillCheckpointsTheEvent` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-5 | `Build_WithACompleteNotification_ProducesAnAdaptiveCardAttachmentReferencedFromTheBody`, `SendAsync_WithAConfiguredTeam_PostsTheCardToTheChannelMessagesEndpointWithAnAppOnlyToken`, `SendAsync_WhenGraphReturnsTheMessage_ReportsItsIdBack` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-5 | `SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `Build_WithACompleteNotification_ProducesBlocksWhoseTextComesFromTheNotification`, `SendAsync_WithAConfiguredBotToken_CallsChatPostMessageWithTheBlocks`, `SendAsync_WhenSlackAnswersOkFalse_RecordsItFailedWithSlacksErrorCode`, `SendAsync_WhenSlackAnswersOk_ReportsTheTimestampAsTheMessageId` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `Build_WithACompleteNotification_ProducesOneEmbedWithTheReportFields`, `SendAsync_WithAConfiguredWebhook_PostsWithWaitTrueAndReportsTheMessageId` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-8 | `Build_WithACompleteNotification_ProducesAnHtmlBodyAndAPlainTextAlternative`, `Build_WithANotification_SubjectNamesTheIncidentAndTheConfidence`, `SendAsync_WithThreeConfiguredRecipients_SendsOneMessageAddressedToAllOfThem`, `SendAsync_WithoutASenderOrRecipients_ReportsItselfUnconfigured` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-8 | `SendAsync_AgainstARealCommunicationServicesResource_QueuesOneEmail` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-9 | `Build_WithAnOversizedDiagnosis_KeepsEveryBlockWithinSlacksLimits`, `Build_WithAnOversizedDiagnosis_KeepsTheEmbedWithinDiscordsLimits`, `Build_WithAnOversizedDiagnosis_TruncatesTheCardVisibly`, `Build_WithAnOversizedDiagnosis_StillNamesTheIncidentAndTheReportLocation` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-10 | `ToString_WithASecret_DoesNotPrintIt` (theory over the four settings records), `NotifyAsync_WhenAChannelFails_LogsTheReasonWithoutTheCredential`, `Build_ForEveryChannel_PutsNoCredentialInThePayload` | Unit | Infrastructure, Application | `tests/DevOpsCopilot.Infrastructure.Tests`, `tests/DevOpsCopilot.Application.Tests` |
| AC-11 | `RunAsync_WithNotificationAdded_WritesTheSameReportBytesAsBefore`, `RunAsync_WhenAChannelIsConfigured_WritesTheReportBeforeCallingAnyChannel` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-12 | `NotifyAsync_WithAnAdditionalNotifierRegistered_CallsItWithoutAnyChangeToTheStage`, `Application_WhenInspectedForChannelNames_MentionsNoChannelType` | Unit, Architecture | Application | `tests/DevOpsCopilot.Application.Tests`, `tests/DevOpsCopilot.Architecture.Tests` |
| AC-13 | `NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure`, `Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk`, existing layer rules | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-2, AC-3 | `EndToEnd_AnErrorEventWithNoChannelConfigured_WritesTheReportAndRecordsEveryChannelUnavailable` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |
| AC-2, AC-10 | `AddInfrastructure_WithEachChannelConfigured_RegistersThatNotifier` (theory), `AddInfrastructure_WithNoChannelConfigured_RegistersFourUnconfiguredNotifiers` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |

## Unit tests (written first)

### `tests/DevOpsCopilot.Domain.Tests` — the notification, pure

- **`From_WithACompleteDiagnosis_CarriesTheTitleCauseConfidenceImpactAndReportLocation`** (AC-1) –
  Arrange: a diagnosis with every field filled, both handoffs `ok`, and a report path. Act: build
  the notification. Assert: title, likely cause, confidence, impact line, evidence lines, similar
  incidents, suggested steps, open questions, incident id and report path are all present and equal
  to their sources.
- **`From_WithADiagnosis_AddsNoTextThatIsNotInTheDiagnosisOrTheHandoffs`** (AC-1) – Arrange: as
  above. Act: build. Assert: every evidence line is one of the handoff's findings and every similar
  incident is one of the grounding hits; the notification introduces no sentence of its own beyond
  its fixed labels.
- **`From_WhenBothToolsWereUnavailable_SaysSoInsteadOfListingEvidence`** (AC-1) – Arrange: both
  statuses `unavailable`, no rows, no hits. Act: build. Assert: the evidence and similar-incident
  sections state the tool was unavailable and list nothing, mirroring `IncidentReport`.
- **`From_WithTheSameInputs_ProducesAnEqualNotification`** (NFR, determinism) – Arrange: the same
  inputs twice. Act: build both. Assert: the two are equal field by field.

### `tests/DevOpsCopilot.Application.Tests` — the stage, with fake notifiers

A `FakeNotifier` records its calls and is configured to succeed, throw, hang or report itself
unconfigured.

- **`NotifyAsync_WithFourConfiguredChannels_CallsEachOnceInTheContractOrder`** (AC-2) – Arrange:
  four fakes named for the four channels. Act: notify. Assert: each was called once and the
  recorded call order is Teams, Slack, Discord, email.
- **`RunAsync_WithAConfiguredChannel_NotifiesItAfterWritingTheReport`** (AC-2, AC-11) – Arrange:
  the full pipeline with a recording report writer and one fake notifier. Act: run one error event.
  Assert: the writer ran before the notifier, and the notification carries the path the writer
  returned.
- **`NotifyAsync_WithNoConfiguredChannel_RecordsEveryChannelUnavailableWithoutCallingIt`** (AC-3) –
  Arrange: four fakes reporting `IsConfigured` false. Act: notify. Assert: four deliveries, all
  `unavailable`, and no fake saw a send.
- **`RunAsync_WithNoConfiguredChannel_StillWritesTheReportAndReturnsNormally`** (AC-3) – Arrange:
  the pipeline with no notifier registered at all. Act: run one error event. Assert: a report path
  is returned and no exception is thrown — the empty-collection case, which is today's behaviour.
- **`NotifyAsync_WhenAChannelThrows_RecordsItFailedAndStillCallsTheRest`** (AC-4) – Arrange: four
  fakes, the first throwing. Act: notify. Assert: delivery one is `failed` with the exception
  message as the reason, the other three were called and are `sent`, and nothing propagated.
- **`NotifyAsync_WhenAChannelTimesOut_RecordsItFailedWithTheTimeoutAsTheReason`** (AC-4) – Arrange:
  a fake that waits on the cancellation token and a stage built with a zero per-channel timeout.
  Act: notify. Assert: `failed`, the reason names the timeout, and the following channel still ran.
  (A `TimeSpan` on the stage, as M5's diagnose timeout does; the repo has no `FakeTimeProvider`.)
- **`NotifyAsync_WhenShutdownIsRequestedWhileAChannelSends_PropagatesTheCancellation`** (AC-4) –
  Arrange: an already-cancelled caller token. Act: notify. Assert: an `OperationCanceledException`
  propagates. A cancelled caller is a shutdown, not a channel failure — the same carve-out M5 makes
  for the model.
- **`RunAsync_WhenEveryChannelFails_StillCheckpointsTheEvent`** (AC-4) – Arrange: `ProcessLogEvents`
  with four throwing notifiers. Act: run one event. Assert: the checkpoint delegate ran once and
  the observer saw no error.
- **`NotifyAsync_WhenAChannelFails_LogsTheReasonWithoutTheCredential`** (AC-10) – Arrange: a fake
  whose failure message would contain a token if the stage echoed its settings. Act: notify.
  Assert: the log line names the channel and the reason and contains no credential.
- **`NotifyAsync_WithAnAdditionalNotifierRegistered_CallsItWithoutAnyChangeToTheStage`** (AC-12) –
  Arrange: the four fakes plus a fifth of an unknown name. Act: notify. Assert: five deliveries,
  the fifth included, proving the stage resolves channels rather than naming them.
- **`RunAsync_WithNotificationAdded_WritesTheSameReportBytesAsBefore`** (AC-11) – Arrange: the
  handoffs from M5's existing pipeline test and no configured channel. Act: run. Assert: the
  written document equals the expected seven-section report byte for byte.

Existing Application tests must keep passing unchanged, in particular
`RunAsync_WithoutAConfiguredModel_StillWritesAFullReport` and the `Warning`-does-not-start-the-team
test.

### `tests/DevOpsCopilot.Infrastructure.Tests` — the four channels, no network

Driven by `StubHttpMessageHandler`, which answers with whatever status and body the test gives it
and records the request it received. The payload builders are pure and tested directly.

**Teams** (AC-5, AC-9, AC-10)

- **`Build_WithACompleteNotification_ProducesAnAdaptiveCardAttachmentReferencedFromTheBody`** –
  Assert: one attachment with `contentType` `application/vnd.microsoft.card.adaptive`, a GUID id,
  the serialised card in `content`, and `body.content` carrying the matching
  `<attachment id="…"></attachment>` placeholder.
- **`Build_WithACompleteNotification_PutsTheNotificationsTextInTheCard`** – Assert: title, likely
  cause, confidence, impact, evidence, similar incidents, suggested steps and open questions all
  appear, and the incident id and report location are present.
- **`SendAsync_WithAConfiguredTeam_PostsTheCardToTheChannelMessagesEndpointWithAnAppOnlyToken`** –
  Assert: one `POST` to `/v1.0/teams/{teamId}/channels/{channelId}/messages` with an
  `Authorization: Bearer` header from the token provider and the built body.
- **`SendAsync_WhenGraphReturnsTheMessage_ReportsItsIdBack`** – Assert: the delivery is `sent` and
  carries the `id` and `webUrl` from the response.
- **`SendAsync_WhenGraphRejectsTheToken_RecordsItFailedWithTheGraphErrorCode`** – Arrange: a `403`
  with a Graph error body (the shape returned when RSC is missing). Assert: `failed`, the reason
  names the Graph error code, nothing throws.
- **`SendAsync_WithoutATeamOrChannel_ReportsItselfUnconfigured`** – theory over blank tenant,
  client, team and channel.
- **`Build_WithAnOversizedDiagnosis_TruncatesTheCardVisibly`** (AC-9).

**Slack** (AC-6, AC-9)

- **`Build_WithACompleteNotification_ProducesBlocksWhoseTextComesFromTheNotification`**.
- **`SendAsync_WithAConfiguredBotToken_CallsChatPostMessageWithTheBlocks`** – Assert: one `POST` to
  `https://slack.com/api/chat.postMessage`, the bot token in the `Authorization` header, the
  channel id and the blocks in the body.
- **`SendAsync_WhenSlackAnswersOkFalse_RecordsItFailedWithSlacksErrorCode`** – Arrange: `200 OK`
  with `{"ok":false,"error":"channel_not_found"}`. Assert: `failed`, reason `channel_not_found`.
  The whole point of the test: a `200` is not a success here.
- **`SendAsync_WhenSlackAnswersOk_ReportsTheTimestampAsTheMessageId`**.
- **`Build_WithAnOversizedDiagnosis_KeepsEveryBlockWithinSlacksLimits`** (AC-9) – 3 000 characters
  per text block, at most 50 blocks.
- **`SendAsync_WithoutABotTokenOrChannel_ReportsItselfUnconfigured`** – theory.

**Discord** (AC-7, AC-9)

- **`Build_WithACompleteNotification_ProducesOneEmbedWithTheReportFields`**.
- **`SendAsync_WithAConfiguredWebhook_PostsWithWaitTrueAndReportsTheMessageId`** – Assert: the
  query string contains `wait=true` and the delivery carries the returned message id.
- **`SendAsync_WhenDiscordRateLimits_RetriesOnceAfterRetryAfterThenSucceeds`** (AC-4, D8) – Arrange:
  a `429` carrying `Retry-After`, then a `200`. Assert: exactly two requests and a `sent` delivery.
- **`SendAsync_WhenDiscordRateLimitsTwice_RecordsItFailedWithoutRetryingAgain`** (D8) – Assert:
  exactly two requests and a `failed` delivery. The retry budget is one.
- **`Build_WithAnOversizedDiagnosis_KeepsTheEmbedWithinDiscordsLimits`** (AC-9) – 4 096 characters
  of description, 6 000 across the embed, at most 10 embeds.
- **`SendAsync_WithoutAWebhookUrl_ReportsItselfUnconfigured`**.

**Email** (AC-8, AC-9, AC-10)

- **`Build_WithACompleteNotification_ProducesAnHtmlBodyAndAPlainTextAlternative`** – Assert: both
  bodies carry the same facts and the plain text is readable without HTML.
- **`Build_WithANotification_SubjectNamesTheIncidentAndTheConfidence`**.
- **`SendAsync_WithThreeConfiguredRecipients_SendsOneMessageAddressedToAllOfThem`** – Arrange: a
  fake email transport behind the thin interface the notifier depends on. Assert: one send, three
  `To` addresses, the sender from configuration.
- **`SendAsync_WhenTheTransportFails_RecordsItFailedWithTheErrorCode`**.
- **`SendAsync_WithoutASenderOrRecipients_ReportsItselfUnconfigured`** – theory over a blank
  connection, a blank sender and an empty recipient list.

**Settings and wiring** (AC-2, AC-10, AC-13)

- **`ToString_WithASecret_DoesNotPrintIt`** – theory over `TeamsSettings`, `SlackSettings`,
  `DiscordSettings` and `EmailSettings`: each prints `(set)` or `(none)` in place of the client
  secret, bot token, webhook URL and connection string.
- **`AddInfrastructure_WithEachChannelConfigured_RegistersThatNotifier`** – theory over the four
  channels: the registered `INotifier` set contains that channel and it reports itself configured.
- **`AddInfrastructure_WithNoChannelConfigured_RegistersFourUnconfiguredNotifiers`** – the worker
  always resolves four notifiers; configuration decides only whether they send.
- **`Build_ForEveryChannel_PutsNoCredentialInThePayload`** (AC-10) – theory over the four builders
  with credential-bearing settings: no payload contains the credential.

## Integration tests

Each is gated on its own configuration being present in user secrets or the environment, in the
way M5's `AzureOpenAIRequiredFact` gates the Azure OpenAI test, and none runs in CI.

- **`SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard`** (AC-5) – needs a tenant, an app
  registration whose Teams app is installed in the target team with `ChannelMessage.Send.Group`,
  and a team and channel id. Marked `Category=RequiresTeams`.
- **`SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage`** (AC-6) – needs a bot token with
  `chat:write` and a channel the bot has joined. Marked `Category=RequiresSlack`.
- **`SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed`** (AC-7) – needs a webhook URL for a test
  channel. Marked `Category=RequiresDiscord`.
- **`SendAsync_AgainstARealCommunicationServicesResource_QueuesOneEmail`** (AC-8) – needs a
  Communication Services connection, a verified sender domain and one recipient. Asserts the send
  operation is accepted and an operation id comes back. Marked `Category=RequiresEmail`.
- **`EndToEnd_AnErrorEventWithNoChannelConfigured_WritesTheReportAndRecordsEveryChannelUnavailable`**
  (AC-2, AC-3) – extends the existing AppHost emulator test: produce one error event to hub `logs`
  with no channel configured, then assert the report file appears and the worker logged four
  `unavailable` deliveries. Marked `Category=RequiresDocker`.

## Architecture tests

- **`NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure`** (AC-13) – the Communication
  Services package and any HTTP client registration appear only in
  `src/DevOpsCopilot.Infrastructure`.
- **`Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk`** (AC-13) – extends the existing
  Application rule so a future channel cannot be written in the wrong layer.
- **`Application_WhenInspectedForChannelNames_MentionsNoChannelType`** (AC-12) – no type or member
  in `DevOpsCopilot.Application` names Teams, Slack, Discord, Graph or Communication Services.
- The existing layer rules keep passing unchanged.

## Evaluation

Not applicable. No LLM behaviour changes in this milestone: the diagnosis is already produced
before stage 7 begins, and no renderer calls a model. Diagnosis quality stays M8's subject.

## Manual checks

These need credentials and a human eye, and are run once before the pull request is merged:

- `aspire run` with no channel configured: one error event produces the report file and four
  `unavailable` deliveries in the log; nothing fails.
- Teams, with the RSC Teams app installed: the Adaptive Card renders correctly in the channel on
  the desktop and mobile clients, the text matches the report file, and no section is cut off
  mid-word.
- Slack: the Block Kit message renders correctly, long steps are readable and the truncation marker
  is visible.
- Discord: the embed renders with its colour and fields, and the message is attributed to the
  webhook's name.
- Email: the message arrives at every configured address, renders in Outlook and in a plain-text
  client, and the subject is useful in a notification preview.
- Across all four: no token, secret, webhook URL or connection string appears in the console output,
  in any payload or in any delivered message.
- Deliberate failure: revoke the Slack token and run again; Slack is `failed`, the other three
  still deliver and the worker keeps consuming.

---

## Results

<!-- Fill in after implementation. Include this section in the pull request. -->

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | | Pass / Fail | |
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

- **`dotnet build`:** 
- **`dotnet test`:** <passed> / <total>
- **Gated integration tests:** <which ran, which were skipped and why>
- **Manual checks:** 
- **Date:** YYYY-MM-DD
