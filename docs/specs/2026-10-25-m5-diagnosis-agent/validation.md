# Validation: Azure OpenAI diagnosis

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `Confidence_rejects_a_value_outside_the_three_levels`, `A_diagnosis_carries_the_contract_fields` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-2 | `The_report_has_the_seven_contract_sections_in_order`, `The_header_names_the_event_the_trace_and_the_hub`, `The_likely_cause_section_states_the_confidence` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-3 | `With_both_tools_unavailable_the_report_says_so_and_lists_nothing` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-4 | `Without_a_configured_model_the_deterministic_diagnosis_is_used`, `The_deterministic_diagnosis_records_the_model_as_unavailable` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-4 | `Without_a_model_the_pipeline_still_writes_a_report` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-5 | `A_well_formed_model_diagnosis_is_used_in_the_report` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Confidence_is_low_when_both_tools_were_unavailable` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-6 | `A_high_confidence_from_the_model_is_lowered_when_both_tools_were_unavailable` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-7 | `A_citation_to_a_kql_entry_that_does_not_exist_is_invalid`, `A_citation_to_a_row_that_does_not_exist_is_invalid`, `A_similar_incident_that_was_not_a_hit_is_invalid`, `A_diagnosis_citing_only_returned_evidence_is_valid` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-7 | `A_rejected_model_diagnosis_falls_back_with_an_open_question` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `A_throwing_model_falls_back_and_does_not_propagate`, `A_model_that_returns_nothing_falls_back`, `A_timed_out_model_falls_back` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `An_event_whose_model_fails_is_still_checkpointed` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-9 | `Impact_for_one_event_is_one_event_its_category_and_no_resources` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-10 | Existing architecture tests, extended with the Azure OpenAI package rule | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-11 | `AzureOpenAI_ReturnsABindableAndCitedDiagnosis` | Integration | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-2, AC-4 | `EndToEnd_AnErrorEvent_WritesASevenSectionReport` | Integration | Host | `tests/DevOpsCopilot.AppHost.Tests` |

## Unit tests (written first)

`tests/DevOpsCopilot.Domain.Tests` — the diagnosis, the renderer and the guard, all pure:

- **`Confidence_rejects_a_value_outside_the_three_levels`** (AC-1) – Arrange: the strings `low`, `medium`, `high` and `certain`. Act: create a `Confidence` from each. Assert: the first three succeed and are case-insensitive; `certain` throws.
- **`A_diagnosis_carries_the_contract_fields`** (AC-1) – Arrange: handoffs with one finding and one hit. Act: build the deterministic diagnosis. Assert: every field in [pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent) is populated or empty, never null.
- **`The_report_has_the_seven_contract_sections_in_order`** (AC-2) – Arrange: a diagnosis with every field filled. Act: render. Assert: the seven section headings appear exactly once each and their indexes in the document ascend in contract order.
- **`The_header_names_the_event_the_trace_and_the_hub`** (AC-2) – Arrange: a diagnosis for an event with a trace id. Act: render. Assert: the header contains the title, the status, the event id, the trace id and `logs`.
- **`The_likely_cause_section_states_the_confidence`** (AC-2) – Arrange: a diagnosis at `medium`. Act: render. Assert: the Likely cause section contains the cause and the word `medium`.
- **`With_both_tools_unavailable_the_report_says_so_and_lists_nothing`** (AC-3) – Arrange: handoffs with both statuses `unavailable` and empty rows and hits. Act: render. Assert: Evidence says KQL was unavailable, Similar incidents says search was unavailable, and neither section contains a row value or a hit title.
- **`Confidence_is_low_when_both_tools_were_unavailable`** (AC-6) – Arrange: both statuses `unavailable`. Act: apply the contract confidence rule to a `high` diagnosis. Assert: `low`.
- **`A_citation_to_a_kql_entry_that_does_not_exist_is_invalid`** (AC-7) – Arrange: a handoff with two KQL entries and a diagnosis citing entry 5. Act: validate. Assert: invalid, and the reason names the citation.
- **`A_citation_to_a_row_that_does_not_exist_is_invalid`** (AC-7) – as above for the row index.
- **`A_similar_incident_that_was_not_a_hit_is_invalid`** (AC-7) – Arrange: grounding with hits `h1`, `h2` and a diagnosis listing `h9`. Act: validate. Assert: invalid.
- **`A_diagnosis_citing_only_returned_evidence_is_valid`** (AC-7) – Arrange: citations inside both lists and similar incidents drawn from the hits. Act: validate. Assert: valid.
- **`Impact_for_one_event_is_one_event_its_category_and_no_resources`** (AC-9) – Arrange: a handoff for one event with category `Orders.Api`. Act: build the deterministic diagnosis. Assert: event count 1, services `["Orders.Api"]`, resources empty, window equal to the event's timestamp.
- **`The_deterministic_diagnosis_is_byte_identical_for_the_same_handoffs`** (NFR) – Arrange: the same handoffs twice. Act: render both. Assert: the two documents are equal.

`tests/DevOpsCopilot.Application.Tests` — the stage, with a fake `IDiagnosisModel`:

- **`Without_a_configured_model_the_deterministic_diagnosis_is_used`** (AC-4) – Arrange: a fake model reporting not configured. Act: run the diagnose stage. Assert: the diagnosis matches the deterministic one field by field (record equality compares its lists by reference, so the fields are compared instead) and the model was never called.
- **`The_deterministic_diagnosis_records_the_model_as_unavailable`** (AC-4) – Act: as above. Assert: an open question states the diagnosis model was unavailable.
- **`Without_a_model_the_pipeline_still_writes_a_report`** (AC-4) – Arrange: the full pipeline with fake tools, all unavailable. Act: run one error event. Assert: one report path is returned and the report has the seven sections.
- **`A_well_formed_model_diagnosis_is_used_in_the_report`** (AC-5) – Arrange: a fake model returning a valid, fully cited diagnosis. Act: run. Assert: the report's title, likely cause and suggested steps are the model's, and no open question claims the model was unavailable.
- **`A_high_confidence_from_the_model_is_lowered_when_both_tools_were_unavailable`** (AC-6) – Arrange: a model returning `high` with both statuses `unavailable`. Act: run. Assert: the resulting confidence is `low`.
- **`A_rejected_model_diagnosis_falls_back_with_an_open_question`** (AC-7) – Arrange: a model returning a diagnosis citing row 99. Act: run. Assert: the deterministic diagnosis is used, confidence `low`, and an open question says the model's diagnosis was rejected for citing evidence that was not returned.
- **`A_throwing_model_falls_back_and_does_not_propagate`** (AC-8) – Arrange: a model that throws. Act: run. Assert: no exception escapes, the deterministic diagnosis is used and an open question names the failure.
- **`A_model_that_returns_nothing_falls_back`** (AC-8) – Arrange: a model returning `null` (an unbindable response at the boundary). Act: run. Assert: as above.
- **`A_timed_out_model_falls_back`** (AC-8) – Arrange: a model that waits on the cancellation token, and a stage built with a zero timeout. Act: run. Assert: as above, and the open question names the timeout. (The timeout is a `TimeSpan` on the stage rather than a fake `TimeProvider`: the repo has no `FakeTimeProvider` package and a zero timeout is deterministic without one.)
- **`A_shutdown_while_the_model_is_running_still_stops_the_worker`** (AC-8) – Arrange: a model that waits on the cancellation token and an already-cancelled caller token. Act: run. Assert: an `OperationCanceledException` propagates. Added during implementation: a cancelled *caller* must not be swallowed as a model failure, which is the one case where falling back would be wrong.
- **`An_event_whose_model_fails_is_still_checkpointed`** (AC-8) – Arrange: `ProcessLogEvents` with a throwing model. Act: run one event. Assert: the checkpoint delegate ran once and the observer saw no error.

Existing Application and Domain tests keep passing unchanged, in particular the `Warning`-does-not-start-the-team test and the five-hit cap.

## Integration tests

- **`AzureOpenAI_ReturnsABindableAndCitedDiagnosis`** (AC-11) – Arrange: handoffs with two KQL entries, two rows and two hits, and an endpoint, deployment and credential from user secrets. Act: call the Azure OpenAI diagnosis model once. Assert: the response binds to the diagnosis shape and passes the citation guard. Marked `Category=RequiresAzureOpenAI` and skipped when the configuration is absent, in the same way the AppHost tests skip without Docker. Never run in CI.
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

| AC | Test or check | Result | Notes |
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

- **`dotnet build`:** 
- **`dotnet test`:** <passed> / <total>
- **Azure OpenAI integration test:** <ran / skipped>
- **End-to-end emulator test:** <ran / skipped>
- **Manual checks:** 
- **Date:** 
