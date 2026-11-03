# Validation: Clean Architecture split of DevOpsCopilot.Daemon

This feature changes structure, not behaviour. Validation therefore has two halves: new architecture tests that prove the layering (AC-1 to AC-5, AC-8, AC-9), and the eight existing tests moved to their new homes with their assertions unchanged, proving nothing broke (AC-6, AC-7).

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `dotnet build src/DevOpsCopilot.sln` succeeds | Manual | — | — |
| AC-1 | `LayerProjects_WhenInspected_TargetNet10AndReferenceOnlyInwards` | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-2 | `Domain_WhenBuilt_ReferencesNoExternalFramework` | Architecture | Domain | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-3 | `Application_WhenBuilt_ReferencesNoAzureSdkOrAgentFramework` | Architecture | Application | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-4 | `Domain_WhenBuilt_DoesNotReferenceApplicationOrInfrastructure` | Architecture | Domain | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-4 | `Infrastructure_WhenBuilt_DoesNotReferenceTheHost` | Architecture | Infrastructure | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-5 | `RunAsync_WithAnIncidentStartingEvent_RunsEvidenceThenGroundingThenReport` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-5 | `InwardTestProjects_WhenInspected_ReferenceNoInfrastructureOrAzurePackages` | Architecture | Application | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-6 | `Parse_WithAFullJsonBody_ReadsTheLogFieldsAndW3CTraceContext` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-6 | `RunAsync_WithAWarningWithoutAnException_DoesNotRunTheTeam` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `RunAsync_WithAnErrorButNoKustoConfiguration_RecordsThreeQueriesAndNoRows` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `RunAsync_WithoutSearchConfiguration_ReturnsNoHits` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `RunAsync_WithOverlappingAndUnrelatedHits_KeepsFiveAndDropsTheUnrelatedService` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `Render_WhenNoResourceIsNamed_WritesSevenSectionsWithoutABicepFence` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-6 | `ExecuteAsync_WithKqlThatContainsADropCommand_DoesNotExecuteTheQuery` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-6 | `WriteAsync_ForADraft_NamesTheFileFromTheEventDayAndId` | Unit | Infrastructure | `tests/DevOpsCopilot.Infrastructure.Tests` |
| AC-7 | `RunAsync_AfterARestart_ResumesFromTheLastHandledCheckpoint` | Unit | Application | `tests/DevOpsCopilot.Application.Tests` |
| AC-8 | `Solution_AfterTheDaemonRemoval_ContainsNoDaemonProject` | Architecture | — | `tests/DevOpsCopilot.Architecture.Tests` |
| AC-8 | AppHost starts the worker under `aspire run` | Manual | Host | — |
| AC-9 | Deliberate violation fails an architecture test | Manual | — | — |
| AC-10 | Documentation review in the pull request | Manual | — | — |

Domain and Application tests are plain unit tests with no Azure SDK, network or emulator. The Infrastructure tests listed here are also plain unit tests — the parser and the file writer need neither. No emulator-backed integration test is added by this feature; `EventHubLogConsumer` has no test today and this feature does not add one, because moving it is a file move with no logic change.

## Unit tests (written first)

Eight of these are moves. For a move, "written first" means the test is placed in its new project and compiled against the new type names before the old project is deleted — it fails to compile, then passes once the types exist in their target layer. The assertions are not edited.

### New

- **`RunAsync_WithAnIncidentStartingEvent_RunsEvidenceThenGroundingThenReport`** (AC-5) – Arrange: an error `LogEvent`, a recording `ILogQuery`, a recording `IKnowledgeSearch` and a recording `IReportWriter`, all appending to one shared call log; construct `RunIncidentPipeline` directly, with no DI container and no Infrastructure type in scope. Act: run the use case. Assert: the call log reads query, search, write in that order, and the returned `IncidentWorkflowResult` carries both handoffs and the path the writer returned.
- **`WriteAsync_ForADraft_NamesTheFileFromTheEventDayAndId`** (AC-6) – Arrange: a `FileReportWriter` over a temp directory and a `DiagnosticHandoff` whose `EventId` is `evt-20261020100000000`. Act: write the draft. Assert: the file is at `<temp>/2026-10-20-evt-20261020100000000.md`, matching what `RemediationAgent.Write` produces today, and its content equals `IncidentDraft.Render(...)` for the same handoffs.

### Moved unchanged

- **`Parse_WithAFullJsonBody_ReadsTheLogFieldsAndW3CTraceContext`** (AC-6) – Arrange: a JSON body with timestamp, level, category, message, exception, trace id, span id and one attribute. Act: parse it with the Infrastructure parser. Assert: every field round-trips, including `TraceId`, `SpanId` and `Attribute["operation"]`.
- **`RunAsync_AfterARestart_ResumesFromTheLastHandledCheckpoint`** (AC-7) – Arrange: a fake `IEventSource` holding two error events that delivers the one after its last checkpoint, and a pipeline over unconfigured tools writing to a temp directory. Act: run the processor twice. Assert: events 0 then 1 are delivered, the checkpoint index ends at 1, every checkpoint happened after its draft was written, and two `.md` files exist.
- **`RunAsync_WithAWarningWithoutAnException_DoesNotRunTheTeam`** (AC-6) – Arrange: a `Warning` event with no exception and recording tools. Act: run the pipeline. Assert: the result is null, neither tool executed, and no file was written.
- **`RunAsync_WithAnErrorButNoKustoConfiguration_RecordsThreeQueriesAndNoRows`** (AC-6) – Arrange: an `Error` event with an exception, `ILogQuery` reporting not configured. Act: run the pipeline. Assert: status is `unavailable`, no rows, no findings, three distinct KQL strings recorded, containing the trace id, the exception first line and `Success == false` respectively, and the query runner was never executed.
- **`RunAsync_WithoutSearchConfiguration_ReturnsNoHits`** (AC-6) – Arrange: both tools unconfigured. Act: run the pipeline. Assert: grounding status is `unavailable`, the index is `historical-postmortems`, hits are empty, queries are not empty, and search was never executed.
- **`RunAsync_WithOverlappingAndUnrelatedHits_KeepsFiveAndDropsTheUnrelatedService`** (AC-6) – Arrange: a configured search returning six overlapping hits and one unrelated billing hit. Act: run the pipeline on a `Critical` timeout event. Assert: status is `ok`, exactly five hits are kept, `unrelated-billing` is not among them, and search ran once.
- **`Render_WhenNoResourceIsNamed_WritesSevenSectionsWithoutABicepFence`** (AC-6) – Arrange: handoffs from an unconfigured run. Act: render the draft from the Domain renderer. Assert: the seven headings appear in order, there is no ` ```bicep ` fence, and the text contains "No finding names a resource change." This test no longer reads a file; the file-writing half of it is covered by `WriteAsync_ForADraft_NamesTheFileFromTheEventDayAndId`.
- **`ExecuteAsync_WithKqlThatContainsADropCommand_DoesNotExecuteTheQuery`** (AC-6) – Arrange: a configured recording `ILogQuery` and the KQL string `StormEvents | .drop table StormEvents`. Act: execute it through the Application evidence use case. Assert: the call is not executed, the runner ran zero times, and no rows come back.

## Architecture tests

New project `tests/DevOpsCopilot.Architecture.Tests`, referencing all four layer projects, asserting over `Assembly.GetReferencedAssemblies()` and type namespaces. No new NuGet package (decision D-12).

- **`Domain_WhenBuilt_ReferencesNoExternalFramework`** (AC-2) – Assert: no referenced assembly name starts with `Azure.`, `Kusto.`, `Microsoft.Azure.`, `Microsoft.Agents.`, `Microsoft.Extensions.` or `Newtonsoft.`, and none is `System.Text.Json`.
- **`Application_WhenBuilt_ReferencesNoAzureSdkOrAgentFramework`** (AC-3) – Assert: no referenced assembly name starts with `Azure.`, `Kusto.`, `Microsoft.Azure.` or `Microsoft.Agents.`; `DevOpsCopilot.Domain` is referenced.
- **`Domain_WhenBuilt_DoesNotReferenceApplicationOrInfrastructure`** (AC-4) – Assert: neither `DevOpsCopilot.Application` nor `DevOpsCopilot.Infrastructure` appears in Domain's references.
- **`Infrastructure_WhenBuilt_DoesNotReferenceTheHost`** (AC-4) – Assert: `DevOpsCopilot.Worker` does not appear in Infrastructure's references.
- **`LayerProjects_WhenInspected_TargetNet10AndReferenceOnlyInwards`** (AC-1) – Arrange: read the four `.csproj` files from disk. Assert: each has `<TargetFramework>net10.0</TargetFramework>`, and the set of `ProjectReference` paths in each matches exactly the allowed inward set for its layer.
- **`InwardTestProjects_WhenInspected_ReferenceNoInfrastructureOrAzurePackages`** (AC-5) – Assert: `tests/DevOpsCopilot.Application.Tests.csproj` and `tests/DevOpsCopilot.Domain.Tests.csproj` contain no `ProjectReference` to Infrastructure or Worker. This is what stops a later feature from quietly testing Application through an Azure client.
- **`Solution_AfterTheDaemonRemoval_ContainsNoDaemonProject`** (AC-8) – Assert: `src/DevOpsCopilot.sln` contains no `DevOpsCopilot.Daemon` entry and the directories `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests` do not exist.

The `.csproj`- and `.sln`-reading tests locate the repository root by walking up from `AppContext.BaseDirectory` to the directory containing `AGENTS.md`.

## Manual checks

- **Build and test:** `dotnet build src/DevOpsCopilot.sln` and `dotnet test src/DevOpsCopilot.sln` are clean, with the same number of behaviour tests as before plus the two new ones and the architecture tests.
- **AppHost (AC-8):** `aspire run` starts the emulators and a resource named `worker`; the worker connects to hub `logs` with consumer group `devops-copilot` and the `checkpoints` container is created.
- **End-to-end smoke:** with the stack running, the `LogForwarder` emits an error log; a draft appears in the drafts directory with the same shape as before the split.
- **Violation check (AC-9):** temporarily add a `ProjectReference` from Application to Infrastructure, confirm `Application_WhenBuilt_ReferencesNoAzureSdkOrAgentFramework` and `LayerProjects_WhenInspected_TargetNet10AndReferenceOnlyInwards` fail, then revert. Record the observed failure in Results.
- **User secrets:** confirm the Worker project keeps the `UserSecretsId` `dotnet-DevOpsCopilot.Daemon-f6bcefe1-0b1e-448b-a7a0-18f3ac74fa34`, so existing local secrets still resolve.
- **Documentation (AC-10):** read `AGENTS.md § Layout`, `tech-stack.md § Solution structure` and `§ Testing`, `roadmap.md` M3, and `pipeline.md § 1` and confirm none still describes the worker as one project.

---

## Results

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | `dotnet build src/DevOpsCopilot.sln`; `LayerProjects_WhenInspected_TargetNet10AndReferenceOnlyInwards` | Pass | Solution builds with 0 errors. The 9 warnings are pre-existing NU1902/NU1903 advisories on the sample projects and one Aspire ASPIRE010, none from the new projects. |
| AC-2 | `Domain_WhenBuilt_ReferencesNoExternalFramework` | Pass | Domain has zero `PackageReference`. |
| AC-3 | `Application_WhenBuilt_ReferencesNoAzureSdkOrAgentFramework` | Pass | Application's only package is `Microsoft.Extensions.Logging.Abstractions`. |
| AC-4 | `Domain_WhenBuilt_DoesNotReferenceApplicationOrInfrastructure`; `Infrastructure_WhenBuilt_DoesNotReferenceTheHost` | Pass | |
| AC-5 | `RunAsync_WithAnIncidentStartingEvent_RunsEvidenceThenGroundingThenReport`; `InwardTestProjects_WhenInspected_ReferenceNoInfrastructureOrAzurePackages` | Pass | `RunIncidentPipeline` is constructed from three interfaces with no container and no Infrastructure type. |
| AC-6 | The eight moved tests plus `WriteAsync_ForADraft_NamesTheFileFromTheEventDayAndId` | Pass | All eight ran green against the Domain and Application types before `DevOpsCopilot.Daemon` was deleted, which is what proves the move kept behaviour. |
| AC-7 | `RunAsync_AfterARestart_ResumesFromTheLastHandledCheckpoint` | Pass | Report written before every checkpoint; second run resumes at the next event. |
| AC-8 | `Solution_AfterTheDaemonRemoval_ContainsNoDaemonProject`; `AppHost_WhenComposed_RunsTheWorkerWithItsEventHubAndCheckpointReferences` | Pass (automated); **manual half not run** | Both directories and all solution entries are gone, and the AppHost registers `Projects.DevOpsCopilot_Worker` with the `eventhubs` and `checkpoints` references. `aspire run` was **not** executed — see Environment below. |
| AC-9 | Deliberate violation | Pass | An `Azure.Search.Documents` reference added to Application failed `Application_WhenBuilt_ReferencesNoAzureSdkOrAgentFramework` with `Assert.DoesNotContain() Failure: Filter matched in collection`; 1 failed / 7 passed. Reverted, back to 8/8. |
| AC-10 | Documentation review | Pass | `AGENTS.md § Layout` now lists four projects, `tech-stack.md § Solution structure` describes the built state with per-layer packages, `§ Testing` marks the architecture tests built, `pipeline.md § 1` names `src/DevOpsCopilot.Worker`, and roadmap M3, M3.1–M3.3 are marked done with the layer-split gap row removed. No file still describes the worker as one project. |

- **`dotnet build src/DevOpsCopilot.sln`:** succeeded, 0 errors.
- **`dotnet test`:** 23 / 23 passed across the projects this feature owns and the untouched `LogForwarder` — Domain 1, Application 7, Infrastructure 2, Architecture 8, LogForwarder 5.
- **Known unrelated failure:** `DevOpsCopilot.ApiService.Tests` is 2 / 3, failing with `Timed out waiting for the entry point to build the IHost after 00:05:00`. This is the leftover Aspire sample (roadmap M18); it needs the Azurite emulator and there is no Docker daemon in this environment. This branch changes no file under `src/DevOpsCopilot.ApiService`, `src/DevOpsCopilot.Server`, `src/DevOpsCopilot.ServiceDefaults` or `tests/DevOpsCopilot.ApiService.Tests`.
- **Evaluation run:** Not applicable — this feature changes no LLM behaviour.
- **Date:** 2026-10-25

### Environment and what was not verified

The work was done in a Linux worktree with no .NET SDK installed; builds and tests ran against the Windows SDK (`11.0.100-rc.2`) over the WSL mount, targeting `net10.0`. Two manual checks in this document could **not** be run here and are still outstanding:

- **`aspire run` (AC-8, end-to-end smoke).** There is no Docker daemon, so the Event Hubs and Azurite emulators cannot start. The automated test proves the AppHost wiring is correct, but nobody has yet watched the `worker` resource start, create the checkpoint container and write a draft from a `LogForwarder` event. **Run this before merging.**
- **User secrets.** `DevOpsCopilot.Worker.csproj` carries the Daemon's `UserSecretsId` verbatim (`dotnet-DevOpsCopilot.Daemon-f6bcefe1-0b1e-448b-a7a0-18f3ac74fa34`), which was verified by reading the file, not by resolving a secret at runtime.

### Deviations from the plan

- **Package versions.** Application and Infrastructure take `Microsoft.Extensions.*.Abstractions` at 10.0.12, not 10.0.8: the Agent Framework and `Microsoft.Extensions.Hosting` pull 10.0.12 transitively and NuGet treats the downgrade as an error. Recorded in `tech-stack.md`.
- **Application takes one package, not two.** `Microsoft.Extensions.DependencyInjection.Abstractions` turned out to be unnecessary there, because the host registers the use cases directly. Infrastructure keeps it for `AddInfrastructure`.
- **`EventHubSettings` added.** `EventHubLogConsumer` took `IConfiguration` directly. Decision D-5 only named three settings records; a fourth was needed to keep `IConfiguration` out of Infrastructure entirely.
- **Plan step 4.6 skipped.** The Daemon was never pointed at `RunIncidentPipeline`; it kept its own workflow graph until it was deleted in group 7. By then its eight tests were duplicated by the Application, Domain and Infrastructure tests, so shimming it would have been throwaway work. The Daemon and its tests stayed green up to the moment of deletion, which is what the step existed to guarantee.
- **AC-9 demonstrated with a package, not a project reference.** Adding an Application → Infrastructure `ProjectReference` is a circular dependency that MSBuild rejects during restore, so no test ever runs. A forbidden `Azure.Search.Documents` package reference compiles cleanly and is caught by the architecture test, which is the more meaningful demonstration.
- **One extra architecture test.** `AppHost_WhenComposed_RunsTheWorkerWithItsEventHubAndCheckpointReferences` was added so AC-8's AppHost wiring is covered automatically rather than only by the manual `aspire run` check.
