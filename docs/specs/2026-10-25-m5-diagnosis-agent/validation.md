# Validation: Azure OpenAI diagnosis

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `Create_WithAValueOutsideTheThreeLevels_ThrowsArgumentException`, `Deterministic_WithOkHandoffs_CarriesTheContractFields` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-2 | `Render_WithACompleteDiagnosis_WritesTheSevenContractSectionsInOrder`, `Render_ForAnyDiagnosis_NamesTheEventTheTraceAndTheHubInTheHeader`, `Render_WithAMediumConfidenceDiagnosis_StatesTheConfidenceInTheLikelyCauseSection` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-3 | `Render_WhenBothToolsWereUnavailable_SaysSoAndListsNoEvidence` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-4 | `DiagnoseAsync_WithoutAConfiguredModel_UsesTheDeterministicDiagnosis`, `DiagnoseAsync_WithoutAConfiguredModel_RecordsTheModelAsUnavailableInTheOpenQuestions` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-4 | `RunAsync_WithoutAConfiguredModel_StillWritesAFullReport` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-5 | `RunAsync_WithAWellFormedModelDiagnosis_UsesItInTheReport` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `ApplyConfidenceRule_WhenBothToolsWereUnavailable_LowersTheConfidenceToLow` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-6 | `DiagnoseAsync_WhenBothToolsWereUnavailable_LowersAHighModelConfidenceToLow` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-7 | `Validate_WithACitationToAMissingKqlEntry_ReturnsInvalid`, `Validate_WithACitationToAMissingRow_ReturnsInvalid`, `Validate_WithASimilarIncidentThatWasNotAHit_ReturnsInvalid`, `Validate_WhenOnlyReturnedEvidenceIsCited_ReturnsValid` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-7 | `DiagnoseAsync_WhenTheModelDiagnosisIsRejected_FallsBackWithAnOpenQuestion` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `DiagnoseAsync_WhenTheModelThrows_FallsBackWithoutPropagatingTheException`, `DiagnoseAsync_WhenTheModelReturnsNothing_FallsBackToTheDeterministicDiagnosis`, `DiagnoseAsync_WhenTheModelTimesOut_FallsBackToTheDeterministicDiagnosis` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `RunAsync_WhenTheModelFails_StillCheckpointsTheEvent` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-9 | `Deterministic_ForASingleEvent_SetsImpactToOneEventItsServiceAndNoResources` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-10 | Existing architecture tests, extended with the Azure OpenAI package rule | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-11 | `ProposeAsync_AgainstARealAzureOpenAIDeployment_ReturnsABindableAndCitedDiagnosis` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-2, AC-4 | `EndToEnd_AnErrorEvent_WritesASevenSectionReport` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |

## Unit tests (written first)

`tests/DevOpsCopilot.Domain.Tests` — the diagnosis, the renderer and the guard, all pure:

- **`Create_WithAValueOutsideTheThreeLevels_ThrowsArgumentException`** (AC-1) – Arrange: the strings `low`, `medium`, `high` and `certain`. Act: create a `Confidence` from each. Assert: the first three succeed and are case-insensitive; `certain` throws.
- **`Deterministic_WithOkHandoffs_CarriesTheContractFields`** (AC-1) – Arrange: handoffs with one finding and one hit. Act: build the deterministic diagnosis. Assert: every field in [pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent) is populated or empty, never null.
- **`Render_WithACompleteDiagnosis_WritesTheSevenContractSectionsInOrder`** (AC-2) – Arrange: a diagnosis with every field filled. Act: render. Assert: the seven section headings appear exactly once each and their indexes in the document ascend in contract order.
- **`Render_ForAnyDiagnosis_NamesTheEventTheTraceAndTheHubInTheHeader`** (AC-2) – Arrange: a diagnosis for an event with a trace id. Act: render. Assert: the header contains the title, the status, the event id, the trace id and `logs`.
- **`Render_WithAMediumConfidenceDiagnosis_StatesTheConfidenceInTheLikelyCauseSection`** (AC-2) – Arrange: a diagnosis at `medium`. Act: render. Assert: the Likely cause section contains the cause and the word `medium`.
- **`Render_WhenBothToolsWereUnavailable_SaysSoAndListsNoEvidence`** (AC-3) – Arrange: handoffs with both statuses `unavailable` and empty rows and hits. Act: render. Assert: Evidence says KQL was unavailable, Similar incidents says search was unavailable, and neither section contains a row value or a hit title.
- **`ApplyConfidenceRule_WhenBothToolsWereUnavailable_LowersTheConfidenceToLow`** (AC-6) – Arrange: both statuses `unavailable`. Act: apply the contract confidence rule to a `high` diagnosis. Assert: `low`.
- **`Validate_WithACitationToAMissingKqlEntry_ReturnsInvalid`** (AC-7) – Arrange: a handoff with two KQL entries and a diagnosis citing entry 5. Act: validate. Assert: invalid, and the reason names the citation.
- **`Validate_WithACitationToAMissingRow_ReturnsInvalid`** (AC-7) – as above for the row index.
- **`Validate_WithASimilarIncidentThatWasNotAHit_ReturnsInvalid`** (AC-7) – Arrange: grounding with hits `h1`, `h2` and a diagnosis listing `h9`. Act: validate. Assert: invalid.
- **`Validate_WhenOnlyReturnedEvidenceIsCited_ReturnsValid`** (AC-7) – Arrange: citations inside both lists and similar incidents drawn from the hits. Act: validate. Assert: valid.
- **`Deterministic_ForASingleEvent_SetsImpactToOneEventItsServiceAndNoResources`** (AC-9) – Arrange: a handoff for one event with category `Orders.Api`. Act: build the deterministic diagnosis. Assert: event count 1, services `["Orders.Api"]`, resources empty, window equal to the event's timestamp.
- **`Deterministic_WithTheSameHandoffs_RendersByteIdenticalReports`** (NFR) – Arrange: the same handoffs twice. Act: render both. Assert: the two documents are equal.

`tests/DevOpsCopilot.Application.Tests` — the stage, with a fake `IDiagnosisModel`:

- **`DiagnoseAsync_WithoutAConfiguredModel_UsesTheDeterministicDiagnosis`** (AC-4) – Arrange: a fake model reporting not configured. Act: run the diagnose stage. Assert: the diagnosis matches the deterministic one field by field (record equality compares its lists by reference, so the fields are compared instead) and the model was never called.
- **`DiagnoseAsync_WithoutAConfiguredModel_RecordsTheModelAsUnavailableInTheOpenQuestions`** (AC-4) – Act: as above. Assert: an open question states the diagnosis model was unavailable.
- **`RunAsync_WithoutAConfiguredModel_StillWritesAFullReport`** (AC-4) – Arrange: the full pipeline with fake tools, all unavailable. Act: run one error event. Assert: one report path is returned and the report has the seven sections.
- **`RunAsync_WithAWellFormedModelDiagnosis_UsesItInTheReport`** (AC-5) – Arrange: a fake model returning a valid, fully cited diagnosis. Act: run. Assert: the report's title, likely cause and suggested steps are the model's, and no open question claims the model was unavailable.
- **`DiagnoseAsync_WhenBothToolsWereUnavailable_LowersAHighModelConfidenceToLow`** (AC-6) – Arrange: a model returning `high` with both statuses `unavailable`. Act: run. Assert: the resulting confidence is `low`.
- **`DiagnoseAsync_WhenTheModelDiagnosisIsRejected_FallsBackWithAnOpenQuestion`** (AC-7) – Arrange: a model returning a diagnosis citing row 99. Act: run. Assert: the deterministic diagnosis is used, confidence `low`, and an open question says the model's diagnosis was rejected for citing evidence that was not returned.
- **`DiagnoseAsync_WhenTheModelThrows_FallsBackWithoutPropagatingTheException`** (AC-8) – Arrange: a model that throws. Act: run. Assert: no exception escapes, the deterministic diagnosis is used and an open question names the failure.
- **`DiagnoseAsync_WhenTheModelReturnsNothing_FallsBackToTheDeterministicDiagnosis`** (AC-8) – Arrange: a model returning `null` (an unbindable response at the boundary). Act: run. Assert: as above.
- **`DiagnoseAsync_WhenTheModelTimesOut_FallsBackToTheDeterministicDiagnosis`** (AC-8) – Arrange: a model that waits on the cancellation token, and a stage built with a zero timeout. Act: run. Assert: as above, and the open question names the timeout. (The timeout is a `TimeSpan` on the stage rather than a fake `TimeProvider`: the repo has no `FakeTimeProvider` package and a zero timeout is deterministic without one.)
- **`DiagnoseAsync_WhenShutdownIsRequestedWhileTheModelRuns_PropagatesTheCancellation`** (AC-8) – Arrange: a model that waits on the cancellation token and an already-cancelled caller token. Act: run. Assert: an `OperationCanceledException` propagates. Added during implementation: a cancelled *caller* must not be swallowed as a model failure, which is the one case where falling back would be wrong.
- **`RunAsync_WhenTheModelFails_StillCheckpointsTheEvent`** (AC-8) – Arrange: `ProcessLogEvents` with a throwing model. Act: run one event. Assert: the checkpoint delegate ran once and the observer saw no error.

Existing Application and Domain tests keep passing unchanged, in particular the `Warning`-does-not-start-the-team test and the five-hit cap.

## Integration tests

- **`ProposeAsync_AgainstARealAzureOpenAIDeployment_ReturnsABindableAndCitedDiagnosis`** (AC-11) – Arrange: handoffs with two KQL entries, two rows and two hits, and an endpoint, deployment and credential from user secrets. Act: call the Azure OpenAI diagnosis model once. Assert: the response binds to the diagnosis shape and passes the citation guard. Marked `Category=RequiresAzureOpenAI` and skipped by `AzureOpenAIRequiredFactAttribute` when the configuration is absent, in the same way the AppHost tests skip without Docker. Never run in CI.

### Infrastructure unit tests for the model (no network)

Driven by `FakeChatClient`, which answers with whatever text the test gives it, so the whole mapping, prompt and failure surface is covered without Azure:

- **`IsConfigured_WithoutAChatClient_ReturnsFalse`**, **`IsConfigured_WithAChatClient_ReturnsTrue`**, **`ProposeAsync_WhenTheModelIsUnconfigured_ReturnsNullWithoutCallingTheClient`** (AC-4).
- **`ProposeAsync_WithAWellFormedAnswer_MapsItToTheDomainDiagnosis`** (AC-5) – every field of the contract shape is mapped.
- **`ProposeAsync_WhenTheAnswerCarriesAnIncidentId_UsesTheHandoffIdInstead`** (AC-5) – the model cannot rename the incident it was asked about.
- **`ProposeAsync_WithAnAnswerThatIsNotJson_ReturnsNull`**, **`ProposeAsync_WithAConfidenceOutsideTheContract_ReturnsNull`**, **`ProposeAsync_WithAnAnswerWithoutALikelyCause_ReturnsNull`** (AC-8) – an answer that cannot be trusted becomes `null`, which the stage turns into the deterministic fallback.
- **`ProposeAsync_WithMissingLists_MapsThemToEmptyListsNeverNull`** (AC-1).
- **`ProposeAsync_WithOkHandoffs_PromptsWithIndexedEvidenceAndForbidsInfrastructureCode`** (AC-7, [mission principle 1](../mission.md#principles)) – the prompt carries the KQL, rows and hit ids by index and forbids Bicep, ARM and Terraform.
- **`ProposeAsync_WhenAToolWasUnavailable_SaysSoInThePromptRatherThanInventingEvidence`** (AC-3).
- **`ProposeAsync_ForOneEvent_AsksTheModelExactlyOnce`** (NFR, cost).
- **`ProposeAsync_WhenTheCallIsCancelled_PropagatesTheCancellation`**, **`ProposeAsync_WhenTheTransportFails_PropagatesTheException`** (AC-8, decision D2) – Infrastructure never swallows a failure.

### Configuration and wiring tests

- **`TryCreate_WithoutAnEndpointOrADeployment_ReturnsNull`** (theory over blank endpoint and deployment), **`TryCreate_WithAnEndpointThatIsNotAUrl_ReturnsNullRatherThanThrowing`**, **`TryCreate_WithAnEndpointADeploymentAndAKey_BuildsAClient`**, **`TryCreate_WithoutAKey_StillBuildsAClientForManagedIdentity`** (AC-4, AC-11).
- **`TryCreate_WithTheUnconfiguredSettings_ReturnsNullAndTheSettingsCarryNoDefaults`** – nothing is hardcoded; `Unconfigured` is empty throughout.
- **`ToString_WithAnApiKey_DoesNotPrintTheKey`** (NFR, secrets) – `ToString` redacts the key, so it cannot reach a log line.
- **`Timeout_WithANonPositiveConfiguredValue_FallsBackToTheDefault`**, **`Timeout_WithAPositiveConfiguredValue_ReturnsTheConfiguredValue`** (AC-8).
- **`AddInfrastructure_WithoutAzureOpenAI_RegistersAnUnconfiguredModel`**, **`AddInfrastructure_WithAzureOpenAI_RegistersAConfiguredModel`**, **`AddInfrastructure_WithAConfiguredTimeout_RegistersTheDiagnoseStageWithThatTimeout`**, **`AddInfrastructure_WhenTheAzureOpenAISettingsAreOmitted_KeepsTheDeterministicPath`** (AC-4, AC-10) – what the host actually resolves from `AddInfrastructure`.
- **`EndToEnd_AnErrorEvent_WritesASevenSectionReport`** (AC-2, AC-4) – Arrange: the AppHost with the emulators and no Azure OpenAI configuration. Act: produce one error event to hub `logs`. Assert: a report file appears under the configured drafts directory with the seven section headings, confidence `low`, and an open question naming the unavailable model. Extends the existing emulator end-to-end test; marked `Category=RequiresDocker`.

## Architecture tests

- The existing layer rules must keep passing with the new code: Domain references no package, Application references no Azure SDK or Agent Framework package.
- One new rule: the Azure OpenAI package is referenced only by `src/DevOpsCopilot.Infrastructure`. The Host wires it through the Infrastructure registration and has no direct reference.

## Evaluation

Not applicable. Diagnosis quality is scored by the staged-incident set in M8. This milestone proves correctness and safety only: the shape, the sections, the confidence rule, the guard and the fallbacks.

## Manual checks

- `aspire run` with no Azure OpenAI configuration: an error event produces a report whose Likely cause section says the cause was not determined and whose Open questions name the unavailable model.
- `aspire run` with Azure OpenAI configured in user secrets: the same event produces a report with a named likely cause, a confidence, and suggested steps written for an engineer, and no Bicep, ARM, Terraform or script content in any section.
- No secret value appears in the console output or in the written report.

---

## Results

Completed on the `features/2026-11-04-m4-report-contract` branch, which finished M5's task
group 6. Two tests were renamed after implementation by later work on that branch: M4 renamed the
agent (no M5 test names changed), and the IaC removal renamed
`ProposeAsync_WithOkHandoffs_PromptsWithIndexedEvidenceAndForbidsInfrastructureCode` to
`…AndForbidsResourceChanges`, which now asserts the prompt's generic no-resource-changes rule
instead of naming IaC technologies.

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | `Create_WithAValueOutsideTheThreeLevels_ThrowsArgumentException`, `Deterministic_WithOkHandoffs_CarriesTheContractFields`, `ProposeAsync_WithMissingLists_MapsThemToEmptyListsNeverNull` | Pass | |
| AC-2 | `Render_WithACompleteDiagnosis_WritesTheSevenContractSectionsInOrder`, `Render_ForAnyDiagnosis_NamesTheEventTheTraceAndTheHubInTheHeader`, `Render_WithAMediumConfidenceDiagnosis_StatesTheConfidenceInTheLikelyCauseSection` | Pass | |
| AC-3 | `Render_WhenBothToolsWereUnavailable_SaysSoAndListsNoEvidence`, `ProposeAsync_WhenAToolWasUnavailable_SaysSoInThePromptRatherThanInventingEvidence` | Pass | |
| AC-4 | The three `DiagnoseAsync`/`RunAsync` without-a-configured-model tests and the `TryCreate`/`AddInfrastructure` configuration tests | Pass | |
| AC-5 | `RunAsync_WithAWellFormedModelDiagnosis_UsesItInTheReport`, `ProposeAsync_WithAWellFormedAnswer_MapsItToTheDomainDiagnosis` | Pass | |
| AC-6 | `ApplyConfidenceRule_WhenBothToolsWereUnavailable_LowersTheConfidenceToLow`, `DiagnoseAsync_WhenBothToolsWereUnavailable_LowersAHighModelConfidenceToLow` | Pass | |
| AC-7 | The four `Validate_…` guard tests, `DiagnoseAsync_WhenTheModelDiagnosisIsRejected_FallsBackWithAnOpenQuestion` | Pass | |
| AC-8 | The throw/timeout/nothing/cancellation fallback tests, `RunAsync_WhenTheModelFails_StillCheckpointsTheEvent` | Pass | |
| AC-9 | `Deterministic_ForASingleEvent_SetsImpactToOneEventItsServiceAndNoResources` | Pass | |
| AC-10 | Architecture tests, including `OpenAIPackages_AcrossTheSolution_LiveOnlyInInfrastructure` | Pass | All 9 pass. |
| AC-11 | `ProposeAsync_AgainstARealAzureOpenAIDeployment_ReturnsABindableAndCitedDiagnosis` | Skipped | No Azure OpenAI configuration in the verification environment; gated by `AzureOpenAIRequiredFact`, as designed. Run it once against a real deployment before relying on the configured path. |

- **`dotnet build`:** clean, 0 errors; only pre-existing NuGet advisory warnings in the leftover sample projects.
- **`dotnet test`:** 101 / 105 passed, 0 failed, 4 skipped (all environment-gated).
- **Azure OpenAI integration test:** skipped — no deployment configured in the verification environment.
- **End-to-end emulator test:** `EndToEnd_AnErrorEvent_WritesASevenSectionReport` added in `tests/DevOpsCopilot.AppHost.Tests`; skipped in the verification environment for want of Docker. Run it with Docker available to see it pass.
- **Manual checks:** not run — the verification environment has no Docker and no Azure OpenAI deployment. The unconfigured path is covered by the unit tests and the new end-to-end test; the configured path's manual check remains open until a deployment is available.
- **Date:** 2026-11-04
