# Validation: Report matches the contract

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-1 | Solution-wide search for `RemediationAgent` returns nothing | Manual | — | — |
| AC-2 | `WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-3 | `WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-4 | `DiagnosisAgent_WithAChatClient_ForbidsInfrastructureCodeInItsInstructions` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-5 | The solution compiles with `IncidentDraft` and `IncidentDraftTests` deleted; `WriteAsync_ForAReport_NamesTheFileFromTheEventDayAndId` renders through `IncidentReport` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | Case-insensitive repository search for `remediation` and `bicep` | Manual | — | — |
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
- **`DiagnosisAgent_WithAChatClient_ForbidsInfrastructureCodeInItsInstructions`** (AC-4) –
  Arrange: the agent's instruction text (a constant on the type, so the assertion needs no Agent
  Framework call). Act: construct the agent with a fake chat client. Assert: `ChatAgent` is built,
  the instructions restrict the model to the handoffs' evidence and name Bicep, ARM, Terraform and
  resource-changing scripts as forbidden, and the agent's description mentions none of Slack,
  Teams or Bicep.
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
- `grep -ri "bicep" .` (excluding `.git`) matches only: statements of the prohibition (mission.md,
  AGENTS.md, `DiagnosisPrompt`, `AzureOpenAIDiagnosisModelTests` asserting the prompt forbids it)
  and historical spec folders (AC-6).
- `rg -i "remediation|bicep" src tests --glob '!**/DiagnosisPrompt.cs' --glob '!**/AzureOpenAIDiagnosisModelTests.cs'`
  returns nothing (AC-1, AC-5, AC-6).

---

## Results

<!-- Fill in after implementation. Include this section in the pull request. -->

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |

- **`dotnet build`:**
- **`dotnet test`:** <passed> / <total>
- **Sweep greps:**
- **Date:** YYYY-MM-DD
