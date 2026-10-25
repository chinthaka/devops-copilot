# Requirement: Azure OpenAI diagnosis

| | |
|---|---|
| **Branch** | `features/2026-10-25-m5-diagnosis-agent` |
| **Milestone** | M5 – Azure OpenAI diagnosis, covering M5.1, M5.2 and M5.3 ([roadmap](../roadmap.md#m5--azure-openai-diagnosis-m)) |
| **Status** | Draft |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-10-25 |

## Summary

Add stage 6, **Diagnose**, to the pipeline. The two handoffs the pipeline already produces are turned into one structured `Diagnosis` — title, likely cause, confidence, impact, evidence citations, similar incidents, suggested steps and open questions — and that object is rendered into the seven report sections the contract requires. An Azure OpenAI chat client produces the diagnosis when it is configured; without it, a deterministic diagnosis is produced from the handoffs alone and the report says the model was unavailable. A citation guard rejects any model diagnosis that cites evidence which does not exist.

One branch and this one spec deliver all three roadmap sub-milestones: the diagnosis type and renderer (M5.1), the chat client wiring and prompt (M5.2) and the citation check (M5.3).

## Why

[pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent) defines a diagnosis stage that the code does not have. Today the third agent formats the handoffs directly into a draft, so the report has no likely cause, no confidence and no suggested steps — the three things [mission.md](../mission.md#what-devopscopilot-does) promises the on-call engineer. Everything downstream in Phase 1 reads the diagnosis: M4 reshapes the written report around it, M6 renders a Teams card from it, and M8 scores it. This is the milestone that makes the product say something useful.

It is also the first place an LLM touches the product, so principle 2, **never invent evidence** ([mission.md § Principles](../mission.md#principles)), has to be enforced in code rather than in a prompt. That is what the citation guard is for.

## Scope

**In scope**

- A `Diagnosis` value type in Domain with the shape in [pipeline.md § 6](../../pipeline.md#6-diagnose-diagnosis-agent), and a `Confidence` value object (`low`, `medium`, `high`).
- A renderer that turns a `Diagnosis` plus the two handoffs into the seven report sections of [pipeline.md § 7](../../pipeline.md#report-sections-in-this-order), in order: header, impact, likely cause with confidence, evidence, similar incidents, suggested steps, open questions.
- A **deterministic diagnosis** built from the handoffs alone, with no model: title from the exception type or category, likely cause stated as not determined, `low` confidence, evidence from the findings, similar incidents from the hits, and an open question recording that the model was unavailable.
- `IDiagnosisModel` in Application and an Azure OpenAI implementation in Infrastructure that asks for the `Diagnosis` as structured output, behind the existing third agent.
- Configuration for the Azure OpenAI endpoint, deployment and credential, read only in the Host and passed down as a settings record. When it is absent the model reports itself as not configured and the deterministic path runs.
- The **confidence rule** from the contract, enforced in code and not in the prompt: confidence is `low` whenever both `kqlStatus` and `searchStatus` are `unavailable`, whatever the model returned.
- A **citation guard**: a diagnosis whose evidence cites a KQL entry or row index that does not exist, or whose similar incidents name a hit id that was not returned, is rejected. The pipeline then falls back to the deterministic diagnosis at `low` confidence with an open question saying the model's diagnosis was rejected.
- The report stage writes the rendered seven-section report through the existing `IReportWriter` to `incidents/`, unchanged in location and naming.
- An integration test against a real Azure OpenAI deployment, skipped unless the configuration is present, in the manner of the Docker-gated AppHost tests.

**Out of scope**

- Renaming `RemediationAgent` to `DiagnosisAgent`, deleting the leftover Slack, Teams and Bicep sections of `IncidentDraft`, and the repository-wide sweep for "remediation" and "Bicep". That is **M4**, which now runs after this milestone — see decision **D1**.
- Notifying any channel. Teams is M6, Slack is M15, work items are M14. The report is still a file.
- The staged-incident evaluation set and the CI quality gate (M8). This milestone adds no CI job that needs Azure OpenAI secrets.
- Keying anything by `incidentId`, or diagnosing more than one event at a time. Handoffs stay keyed by `eventId` until M12 and M13; see decision **D3**.
- Follow-up KQL rounds and the eight-query limit (M13.2). The model gets the evidence that stage 4 already gathered and cannot ask for more.
- Prompt or model tuning for quality. Quality is measured in M8; this milestone only has to be correct and safe.

## Decisions

**D1 – This milestone owns the seven-section renderer, so M4 shrinks.**
The roadmap already puts "a renderer that turns it into the seven-section report" in M5.1, and the three sections M4 cannot fill on its own (likely cause, confidence, suggested steps) come from the `Diagnosis`. So the new renderer lands here and the pipeline writes through it. M4 keeps the rename of the agent, its instructions forbidding infrastructure code, the deletion of the superseded `IncidentDraft` with its Slack, Teams and Bicep sections, and the documentation sweep. The roadmap text for M4 is updated in this pull request to say so.
*Consequence:* `IncidentDraft` is left in the tree, unused by the pipeline, between the merge of M5 and the merge of M4. This is deliberate and recorded in the plan.

**D2 – The guard is a pure rule, the fallback is a use-case decision.**
Validating citations needs no infrastructure, so it is a pure function in Domain over the diagnosis and the two handoffs. Choosing the deterministic diagnosis when validation fails is stage orchestration, so it lives in the Application use case. The Infrastructure implementation never decides to fall back; it returns what the model said, or nothing.

**D3 – Impact is what a single event can honestly report.**
`LogEvent` has no `resourceId` until M10, so `impact.resources` is empty, `impact.services` holds the event's category, `impact.eventCount` is 1 and `impact.window` is the event's timestamp as a single instant. The report does not invent a resource or a time range. This is noted in the report's open questions when resources are empty.

**D4 – The model is asked for the whole `Diagnosis` as structured output, not for prose.**
`Microsoft.Extensions.AI` structured output gives a typed object that the guard can check field by field. Free text would have to be parsed, and an unparseable answer is indistinguishable from an invented one. A response that does not bind to the shape is treated exactly like a rejected diagnosis.

**D5 – Degrading is never failing.**
A missing configuration, a timeout, a transport error, a refusal or an unbindable response all take the same path: the deterministic diagnosis, `low` confidence, and an open question that names what went wrong. The worker keeps consuming and still checkpoints. A diagnosis is never retried against the model within one event.

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | The diagnostic handoff from stage 4 | [pipeline.md § 4 – Diagnostic handoff](../../pipeline.md#diagnostic-handoff) |
| **Input** | The grounding handoff from stage 5 | [pipeline.md § 5 – Grounding handoff](../../pipeline.md#grounding-handoff) |
| **Output** | One structured diagnosis | [pipeline.md § 6 – Diagnosis](../../pipeline.md#6-diagnose-diagnosis-agent) |
| **Output** | One report file rendered from the diagnosis, written to `incidents/YYYY-MM-DD-<event-id>.md` | [pipeline.md § 7 – Report sections](../../pipeline.md#report-sections-in-this-order) |

## Contract and rules touched

- [pipeline.md § 6 – Diagnose](../../pipeline.md#6-diagnose-diagnosis-agent) – implemented by this feature.
- [pipeline.md § 7 – Report and notify](../../pipeline.md#7-report-and-notify) – the seven sections are implemented; the channel table is not.
- [pipeline.md § Evidence rule](../../pipeline.md#evidence-rule) – the guard is how this stage obeys it.
- [pipeline.md § 4](../../pipeline.md#4-gather-evidence-diagnostic-agent) and [§ 5](../../pipeline.md#5-ground-grounding-agent) – read only, as the diagnosis's sources of citable evidence.

No contract change is required. The contract already describes this stage; the code does not yet have it.

## Domain language

| Term | Meaning in this feature |
|---|---|
| **Diagnosis** | The structured result of stage 6: title, likely cause, confidence, impact, evidence, similar incidents, suggested steps, open questions. |
| **Confidence** | `low`, `medium` or `high`. Forced to `low` when both tools were unavailable. |
| **Evidence citation** | A `{ kql, row }` pair pointing at an entry of the diagnostic handoff's `Kql` and `Rows` lists. |
| **Deterministic diagnosis** | The diagnosis built from the handoffs with no model, used when the model is unconfigured, failing or rejected. |
| **Citation guard** | The rule that rejects a diagnosis citing evidence that was not returned by a tool. |
| **Report** | The rendered seven-section document written for one event. |

## Acceptance criteria

**AC-1** – The diagnosis type carries the contract's shape
- **Given** the diagnostic and grounding handoffs for one event
- **When** a `Diagnosis` is constructed
- **Then** it exposes title, likely cause, confidence, impact (services, resources, event count, window), evidence citations, similar incidents, suggested steps and open questions, and `Confidence` rejects any value outside `low`, `medium`, `high`

**AC-2** – The report has the seven contract sections, in order
- **Given** any diagnosis and its handoffs
- **When** the report is rendered
- **Then** the document contains the header, Impact, Likely cause, Evidence, Similar incidents, Suggested steps and Open questions sections in the order given in [pipeline.md § 7](../../pipeline.md#report-sections-in-this-order), the header names the event id, the trace ids and hub `logs`, and the likely cause section states the confidence

**AC-3** – Unavailable tools are stated, never filled in
- **Given** a diagnostic handoff with `kqlStatus` `unavailable` and a grounding handoff with `searchStatus` `unavailable`
- **When** the report is rendered
- **Then** the Evidence section says KQL was unavailable, the Similar incidents section says search was unavailable, both list nothing, and neither section names a row or a hit

**AC-4** – Without Azure OpenAI the deterministic diagnosis is used and says so
- **Given** no Azure OpenAI configuration
- **When** an error event runs the pipeline
- **Then** a report is still written, its confidence is `low`, its open questions record that the diagnosis model was unavailable, and no claim cites evidence that is not in the handoffs

**AC-5** – With Azure OpenAI the model's diagnosis is used
- **Given** a configured diagnosis model that returns a well-formed diagnosis citing existing evidence
- **When** an error event runs the pipeline
- **Then** the written report's title, likely cause and suggested steps are the model's, and the open questions do not claim the model was unavailable

**AC-6** – Confidence is `low` when both tools were unavailable
- **Given** a model that returns `high` confidence while both `kqlStatus` and `searchStatus` are `unavailable`
- **When** the diagnosis is produced
- **Then** the confidence of the diagnosis and of the report is `low`

**AC-7** – A diagnosis citing evidence that does not exist is rejected
- **Given** a model that returns a diagnosis citing a KQL entry or row index outside the handoff's lists, or a similar incident id that is not among the returned hits
- **When** the diagnosis is produced
- **Then** the model's diagnosis is discarded, the deterministic diagnosis is used at `low` confidence, and an open question records that the model's diagnosis was rejected for citing evidence that was not returned

**AC-8** – A failing model degrades rather than fails the run
- **Given** a configured model that throws, times out or returns a response that does not bind to the diagnosis shape
- **When** an error event runs the pipeline
- **Then** the report is still written from the deterministic diagnosis, an open question names the failure, the event is still checkpointed, and no exception escapes the stage

**AC-9** – Impact reports only what the event supports
- **Given** one event with a category and a timestamp and no resource id
- **When** the diagnosis is produced
- **Then** impact has event count 1, the event's category as its only service, no resources, and a window equal to the event's timestamp

**AC-10** – The layers stay clean
- **Given** the solution
- **When** the architecture tests run
- **Then** Domain still references no package, Application still references no Azure SDK or Agent Framework package, and the Azure OpenAI client exists only in Infrastructure and is wired only in the Host

**AC-11** – A real deployment produces a cited report
- **Given** an Azure OpenAI endpoint, deployment and credential in user secrets
- **When** the model integration test runs
- **Then** it returns a diagnosis that binds to the shape and passes the citation guard; without the configuration the test is skipped, not failed

## Non-functional requirements

- **Cost and latency.** One model call per event, no retries and no follow-up rounds. A timeout bounds the call; exceeding it takes the deterministic path (AC-8).
- **Secrets.** The endpoint, deployment and credential are read from Aspire references or user secrets only. No value is written into the repo, into a log line or into the report file.
- **Determinism of the fallback.** The deterministic diagnosis for the same handoffs is byte-identical every time, so tests and the later evaluation set are stable.
- **Idempotency.** Re-running the pipeline for the same event overwrites the same report path, as it does today.

## Open questions

1. **D1 shrinks M4's scope and changes the roadmap text for it.** Confirm that M4 keeps only the rename, the agent instructions, the deletion of the superseded `IncidentDraft`, and the sweep — and that leaving `IncidentDraft` unused between the two merges is acceptable.
2. **Credential.** API key from user secrets, or `DefaultAzureCredential` / managed identity? The plan assumes **both, with the key used only when present**, since M7 deploys to Container Apps where managed identity is the right answer.
3. **Model and deployment name.** No Azure OpenAI deployment is named anywhere in the repo. The plan assumes the deployment name is configuration with no default, and that the `tech-stack.md` entry records the model family actually used once chosen.
4. **Timeout.** The plan assumes 30 seconds for the model call. Confirm or set another value.
