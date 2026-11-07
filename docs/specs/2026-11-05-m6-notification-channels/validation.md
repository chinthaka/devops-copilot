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
| AC-5 | `Build_WithACompleteNotification_ProducesAMessageWithOneAdaptiveCardAttachment`, `SendAsync_WithAConfiguredWebhook_PostsTheCardToTheTriggerUrl`, `SendAsync_WhenTheWorkflowAccepts_RecordsItSentWithoutAMessageId`, `SendAsync_WhenTheWorkflowRejects_RecordsItFailedWithTheStatusAndNotTheUrl` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-5 | `SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `Build_WithACompleteNotification_ProducesBlocksWhoseTextComesFromTheNotification`, `SendAsync_WithAConfiguredBotToken_CallsChatPostMessageWithTheBlocks`, `SendAsync_WhenSlackAnswersOkFalse_RecordsItFailedWithSlacksErrorCode`, `SendAsync_WhenSlackAnswersOk_ReportsTheTimestampAsTheMessageId` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `SendAsync_AgainstARealSlackChannel_PostsOneBlockKitMessage` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `Build_WithACompleteNotification_ProducesOneEmbedWithTheReportFields`, `SendAsync_WithAConfiguredWebhook_PostsWithWaitTrueAndReportsTheMessageId` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `SendAsync_AgainstARealDiscordWebhook_PostsOneEmbed` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-8 | `Build_WithACompleteNotification_ProducesAnHtmlBodyAndAPlainTextAlternative`, `Build_WithANotification_SubjectNamesTheIncidentAndTheConfidence`, `SendAsync_WithThreeConfiguredRecipients_SendsOneMessageAddressedToAllOfThem`, `SendAsync_WithoutASenderOrRecipients_ReportsItselfUnconfigured` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-8 | `SendAsync_AgainstARealCommunicationServicesResource_QueuesOneEmail` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-9 | `Build_WithAnOversizedDiagnosis_KeepsEveryBlockWithinSlacksLimits`, `Build_WithAnOversizedDiagnosis_KeepsTheEmbedWithinDiscordsLimits`, `Build_WithAnOversizedDiagnosis_TruncatesTheCardVisibly`, `Build_WithAnOversizedDiagnosis_StillNamesTheIncidentAndTheReportLocation` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-10 | `ToString_WithASecret_DoesNotPrintIt` (theory over the four settings records), `NotifyAsync_WhenAChannelFails_LogsTheReasonWithoutTheCredential`, `Build_ForEveryChannel_PutsNoCredentialInThePayload` | Unit | Infrastructure, Application | `tests/DevOpsCopilot.Infrastructure.Tests`, `tests/DevOpsCopilot.Application.Tests` |
| AC-11 | `RunAsync_WithNotificationAdded_WritesTheSameReportBytesAsBefore`, `RunAsync_WhenAChannelIsConfigured_WritesTheReportBeforeCallingAnyChannel`, `Render_WithTheDefaultTemplate_MatchesTheHardCodedLayoutByteForByte` | Unit | Domain, Application | `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Application.Tests` |
| AC-12 | `NotifyAsync_WithAnAdditionalNotifierRegistered_CallsItWithoutAnyChangeToTheStage`, `Application_WhenInspectedForChannelNames_MentionsNoChannelType` | Unit, Architecture | Application | `tests/DevOpsCopilot.Application.Tests`, `tests/DevOpsCopilot.Architecture.Tests` |
| AC-13 | `NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure`, `Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk`, existing layer rules | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-14 | `Render_WithACustomTemplate_FollowsItsLayout`, `RunAsync_WithACustomReportTemplate_RendersTheReportThroughIt`, `Build_WithCustomEmailTemplates_FillsTheirPlaceholders`, `GetAsync_WithAConfiguredPath_RereadsTheFileOnEveryCall`, manual template-edit check | Unit, Manual | Domain, Application, Infrastructure | `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Application.Tests`, `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-15 | `Render_WithAnUnknownPlaceholder_LeavesTheTokenVisible`, `RunAsync_WhenTheTemplateSourceReturnsNothing_RendersTheDefaultReport`, `GetAsync_WithAMissingFile_WarnsOnceAndReturnsNull` | Unit | Domain, Application, Infrastructure | `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Application.Tests`, `tests/DevOpsCopilot.Infrastructure.Tests` |
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

The template renderer (D12, D13; AC-11, AC-14, AC-15):

- **`Render_WithEveryPlaceholderNamed_ReplacesEachTokenWithItsValue`** – scalar tokens, filled from
  the values map.
- **`Render_WithAListValue_ExpandsItToOneBulletLinePerEntry`** – and to the section's
  "unavailable" sentence when the list is empty because the tool returned nothing.
- **`Render_WithAnUnknownPlaceholder_LeavesTheTokenVisible`** – a typo in a custom template is
  seen in the output, never silently swallowed.
- **`Render_WithTheDefaultTemplate_MatchesTheHardCodedLayoutByteForByte`** – the embedded default
  report template reproduces M5's report exactly, which is what keeps AC-11 and the M5 end-to-end
  test honest.
- **`Render_WithACustomTemplate_FollowsItsLayout`** – a short template that reorders and relabels
  sections; the output follows it and carries only facts from the values map.

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

- **`RunAsync_WithACustomReportTemplate_RendersTheReportThroughIt`** (AC-14) – Arrange: a fake
  `ITemplateSource` answering the `report` name with a short custom template. Act: run one error
  event. Assert: the written draft follows the custom layout.
- **`RunAsync_WhenTheTemplateSourceReturnsNothing_RendersTheDefaultReport`** (AC-15) – Arrange: a
  fake source answering null. Act: run. Assert: the draft is byte-identical to the no-source
  default.

Existing Application tests must keep passing unchanged, in particular
`RunAsync_WithoutAConfiguredModel_StillWritesAFullReport` and the `Warning`-does-not-start-the-team
test.

### `tests/DevOpsCopilot.Infrastructure.Tests` — the four channels, no network

Driven by `StubHttpMessageHandler`, which answers with whatever status and body the test gives it
and records the request it received. The payload builders are pure and tested directly.

**Teams** (AC-5, AC-9, AC-10)

- **`Build_WithACompleteNotification_ProducesAMessageWithOneAdaptiveCardAttachment`** – Assert: a
  `message` with exactly one attachment whose `contentType` is
  `application/vnd.microsoft.card.adaptive`, `contentUrl` null, and an Adaptive Card 1.5 in
  `content`.
- **`Build_WithACompleteNotification_PutsTheNotificationsTextInTheCard`** – Assert: title, likely
  cause, confidence, impact, evidence, similar incidents, suggested steps and open questions all
  appear, and the incident id and report location are present.
- **`SendAsync_WithAConfiguredWebhook_PostsTheCardToTheTriggerUrl`** – Assert: one `POST` to the
  configured URL with the built body as JSON.
- **`SendAsync_WhenTheWorkflowAccepts_RecordsItSentWithoutAMessageId`** – Arrange: `202 Accepted`,
  empty body. Assert: `sent`, message id and url null (D3).
- **`SendAsync_WhenTheWorkflowRejects_RecordsItFailedWithTheStatusAndNotTheUrl`** – Arrange: a
  `401` or `404`. Assert: `failed`, the reason names the status, and the trigger URL appears
  nowhere in the reason.
- **`SendAsync_WithoutAWebhookUrl_ReportsItselfUnconfigured`**.
- **`Build_WithAnOversizedDiagnosis_TruncatesTheCardVisibly`** (AC-9) – the serialised body stays
  within 28 KB.

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

**Discord** (AC-7, AC-9, AC-10)

- **`Build_WithACompleteNotification_ProducesOneEmbedWithTheReportFields`** – one embed, the
  title, the cause in the description, and the six fields in order.
- **`Build_WithEachConfidence_ChoosesItsColour`** – theory over low, medium and high.
- **`Build_WithMentionsInTheDiagnosis_AllowsNoMentions`** – `allowed_mentions.parse` is empty, so
  an `@everyone` in log text pings nobody.
- **`Build_WhenAToolWasUnavailable_SaysSoInsteadOfListingEvidence`**.
- **`Build_WithAnOversizedDiagnosis_KeepsTheEmbedWithinDiscordsLimits`** (AC-9) – 256 characters of
  title, 4 096 of description, 25 fields of 1 024, 6 000 across the embed, at most 10 embeds, a
  visible marker, and the incident id and report location kept.
- **`Build_WithTheSameNotification_ProducesTheSameBody`** (NFR, determinism).
- **`SendAsync_WithAConfiguredWebhook_PostsWithWaitTrueAndReportsTheMessageId`** – Assert: the
  query string contains `wait=true` and the delivery carries the returned message id.
- **`SendAsync_WithAWebhookUrlThatHasAQuery_KeepsItAndAddsWaitTrue`** – a `thread_id` survives.
- **`SendAsync_WhenDiscordRateLimits_RetriesOnceAfterRetryAfterThenSucceeds`** (AC-4, D8) – Arrange:
  a `429` carrying `Retry-After`, then a `200`. Assert: exactly two requests, one wait of the
  header's delay, and a `sent` delivery.
- **`SendAsync_WhenDiscordRateLimitsTwice_RecordsItFailedWithoutRetryingAgain`** (D8) – Assert:
  exactly two requests and a `failed` delivery. The retry budget is one.
- **`SendAsync_WhenDiscordRateLimitsWithoutRetryAfter_RecordsItFailedWithoutRetrying`** (D8) – only
  the header earns a retry.
- **`SendAsync_WhenRetryAfterExceedsTheCap_RecordsItFailedWithoutWaiting`** (D8) – a delay that
  could never finish within the channel's timeout is not waited for.
- **`SendAsync_WhenDiscordRejects_RecordsItFailedWithTheStatusAndNotTheUrl`** (AC-10) – theory over
  `400`, `401` and `404`.
- **`SendAsync_WhenTheRequestThrows_RecordsItFailedWithTheTypeAndNotTheUrl`** (AC-10).
- **`SendAsync_WithoutAWebhookUrl_ReportsItselfUnconfigured`**.
- **`SendAsync_WhenTheCallerIsCancelled_PropagatesTheCancellation`** – shutdown is not a failure.

**Email** (AC-8, AC-10, AC-14)

- **`Build_WithACompleteNotification_ProducesAnHtmlBodyAndAPlainTextAlternative`** – with the
  embedded default templates. Assert: both bodies carry the same facts, the plain text has no
  markup and no placeholder is left unfilled.
- **`Build_WithANotification_SubjectNamesTheIncidentAndTheConfidence`** – the default subject
  template.
- **`Build_WithCustomEmailTemplates_FillsTheirPlaceholders`** (AC-14) – custom subject, HTML and
  text templates: every token is filled from the notification and the custom wording is kept.
- **`Build_WithMarkupInTheDiagnosis_EncodesItInTheHtmlBody`** – log text is encoded in HTML and
  left as written in plain text.
- **`Build_WithALineBreakInTheTitle_KeepsTheSubjectOnOneLine`** – no value can add a mail header.
- **`Build_WhenAToolWasUnavailable_SaysSoInsteadOfListingEvidence`**.
- **`Build_WithTheSameNotification_ProducesTheSameContent`** (NFR, determinism).
- **`SendAsync_WithThreeConfiguredRecipients_SendsOneMessageAddressedToAllOfThem`** – Arrange: a
  fake email transport behind the thin interface the notifier depends on. Assert: one send, three
  `To` addresses, the sender from configuration, the default-template content, and the operation
  id as the message id.
- **`SendAsync_WithBlankAndDuplicateRecipients_AddressesEachOnce`**.
- **`SendAsync_WhenTheTransportFails_RecordsItFailedWithTheErrorCode`** (AC-10) – a
  `RequestFailedException`: the reason names status and error code and not the connection string.
- **`SendAsync_WhenTheTransportThrowsSomethingElse_RecordsItFailedWithTheType`** (AC-10).
- **`SendAsync_WithoutASenderOrRecipients_ReportsItselfUnconfigured`** – theory over a blank
  connection and endpoint, a blank sender, an empty and an all-blank recipient list.
- **`IsConfigured_WithAnEndpointAndNoConnectionString_IsTrueForManagedIdentity`**.
- **`SendAsync_WithCustomTemplatesFromTheSource_RendersThroughThem`** (AC-14) – the notifier asks
  the source for all three names and falls back per template.
- **`SendAsync_WhenTheTemplateSourceThrows_StillSendsWithTheDefaults`** (AC-15).
- **`SendAsync_WhenTheCallerIsCancelled_PropagatesTheCancellation`**.

**Templates — `FileTemplateSource`** (AC-14, AC-15, D13)

- **`GetAsync_WithAConfiguredPath_RereadsTheFileOnEveryCall`** – Arrange: a template file on disk.
  Act: read, edit the file, read again. Assert: the second read returns the edit — the
  no-deployment guarantee.
- **`GetAsync_WithNoConfiguredPath_ReturnsNull`** – unset means the embedded default, silently.
- **`GetAsync_WithAMissingFile_WarnsOnceAndReturnsNull`** – the warning names the path, nothing
  throws, and the caller falls back to the default.
- **`GetAsync_WithAnUnknownName_ReturnsNull`**.
- **`GetAsync_WithEachEmailName_ReadsItsOwnFile`** – the three email names map to their own paths.

**Settings and wiring** (AC-2, AC-10, AC-13)

- **`ToString_WithASecret_DoesNotPrintIt`** – theory over `TeamsSettings`, `SlackSettings`,
  `DiscordSettings` and `EmailSettings`: each prints `(set)` or `(none)` in place of the Teams
  webhook URL, bot token, both webhook URLs and connection string.
- **`AddInfrastructure_WithEachChannelConfigured_RegistersThatNotifier`** – theory over the four
  channels: the registered `INotifier` set contains that channel and it reports itself configured.
- **`AddInfrastructure_WithNoChannelConfigured_RegistersFourUnconfiguredNotifiers`** – the worker
  always resolves four notifiers, in the contract order Teams, Slack, Discord, email;
  configuration decides only whether they send.
- **`AddInfrastructure_WithTemplatePaths_RegistersTheFileTemplateSource`**.
- **`Build_ForEveryChannel_PutsNoCredentialInThePayload`** (AC-10) – theory over the four builders
  with credential-bearing settings: no payload contains the credential.

## Integration tests

Each is gated on its own configuration being present in user secrets or the environment, in the
way M5's `AzureOpenAIRequiredFact` gates the Azure OpenAI test, and none runs in CI.

- **`SendAsync_AgainstARealTeamsChannel_PostsOneAdaptiveCard`** (AC-5) – needs the trigger URL of
  a Workflows flow posting to a test channel; asserts `202` and a `sent` delivery. Marked
  `Category=RequiresTeams`.
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

- **`NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure`** (AC-13) – the
  Communication Services package and any HTTP client registration appear
  only in `src/DevOpsCopilot.Infrastructure`.
- **`Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk`** (AC-13) – extends the existing
  Application rule so a future channel cannot be written in the wrong layer.
- **`Application_WhenInspectedForChannelNames_MentionsNoChannelType`** (AC-12) – no type or member
  in `DevOpsCopilot.Application` names Teams, Workflows, Slack, Discord or Communication Services.
- The existing layer rules keep passing unchanged.

## Evaluation

Not applicable. No LLM behaviour changes in this milestone: the diagnosis is already produced
before stage 7 begins, and no renderer calls a model. Diagnosis quality stays M8's subject.

## Manual checks

These need credentials and a human eye, and are run once before the pull request is merged:

- `aspire run` with no channel configured: one error event produces the report file and four
  `unavailable` deliveries in the log; nothing fails.
- Teams, through the Workflows flow: the card actually appears (a `202` alone does not prove it), renders correctly in the channel on
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
- Templates: with `aspire run` live and custom report and email templates configured, edit both
  template files and produce another error event; the next report and email follow the edit with
  no restart (AC-14). Then point a template path at a missing file: the embedded defaults are used
  and one warning names the path (AC-15).
- Deliberate failure: revoke the Slack token and run again; Slack is `failed`, the other three
  still deliver and the worker keeps consuming.

---

## Results

Completed 2026-11-07.

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | The three `From_…` Domain tests plus determinism | Pass | |
| AC-2 | `NotifyAsync_WithFourConfiguredChannels_CallsEachOnceInTheContractOrder`, `RunAsync_WithAConfiguredChannel_NotifiesItAfterWritingTheReport`, registration-order test | Pass | |
| AC-3 | `NotifyAsync_WithNoConfiguredChannel_RecordsEveryChannelUnavailableWithoutCallingIt`, `RunAsync_WithNoConfiguredChannel_StillWritesTheReportAndReturnsNormally` | Pass | End-to-end variant written; see the gated row below. |
| AC-4 | The throw, timeout, shutdown and checkpoint tests; the Discord rate-limit tests | Pass | |
| AC-5 | The seven Teams unit tests | Pass | Workflows webhook per D3; `sent` carries no message id. |
| AC-6 | The Slack unit tests, including `SendAsync_WhenSlackAnswersOkFalse_RecordsItFailedWithSlacksErrorCode` | Pass | |
| AC-7 | The Discord unit tests | Pass | |
| AC-8 | The email unit tests against the fake transport | Pass | |
| AC-9 | The oversized-diagnosis tests for Teams, Slack and Discord | Pass | |
| AC-10 | `ToString_WithASecret_DoesNotPrintIt` (theory over the four settings records), the no-credential-in-reason tests, `NotifyAsync_WhenAChannelFails_LogsTheReasonWithoutTheCredential` | Pass | |
| AC-11 | `RunAsync_WithNotificationAdded_WritesTheSameReportBytesAsBefore`, `Render_WithTheDefaultTemplate_MatchesTheHardCodedLayoutByteForByte`, write-before-notify test | Pass | |
| AC-12 | `NotifyAsync_WithAnAdditionalNotifierRegistered_CallsItWithoutAnyChangeToTheStage`, `Application_WhenInspectedForChannelNames_MentionsNoChannelType` | Pass | |
| AC-13 | `NotificationPackages_AcrossTheSolution_LiveOnlyInInfrastructure`, `Application_WhenInspected_ReferencesNoHttpClientOrAzureSdk`, existing layer rules | Pass | |
| AC-14 | Template renderer, pipeline, email and `FileTemplateSource` re-read tests | Pass | Live `aspire run` template-edit check not yet run; see manual checks. |
| AC-15 | Unknown-placeholder, missing-file and throwing-source tests | Pass | |

- **`dotnet build`:** clean, 0 warnings, 0 errors.
- **`dotnet test`:** 219 passed / 228 total; 9 skipped, 0 failed (2026-11-07).
- **Gated integration tests:** none has run yet, for want of credentials and environment, not of
  code. `RequiresDocker` (4, including
  `EndToEnd_AnErrorEventWithNoChannelConfigured_WritesTheReportAndRecordsEveryChannelUnavailable`):
  Docker is running on this machine but the build user is not in the `docker` group, so the tests
  skip; run them once access is granted. `RequiresTeams`, `RequiresSlack`, `RequiresDiscord`,
  `RequiresEmail`, `RequiresAzureOpenAI`: no user secrets are set on
  `tests/DevOpsCopilot.Infrastructure.Tests`. Discord is expected first: it needs only a webhook
  URL from the target channel. Email has no emulator — Azure Communication Services Email is a
  REST service with no local stand-in (the Azure Service Bus emulator is a message broker and
  cannot receive it) — so its live run needs a real resource with a verified sender domain,
  provisioned alongside M7.3.
- **Manual checks:** not yet run; they need the same credentials and a human eye on the rendered
  messages, and are planned alongside M7.3 provisioning. The no-channel `aspire run` check is
  covered mechanically by the end-to-end test above once Docker access exists.
- **Date:** 2026-11-07
