# Plan: Report matches the contract

## Approach

This is a rename-and-delete feature; the one design decision is what the renamed agent wraps.

`DiagnosisAgent` mirrors the other two agents: each is the Microsoft Agent Framework surface over
an Application use case (`DiagnosticAgent` over `GatherEvidence`, `RagGroundingAgent` over
`GroundInHistory`). The third agent therefore takes the existing `Diagnose` stage and the
`IReportWriter`, and its `WriteAsync` becomes: diagnose the two handoffs, render the result with
`IncidentReport.Render`, write it — the same tail `RunIncidentPipeline` already runs. On the
deterministic path the output is byte-identical to the pipeline's (M5 guarantees the renderer is
deterministic), which is what AC-3 asserts.

Two alternatives were rejected. Making the pipeline call the agent would point Application at
Infrastructure, which is forbidden. Leaving the agent rendering its own document would keep two
report shapes alive, which is the gap this milestone exists to close.

The chat instructions move from "format the draft" to the diagnosis wording and carry the
no-infrastructure-code rule from [mission.md principle 1](../mission.md#principles). They live in a
public constant on the type so the test asserts them without constructing anything from the Agent
Framework.

With no caller left, `IncidentDraft` and its tests are deleted rather than deprecated. The M5
renderer tests already prove the seven sections, so nothing is lost with
`Render_WhenNoResourceIsNamed_WritesSevenSectionsWithoutABicepFence`.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | Delete `Reporting/IncidentDraft.cs` | Nothing added. `IncidentReport` is untouched. |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `Agents/RemediationAgent.cs` → `Agents/DiagnosisAgent.cs` (class, `AgentName`, description, instructions, constructor gains `Diagnose`); `InfrastructureServiceCollectionExtensions` registration updated | File named after the type, per AGENTS.md. |
| Tests | `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Infrastructure.Tests` | Delete `Reporting/IncidentDraftTests.cs`; add `Agents/DiagnosisAgentTests.cs`; update `FileReportWriterTests` and the `AddInfrastructure` tests | Test folders keep mirroring the code. |

Application and Host are untouched: `RunIncidentPipeline`, `Diagnose` and `Program.cs` do not name
the agent. Dependencies still point inwards only.

Outside the solution: the `.gitignore` comment ("Incident drafts written by the remediation
agent"), the README's Phase 1 line ("write a remediation report"), and the roadmap's gap table row
for M4.

## Interfaces

None new or changed. The agent keeps using `IReportWriter` and the concrete `Diagnose` stage, as
the other agents use their concrete use cases.

## Contract changes

None. This feature deletes the last code that predates the existing contract.

## Tech-stack changes

None. No package moves; `tech-stack.md`'s description of the three agents already says
"diagnostic, grounding, diagnosis".

## Implementation steps

1. **Rename and rewire the agent** – write
   `WriteAsync_ForAnErrorEventsHandoffs_WritesTheSevenSectionReportWithoutDraftSections` (AC-2) and
   `WriteAsync_WithTheSameHandoffs_WritesExactlyWhatThePipelineWrites` (AC-3) as failing tests,
   then rename `RemediationAgent.cs` to `DiagnosisAgent.cs`, rename the class and `AgentName`, add
   the `Diagnose` dependency, and make `WriteAsync` diagnose, render through
   `IncidentReport.Render` and write.
2. **Instructions** – write `DiagnosisAgent_WithAChatClient_ForbidsInfrastructureCodeInItsInstructions`
   (AC-4), then move the instructions and description to public constants with the diagnosis
   wording and the Bicep/ARM/Terraform/script prohibition.
3. **Registration** – write `AddInfrastructure_WithDefaults_RegistersTheDiagnosisAgent` (AC-1),
   then update the registration in `InfrastructureServiceCollectionExtensions` to construct the
   agent with the registered `Diagnose`.
4. **Delete the draft** – update `FileReportWriterTests` to arrange its document with
   `IncidentReport.Render` and rename the test to `WriteAsync_ForAReport_NamesTheFileFromTheEventDayAndId`
   (AC-5), then delete `IncidentDraft.cs` and `IncidentDraftTests.cs` and confirm the build is
   clean.
5. **Sweep** – update the `.gitignore` comment and the README Phase 1 wording, mark the M4 row of
   the roadmap's gap table closed, and run the three greps from
   [validation.md § Manual checks](validation.md#manual-checks) (AC-6).
6. **Verify** – `dotnet build`, `dotnet test` (architecture tests included, AC-7), mark M4 done in
   the roadmap, set the requirement's status, and fill in the Results section of validation.md.

## Risks

| Risk | Mitigation |
|---|---|
| The Agent Framework's `ChatClientAgent` does not expose its instructions for assertion | The instructions are a public constant on `DiagnosisAgent`; the test asserts the constant and only checks that `ChatAgent` was built. |
| Byte-identical comparison with the pipeline is brittle | Both paths call the same `IncidentReport.Render` over the same deterministic diagnosis, and M5's `Deterministic_WithTheSameHandoffs_RendersByteIdenticalReports` already pins the renderer; the comparison fails only if the two paths genuinely diverge, which is exactly what it is for. |
| Hidden references to the old names outside the solution (scripts, CI, docs) | The sweep greps in validation.md run over the whole repository, not just `src` and `tests`. |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; `dotnet build` and `dotnet test` clean
- [ ] No outward project references; architecture tests pass
- [ ] `docs/pipeline.md` and `docs/specs/tech-stack.md` unchanged (no contract or stack change)
- [ ] Roadmap gap table and M4 status updated
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
