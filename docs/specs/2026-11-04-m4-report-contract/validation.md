# Validation: Report matches the contract

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-1 | Solution-wide search for `RemediationAgent` returns nothing | Manual | — | — |
| AC-2 | `WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-3 | `WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-4 | `DiagnosisAgent_WithAChatClient_ForbidsResourceChangesInItsInstructions` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-5 | The solution compiles with `IncidentDraft` and `IncidentDraftTests` deleted; `WriteAsync_ForAReport_NamesTheFileFromTheEventDayAndId` renders through `IncidentReport` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `ProposeAsync_WithOkHandoffs_PromptsWithIndexedEvidenceAndForbidsResourceChanges` (the prompt keeps the generic rule) | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | Case-insensitive repository search for IaC terms and `remediation` | Manual | — | — |
| AC-7 | Existing architecture tests, unchanged | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |

Existing M5 tests keep passing unchanged and keep covering the report shape itself:
`Render_WithACompleteDiagnosis_WritesTheSevenContractSectionsInOrder`,
`Render_WhenBothToolsWereUnavailable_SaysSoAndListsNoEvidence` and
`Deterministic_WithTheSameHandoffs_RendersByteIdenticalReports` in
`tests/DevOpsCopilot.Domain.Tests`.

## Unit tests (written first)

`tests/DevOpsCopilot.Infrastructure.Tests` — the renamed agent, with fakes and no network:

- **`WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections`** (AC-2) –
  Arrange: a `DiagnosisAgent` over an unconfigured `Diagnose` and a recording report writer, and
  the two handoffs for one error event with both tools `unavailable`. Act: `WriteAsync`. Assert:
  the written document contains `# Incident report:`, `## Impact`, `## Likely cause`,
  `## Evidence`, `## Similar incidents`, `## Suggested steps` and `## Open questions` in that
  order, exactly once each, and contains no `## Slack`, `## Teams` or `## Bicep` heading and no
  ```` ```bicep ```` fence.
- **`WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites`** (AC-3) – Arrange: a
  `RunIncidentPipeline` over fake tools that produce known handoffs, and a `DiagnosisAgent` over
  the same unconfigured `Diagnose` and a recording writer, given those same handoffs. Act: run
  both. Assert: the two written documents are equal, byte for byte.
- **`DiagnosisAgent_WithAChatClient_ForbidsResourceChangesInItsInstructions`** (AC-4) –
  Arrange: the agent's instruction text (a constant on the type, so the assertion needs no Agent
  Framework call). Act: construct the agent with a fake chat client. Assert: `ChatAgent` is built,
  the instructions restrict the model to the handoffs' evidence, forbid any command, script or
  template that changes a resource, and address suggested steps to an engineer, and the agent's
  description mentions neither Slack nor Teams.
- **`ProposeAsync_WithOkHandoffs_PromptsWithIndexedEvidenceAndForbidsResourceChanges`** (AC-6) –
  the existing M5 prompt test, renamed from "…ForbidsInfrastructureCode". It now asserts the
  prompt says steps are carried out "by hand" and forbids anything that would "change a
  resource", instead of asserting the names of IaC technologies.
- **`AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent`** (AC-1) – Arrange: a service
  collection with the unconfigured settings. Act: `AddInfrastructure` and build the provider.
  Assert: `DiagnosisAgent` resolves as a singleton, alongside the existing `DiagnosticAgent` and
  `RagGroundingAgent` registrations.
- **`WriteAsync_ForAReport_NamesTheFileFromTheEventDayAndId`** (AC-5) – the existing
  `FileReportWriterTests` test, renamed from "ForADraft" and re-arranged to write an
  `IncidentReport.Render(...)` document instead of an `IncidentDraft` one. File naming and content
  equality assertions stay as they are.

Deleted with the type they test (AC-5): `IncidentDraftTests` in `tests/DevOpsCopilot.Domain.Tests`,
including `Render_WhenNoResourceIsNamed_WritesSevenSectionsWithoutABicepFence`, whose job — proving
the written document's sections match the contract — is already done by the M5 renderer tests
listed above.

## Architecture tests

- No new rule. The existing layer and package rules must pass unchanged after the rename and the
  deletion (AC-7).

## Manual checks

- `grep -ri "remediation" .` (excluding `.git`) matches only: historical spec folders
  (`2026-10-24-clean-architecture-split`, `2026-10-25-m5-diagnosis-agent`, this folder), the
  roadmap's history, and mission.md's and README's out-of-scope statements ("automated
  remediation"), which name what the product refuses to do (AC-6).
- `rg -il "bicep|terraform|infrastructure-as-code|infrastructure as code|infrastructure code|\bARM\b" --glob '!.git' --glob '!**/package-lock.json' .`
  lists only `docs/specs/roadmap.md` (its Out of scope entry, plus the M4 gap-table row recording
  the deleted Bicep section) and spec folders (AC-6). `package-lock.json` is excluded because its
  `arm` matches are CPU architectures, not ARM templates.
- `rg -i "remediation|bicep|terraform|infrastructure-as-code|infrastructure code" src tests`
  returns nothing (AC-1, AC-5, AC-6).

---

## Results

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | `AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent`; solution-wide search for `RemediationAgent` | Pass | No type, file or registration named `RemediationAgent` remains. |
| AC-2 | `WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections` | Pass | Seven headings once each, in order; no Slack, Teams or Bicep section. |
| AC-3 | `WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites` | Pass | Agent and pipeline output byte-identical on the deterministic path. |
| AC-4 | `DiagnosisAgent_WithAChatClient_ForbidsResourceChangesInItsInstructions` | Pass | Instructions forbid any command, script or template that changes a resource and address steps to an engineer; description mentions neither Slack nor Teams. |
| AC-5 | Build after deleting `IncidentDraft` and `IncidentDraftTests`; `WriteAsync_ForAReport_NamesTheFileFromTheEventDayAndId` | Pass | `IncidentReport` is the only renderer; the writer test renders through it. |
| AC-6 | `ProposeAsync_WithOkHandoffs_PromptsWithIndexedEvidenceAndForbidsResourceChanges`; sweep greps below | Pass | `src` and `tests` have no IaC or "remediation" match; the prompt and instructions keep the generic rule. Outside them, IaC is named only in the roadmap (Out of scope entry and M4's history) and spec folders; "remediation" survives only in out-of-scope statements and history. |
| AC-7 | `tests/DevOpsCopilot.Architecture.Tests` | Pass | All 9 architecture tests pass unchanged. |

- **`dotnet build`:** clean, 0 errors; only pre-existing NuGet advisory warnings in the leftover sample projects.
- **`dotnet test`:** 101 / 104 passed, 0 failed, 3 skipped (the `RequiresAzureOpenAI` integration test and the two `RequiresDocker` AppHost tests, both environment-gated as designed).
- **Sweep greps:** the `rg` check over `src` and `tests` returns nothing; the repo-wide IaC grep lists only `docs/specs/roadmap.md` and spec folders, as listed under Manual checks.
- **Date:** 2026-11-04
