# Requirement: Report matches the contract

| | |
|---|---|
| **Branch** | `features/2026-11-04-m4-report-contract` |
| **Milestone** | M4 – Report matches the contract ([roadmap](../roadmap.md#m4--report-matches-the-contract-s)) |
| **Status** | Done |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-11-04 |

## Summary

Remove the last remains of the pre-contract report path. M5 already delivered the seven-section
report of [pipeline.md § 7](../../pipeline.md#report-sections-in-this-order) and `RunIncidentPipeline`
writes through it, but the third agent is still named `RemediationAgent`, still describes itself as
writing "the Slack, Teams, and Bicep draft", and still renders the superseded `IncidentDraft` with
its Slack, Teams and Bicep sections. This feature renames the agent to `DiagnosisAgent`, puts the
diagnose-and-render path behind it, gives it instructions that forbid infrastructure code, deletes
`IncidentDraft` and its tests, and sweeps the repository for "remediation" and "Bicep".

## Why

[mission.md principle 1](../mission.md#principles) is **diagnose, don't act**: DevOpsCopilot never
generates Bicep, ARM, Terraform or scripts that change resources. A type that renders a `## Bicep`
section contradicts that rule even while nothing calls it, and an agent named "remediation" uses a
word the product's language explicitly rejects. The roadmap's gap table names this as the gap
closed by M4.

The scope here is the reduced M4 agreed in decision **D1** of the M5 spec
([2026-10-25-m5-diagnosis-agent](../2026-10-25-m5-diagnosis-agent/requirement.md#decisions)): M5
took the seven-section renderer, so M4 keeps the rename, the agent instructions, the deletion of
the superseded draft, and the documentation sweep. This pull request also rewrites the roadmap's M4
entry to match D1, which M5 left undone.

## Scope

**In scope**

- Rename `RemediationAgent` to `DiagnosisAgent`: the file, the class, the `AgentName` constant and
  the registration in `InfrastructureServiceCollectionExtensions`.
- The agent's `WriteAsync` produces the contract report, not the draft: it runs the existing
  `Diagnose` stage over the two handoffs, renders the result with `IncidentReport.Render` and
  writes it through `IReportWriter` — the same path, and the same bytes on the deterministic path,
  as `RunIncidentPipeline`.
- The agent's chat instructions and description name diagnosis, restrict it to the handoffs'
  evidence, and forbid Bicep, ARM, Terraform and any script or template that changes resources.
- Delete `IncidentDraft` and `IncidentDraftTests`. The only report renderer left is
  `IncidentReport`. `FileReportWriterTests` stops rendering through the deleted type.
- Sweep the repository for "remediation" and "Bicep": the `.gitignore` comment, the README's
  Phase 1 description, and the roadmap's gap table. Mentions that *state the prohibition*
  (mission.md, AGENTS.md, the diagnosis prompt and its test) and historical spec folders stay.
- Update the roadmap when the milestone's check passes, per its own working rules.

**Out of scope**

- Any change to what the written report contains. M5 fixed the report shape; the deterministic
  output for the same handoffs stays byte-identical.
- The pipeline's stage order, the `Diagnose` stage, the citation guard or the renderer. They are
  M5's and are not touched.
- Notifying any channel (M6, M14, M15), keying by `incidentId` (M12, M13), and parsing
  diagnostic-settings records (M10).
- M5's own unfinished bookkeeping: its empty **Results** section, its Draft status, the end-to-end
  emulator test its plan lists, and marking M5 done in the roadmap. Those belong to M5's branch,
  not this one.

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | The diagnostic handoff from stage 4 | [pipeline.md § 4 – Diagnostic handoff](../../pipeline.md#diagnostic-handoff) |
| **Input** | The grounding handoff from stage 5 | [pipeline.md § 5 – Grounding handoff](../../pipeline.md#grounding-handoff) |
| **Output** | One seven-section report file, unchanged in location and naming (`incidents/YYYY-MM-DD-<event-id>.md`) | [pipeline.md § 7 – Report sections](../../pipeline.md#report-sections-in-this-order) |

## Contract and rules touched

- [pipeline.md § 7 – Report and notify](../../pipeline.md#7-report-and-notify) – the last code
  that predates the seven-section shape is removed.
- [pipeline.md § 6 – Diagnose](../../pipeline.md#6-diagnose-diagnosis-agent) – read only; the
  renamed agent sits in front of the existing stage.
- [mission.md § Principles, 1](../mission.md#principles) – the agent's instructions state the
  no-infrastructure-code rule.

No contract change. The contract already forbids what this feature deletes.

## Domain language

| Term | Meaning in this feature |
|---|---|
| **Diagnosis agent** | The third agent, the Microsoft Agent Framework surface over the diagnose-and-report path. Named `DiagnosisAgent` from this feature on. |
| **Report** | The rendered seven-section document of pipeline.md § 7. |
| **Draft** | The superseded pre-contract document with Slack, Teams and Bicep sections. Removed from the codebase by this feature; the word survives only in historical specs. |

## Acceptance criteria

**AC-1** – The third agent is the diagnosis agent
- **Given** the Infrastructure layer registered through `AddInfrastructure`
- **When** the third agent is resolved
- **Then** it is a `DiagnosisAgent` whose `AgentName` is `DiagnosisAgent`, and no type, file or
  registration named `RemediationAgent` exists in the solution

**AC-2** – The agent writes the contract report, not the draft
- **Given** the diagnostic and grounding handoffs for one error event and no configured model
- **When** the agent's `WriteAsync` runs
- **Then** the written document is the seven-section report (header, Impact, Likely cause,
  Evidence, Similar incidents, Suggested steps, Open questions, in order) and contains no
  `## Slack`, `## Teams` or `## Bicep` section

**AC-3** – The agent and the pipeline write the same report
- **Given** the same handoffs and the same unconfigured diagnosis model
- **When** the agent writes and the pipeline runs the same event
- **Then** the two documents are byte-identical, so the rename changed no behaviour

**AC-4** – The agent's instructions forbid infrastructure code
- **Given** a chat client
- **When** the agent is constructed
- **Then** its instructions tell the model to use only the handoffs' evidence and to never produce
  Bicep, ARM, Terraform or any script or template that changes resources, and its description no
  longer mentions Slack, Teams or Bicep

**AC-5** – The superseded draft is gone
- **Given** the solution
- **When** it is built and searched
- **Then** `IncidentDraft` and `IncidentDraftTests` no longer exist, and the only type that
  renders a report is `IncidentReport`

**AC-6** – The sweep leaves only prohibitions and history
- **Given** the repository
- **When** it is searched case-insensitively for `remediation` and `bicep`
- **Then** every remaining match either states the prohibition (mission.md, AGENTS.md, the
  diagnosis prompt and the test that asserts the prompt forbids it) or sits in a spec folder as a
  historical record; in particular the `.gitignore` comment and the README no longer use
  "remediation" for the product's report

**AC-7** – The layers stay clean
- **Given** the solution
- **When** the architecture tests run
- **Then** they pass unchanged: dependencies still point inwards only and Application still
  references no Azure SDK or Agent Framework package

## Open questions

- None. The scope split with M5 was decided in that spec's D1 and is applied here unchanged.
