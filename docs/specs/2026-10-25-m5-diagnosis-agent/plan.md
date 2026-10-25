# Plan: Azure OpenAI diagnosis

## Approach

The pipeline gains one stage between grounding and the report. `Diagnose` in Application asks an `IDiagnosisModel` for a `Diagnosis`, validates what comes back against the two handoffs, applies the contract's confidence rule, and falls back to a deterministic diagnosis whenever the model is unconfigured, failing, unbindable or rejected. `RunIncidentPipeline` then renders that one object into the seven-section report and writes it through the existing `IReportWriter`.

Everything that can be a pure rule is one. `Diagnosis`, `Confidence`, `Impact`, `EvidenceCitation`, the deterministic diagnosis, the citation guard, the confidence rule and the renderer all live in Domain with no packages, so the whole of the contract's §6 and §7 is unit-testable without an LLM. Application owns only the decision of *which* diagnosis to use. Infrastructure owns only the call to Azure OpenAI and the mapping from its response shape into the Domain type — the model's DTO never leaves the boundary, per [AGENTS.md § Architecture](../../../AGENTS.md#architecture).

The alternative — letting the Agent Framework agent produce the report text directly, as the current third agent does — was rejected because there would be nothing to validate. A typed object is what makes "never invent evidence" ([mission.md](../mission.md#principles)) enforceable in code rather than in a prompt, and it is what M6's Teams card and M8's scoring both need to read.

The third agent keeps its current role as the Agent Framework surface and is given the model behind it. It is not renamed here; that is M4 (decision D1 in [requirement.md](requirement.md#decisions)).

## Task groups

Tests first within every group, per [AGENTS.md](../../../AGENTS.md). Each group leaves `dotnet build` and `dotnet test` green.

### 1. The diagnosis type and the rules (M5.1, Domain)

1. Write the Domain unit tests from [validation.md](validation.md#unit-tests-written-first) for `Confidence`, the diagnosis fields and the impact rule.
2. Add `Confidence` (validating value object over `low`, `medium`, `high`), `EvidenceCitation(int Kql, int Row)`, `Impact(services, resources, eventCount, window)` and the `Diagnosis` record with the shape in [pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent).
3. Add `Diagnosis.Deterministic(diagnostic, grounding, reason)`: title from the exception's first line or the category (the existing `IncidentDraft` title rule, moved not reinvented), likely cause stated as not determined, `low` confidence, impact per decision D3, evidence citing every finding's row, similar incidents from the hits, engineer-facing suggested steps, and `reason` appended to the open questions.
4. Add `DiagnosisRules.ApplyConfidenceRule`: confidence is `low` when both `KustoStatus` and `SearchStatus` are `unavailable`.

### 2. The citation guard (M5.3, Domain)

1. Write the four guard tests from validation.md.
2. Add `DiagnosisCitations.Validate(diagnosis, diagnostic, grounding)` returning a valid/invalid result with a reason: every `EvidenceCitation.Kql` indexes the handoff's `Kql`, every `Row` indexes its `Rows`, and every similar incident is the id of a returned hit. Pure, no dependencies.

### 3. The report renderer (M5.1, Domain)

1. Write the renderer tests from validation.md, including the section-order and both-unavailable tests.
2. Add `IncidentReport.Render(diagnosis, diagnostic, grounding)` producing the seven sections of [pipeline.md § 7](../../pipeline.md#report-sections-in-this-order) in order. Unavailable tools are stated in their own section and list nothing.
3. Point `RunIncidentPipeline` at the new renderer. `IncidentDraft` is left in place, unused, for M4 to delete (decision D1); its existing tests keep passing untouched.

### 4. The diagnose stage (Application)

1. Write the Application stage tests from validation.md with a fake `IDiagnosisModel`.
2. Add `IDiagnosisModel` to Application: `bool IsConfigured { get; }` and `Task<Diagnosis?> ProposeAsync(DiagnosticHandoff, GroundingHandoff, CancellationToken)`.
3. Add the `Diagnose` use case: not configured, `null`, a thrown exception or a cancellation each take the deterministic path with a reason; a returned diagnosis is validated by the guard and, if rejected, replaced by the deterministic one with the rejection as its reason; the confidence rule is applied last, in every path.
4. Insert the stage into `RunIncidentPipeline` between `GroundInHistory` and the write, and render from its result. `AgentHandoff` is extended to carry the diagnosis, or replaced by passing the diagnosis alongside — whichever keeps the use case readable.

### 5. Azure OpenAI (M5.2, Infrastructure and Host)

1. Add `AzureOpenAISettings(Endpoint, Deployment, ApiKey)` next to the existing settings records, and extend `AddInfrastructure` to take it and register the model. Absent or blank endpoint or deployment means `IsConfigured` is false and no client is constructed.
2. Add `AzureOpenAIDiagnosisModel : IDiagnosisModel`: builds the `IChatClient` from `Azure.AI.OpenAI`, asks for the diagnosis as structured output against an Infrastructure-local DTO, maps the DTO to the Domain `Diagnosis`, and returns `null` on an unbindable response. The prompt states the evidence available by index and forbids any claim that does not cite it, and forbids Bicep, ARM, Terraform and scripts, per [mission.md principle 1](../mission.md#principles).
3. Pass the configured timeout (30 s, pending open question 4) to the Application stage, which already bounds the call with a linked cancellation token. Infrastructure does not time itself out: a timeout is a fallback decision, and decision D2 keeps those in Application.
4. Register the chat client so the three existing agents keep resolving `IChatClient` as they do now, and give the third agent the diagnosis model.
5. In `src/DevOpsCopilot.Worker/Program.cs`, read `Incident:AzureOpenAI` into the settings record. No configuration is read anywhere else.
6. Write the integration test `AzureOpenAI_ReturnsABindableAndCitedDiagnosis`, gated on the configuration being present.

### 6. End to end and documentation

1. Extend the AppHost emulator test with `EndToEnd_AnErrorEvent_WritesASevenSectionReport`.
2. Add the new architecture rule: the Azure OpenAI package is referenced only by Infrastructure.
3. Update [tech-stack.md](../tech-stack.md) per the table below, including the model family chosen.
4. Update [roadmap.md](../roadmap.md): mark M5 and M5.1–M5.3 done, and rewrite M4's scope per decision D1.
5. Set this requirement's status to Done and fill in the Results section of validation.md.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | `Diagnosis`, `Confidence`, `Impact`, `EvidenceCitation`, `DiagnosisRules`, `DiagnosisCitations`, `IncidentReport` | No packages. `IncidentDraft` untouched and unused; deleted by M4. |
| Application | `src/DevOpsCopilot.Application` | `IDiagnosisModel`, `Diagnose`, `RunIncidentPipeline` (new stage), `AgentHandoff` | No Azure SDK, no Agent Framework. |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `AzureOpenAIDiagnosisModel`, its response DTO, `AzureOpenAISettings`, `InfrastructureServiceCollectionExtensions`, `Agents/RemediationAgent` (given the model) | Adds `Azure.AI.OpenAI`. The DTO never leaves this layer. |
| Host | `src/DevOpsCopilot.Worker` | `Program.cs` and `IncidentOptions` gain the Azure OpenAI section | Wiring and configuration only. |
| Tests | `tests/DevOpsCopilot.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Architecture.Tests`, `.AppHost.Tests` | New tests in existing projects | No new test project. |

Dependencies still point inwards only.

## Interfaces

| Interface | Responsibility | Implemented by |
|---|---|---|
| `IDiagnosisModel` (Application) | Propose one `Diagnosis` from the two handoffs, or report itself unconfigured. Never decides the fallback. | `AzureOpenAIDiagnosisModel` in Infrastructure; a fake in tests |

## Contract changes

None. [pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent) and [§ 7](../../pipeline.md#report-sections-in-this-order) already define this stage and the report shape; this feature implements them.

## Tech-stack changes

| Item | Change |
|---|---|
| LLM | `Azure OpenAI` moves from "Not yet wired" to built, naming the deployment's model family. |
| Package | `Azure.AI.OpenAI` added to `src/DevOpsCopilot.Infrastructure`, pinned at the version resolved during implementation and recorded here in the same pull request. |
| Testing | A new `Category=RequiresAzureOpenAI` test category, skipped without configuration and never run in CI. |

## Configuration and secrets

Read only in `src/DevOpsCopilot.Worker/Program.cs`, under the existing `Incident` section:

- `Incident:AzureOpenAI:Endpoint` – no default. Blank means unconfigured.
- `Incident:AzureOpenAI:Deployment` – no default. Blank means unconfigured.
- `Incident:AzureOpenAI:ApiKey` – user secrets locally; empty in Azure, where managed identity is used (open question 2).
- `Incident:AzureOpenAI:TimeoutSeconds` – default 30 (open question 4).

No value is committed, logged or written into a report. `appsettings.json` ships the section with
empty strings so the shape is discoverable, and `AzureOpenAISettings.ToString()` redacts the key so
it cannot reach a log line.

Set them locally with user secrets, never in a file under source control:

```bash
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:AzureOpenAI:Deployment" "<deployment-name>"
dotnet user-secrets --project src/DevOpsCopilot.Worker set "Incident:AzureOpenAI:ApiKey" "<key>"   # omit to use your Azure login
```

The same three keys, set on `tests/DevOpsCopilot.Infrastructure.Tests`, switch on the gated
integration test. In Azure the key stays empty and the worker authenticates with its managed
identity.

## Risks

| Risk | Mitigation |
|---|---|
| The model returns prose or a shape that will not bind | Structured output plus decision D5: an unbindable response is a fallback, not a failure, and is covered by `A_model_that_returns_nothing_falls_back`. |
| The model cites evidence that does not exist | The citation guard rejects it and the deterministic diagnosis is used (AC-7). |
| The model emits Bicep or a script in the suggested steps | The prompt forbids it, and the manual check looks for it. A test-level ban lands with M8's scoring; flagged here rather than silently assumed. |
| `Azure.AI.OpenAI` and `Microsoft.Extensions.AI` version drift against Agent Framework 1.25.0 | Pin the version in `tech-stack.md`; if the transitive `Microsoft.Extensions.AI` conflicts, pin it explicitly in the Infrastructure project. |
| Overlap with M4 leaves `IncidentDraft` dead in the tree between merges | Accepted and recorded in decision D1; M4 deletes it and sweeps for Bicep. |
| Leaking a key into a report or log | The settings record holds the key, nothing stringifies it, and the manual check verifies it. |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; `dotnet build` and `dotnet test` clean
- [ ] No outward project references; architecture tests pass, including the new Azure OpenAI package rule
- [ ] `docs/specs/tech-stack.md` updated; `docs/pipeline.md` unchanged (no contract change)
- [ ] `docs/specs/roadmap.md` marks M5 done and records M4's reduced scope
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
