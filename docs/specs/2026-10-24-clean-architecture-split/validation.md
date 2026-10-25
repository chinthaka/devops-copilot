# Validation: Clean Architecture split of DevOpsCopilot.Daemon

This feature changes structure, not behaviour. Validation therefore has two halves: new architecture tests that prove the layering (AC-1 to AC-5, AC-8, AC-9), and the eight existing tests moved to their new homes with their assertions unchanged, proving nothing broke (AC-6, AC-7).

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `dotnet build src/DevOpsCopilot.sln` succeeds | Manual | — | — |
| AC-1 | `Every_layer_project_targets_net10_and_references_only_inwards` | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-2 | `Domain_references_no_external_framework` | Architecture | Domain | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-3 | `Application_references_no_azure_sdk_or_agent_framework` | Architecture | Application | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-4 | `Domain_does_not_reference_application_or_infrastructure` | Architecture | Domain | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-4 | `Infrastructure_does_not_reference_the_host` | Architecture | Infrastructure | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-5 | `Pipeline_runs_evidence_then_grounding_then_report` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-5 | `Application_tests_reference_no_infrastructure_project` | Architecture | Application | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-6 | `Parse_reads_the_log_fields_and_w3c_trace_context` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `Warning_without_an_exception_does_not_run_the_team` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Error_without_kusto_configuration_records_three_queries_and_no_rows` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Missing_search_configuration_returns_no_hits` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-6 | `Kql_that_contains_drop_is_not_executed` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Draft_file_is_named_from_the_event_day_and_id` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `Restart_resumes_after_the_last_handled_checkpoint` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `Daemon_project_no_longer_exists_in_the_solution` | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-8 | AppHost starts the worker under `aspire run` | Manual | Host | — |
| AC-9 | Deliberate violation fails an architecture test | Manual | — | — |
| AC-10 | Documentation review in the pull request | Manual | — | — |

Domain and Application tests are plain unit tests with no Azure SDK, network or emulator. The Infrastructure tests listed here are also plain unit tests — the parser and the file writer need neither. No emulator-backed integration test is added by this feature; `EventHubLogConsumer` has no test today and this feature does not add one, because moving it is a file move with no logic change.

## Unit tests (written first)

Eight of these are moves. For a move, "written first" means the test is placed in its new project and compiled against the new type names before the old project is deleted — it fails to compile, then passes once the types exist in their target layer. The assertions are not edited.

### New

- **`Pipeline_runs_evidence_then_grounding_then_report`** (AC-5) – Arrange: an error `LogEvent`, a recording `ILogQuery`, a recording `IKnowledgeSearch` and a recording `IReportWriter`, all appending to one shared call log; construct `RunIncidentPipeline` directly, with no DI container and no Infrastructure type in scope. Act: run the use case. Assert: the call log reads query, search, write in that order, and the returned `IncidentWorkflowResult` carries both handoffs and the path the writer returned.
- **`Draft_file_is_named_from_the_event_day_and_id`** (AC-6) – Arrange: a `FileReportWriter` over a temp directory and a `DiagnosticHandoff` whose `EventId` is `evt-20261020100000000`. Act: write the draft. Assert: the file is at `<temp>/2026-10-20-evt-20261020100000000.md`, matching what `RemediationAgent.Write` produces today, and its content equals `IncidentDraft.Render(...)` for the same handoffs.

### Moved unchanged

- **`Parse_reads_the_log_fields_and_w3c_trace_context`** (AC-6) – Arrange: a JSON body with timestamp, level, category, message, exception, trace id, span id and one attribute. Act: parse it with the Infrastructure parser. Assert: every field round-trips, including `TraceId`, `SpanId` and `Attribute["operation"]`.
- **`Restart_resumes_after_the_last_handled_checkpoint`** (AC-7) – Arrange: a fake `IEventSource` holding two error events that delivers the one after its last checkpoint, and a pipeline over unconfigured tools writing to a temp directory. Act: run the processor twice. Assert: events 0 then 1 are delivered, the checkpoint index ends at 1, every checkpoint happened after its draft was written, and two `.md` files exist.
- **`Warning_without_an_exception_does_not_run_the_team`** (AC-6) – Arrange: a `Warning` event with no exception and recording tools. Act: run the pipeline. Assert: the result is null, neither tool executed, and no file was written.
- **`Error_without_kusto_configuration_records_three_queries_and_no_rows`** (AC-6) – Arrange: an `Error` event with an exception, `ILogQuery` reporting not configured. Act: run the pipeline. Assert: status is `unavailable`, no rows, no findings, three distinct KQL strings recorded, containing the trace id, the exception first line and `Success == false` respectively, and the query runner was never executed.
- **`Missing_search_configuration_returns_no_hits`** (AC-6) – Arrange: both tools unconfigured. Act: run the pipeline. Assert: grounding status is `unavailable`, the index is `historical-postmortems`, hits are empty, queries are not empty, and search was never executed.
- **`Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service`** (AC-6) – Arrange: a configured search returning six overlapping hits and one unrelated billing hit. Act: run the pipeline on a `Critical` timeout event. Assert: status is `ok`, exactly five hits are kept, `unrelated-billing` is not among them, and search ran once.
- **`Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named`** (AC-6) – Arrange: handoffs from an unconfigured run. Act: render the draft from the Domain renderer. Assert: the seven headings appear in order, there is no ` ```bicep ` fence, and the text contains "No finding names a resource change." This test no longer reads a file; the file-writing half of it is covered by `Draft_file_is_named_from_the_event_day_and_id`.
- **`Kql_that_contains_drop_is_not_executed`** (AC-6) – Arrange: a configured recording `ILogQuery` and the KQL string `StormEvents | .drop table StormEvents`. Act: execute it through the Application evidence use case. Assert: the call is not executed, the runner ran zero times, and no rows come back.

## Architecture tests

New project `tests/DevOpsCopilot.Architecture.Tests`, referencing all four layer projects, asserting over `Assembly.GetReferencedAssemblies()` and type namespaces. No new NuGet package (decision D-12).

- **`Domain_references_no_external_framework`** (AC-2) – Assert: no referenced assembly name starts with `Azure.`, `Kusto.`, `Microsoft.Azure.`, `Microsoft.Agents.`, `Microsoft.Extensions.` or `Newtonsoft.`, and none is `System.Text.Json`.
- **`Application_references_no_azure_sdk_or_agent_framework`** (AC-3) – Assert: no referenced assembly name starts with `Azure.`, `Kusto.`, `Microsoft.Azure.` or `Microsoft.Agents.`; `DevOpsCopilot.Domain` is referenced.
- **`Domain_does_not_reference_application_or_infrastructure`** (AC-4) – Assert: neither `DevOpsCopilot.Application` nor `DevOpsCopilot.Infrastructure` appears in Domain's references.
- **`Infrastructure_does_not_reference_the_host`** (AC-4) – Assert: `DevOpsCopilot.Worker` does not appear in Infrastructure's references.
- **`Every_layer_project_targets_net10_and_references_only_inwards`** (AC-1) – Arrange: read the four `.csproj` files from disk. Assert: each has `<TargetFramework>net10.0</TargetFramework>`, and the set of `ProjectReference` paths in each matches exactly the allowed inward set for its layer.
- **`Application_tests_reference_no_infrastructure_project`** (AC-5) – Assert: `tests/DevOpsCopilot.Application.Tests.csproj` and `tests/DevOpsCopilot.Domain.Tests.csproj` contain no `ProjectReference` to Infrastructure or Worker. This is what stops a later feature from quietly testing Application through an Azure client.
- **`Daemon_project_no_longer_exists_in_the_solution`** (AC-8) – Assert: `src/DevOpsCopilot.sln` contains no `DevOpsCopilot.Daemon` entry and the directories `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests` do not exist.

The `.csproj`- and `.sln`-reading tests locate the repository root by walking up from `AppContext.BaseDirectory` to the directory containing `AGENTS.md`.

## Manual checks

- **Build and test:** `dotnet build src/DevOpsCopilot.sln` and `dotnet test src/DevOpsCopilot.sln` are clean, with the same number of behaviour tests as before plus the two new ones and the architecture tests.
- **AppHost (AC-8):** `aspire run` starts the emulators and a resource named `worker`; the worker connects to hub `logs` with consumer group `devops-copilot` and the `checkpoints` container is created.
- **End-to-end smoke:** with the stack running, the `LogForwarder` emits an error log; a draft appears in the drafts directory with the same shape as before the split.
- **Violation check (AC-9):** temporarily add a `ProjectReference` from Application to Infrastructure, confirm `Application_references_no_azure_sdk_or_agent_framework` and `Every_layer_project_targets_net10_and_references_only_inwards` fail, then revert. Record the observed failure in Results.
- **User secrets:** confirm the Worker project keeps the `UserSecretsId` `dotnet-DevOpsCopilot.Daemon-f6bcefe1-0b1e-448b-a7a0-18f3ac74fa34`, so existing local secrets still resolve.
- **Documentation (AC-10):** read `AGENTS.md § Layout`, `tech-stack.md § Solution structure` and `§ Testing`, `roadmap.md` M3, and `pipeline.md § 1` and confirm none still describes the worker as one project.

---

## Results

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

- **`dotnet build`:** 
- **`dotnet test`:** <passed> / <total>
- **Evaluation run:** Not applicable — this feature changes no LLM behaviour.
- **Date:** YYYY-MM-DD
