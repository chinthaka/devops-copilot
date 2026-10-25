# Plan: Clean Architecture split of DevOpsCopilot.Daemon

## Approach

The split is done in place, in nine ordered task groups, with the solution building and all tests green at the end of each group. The guiding rule is that `src/DevOpsCopilot.Daemon` stays in the solution and stays building until the last type has left it, so there is never a long broken state.

The order is inside-out — scaffolding, then Domain, then Application, then Infrastructure, then Host — because that is the direction the dependencies point: a layer can only be filled once the layer it depends on exists. The architecture tests are written in group 1, against four empty projects, so every later group is checked by them as it lands.

Two alternatives were rejected. Building the four projects alongside the Daemon and switching the AppHost at the end would double the code for the length of the branch and risk the two copies drifting. Moving files wholesale and fixing the compiler errors afterwards would hide the five types that mix layers (`KustoQueryTool`, `PostmortemSearchTool`, `RemediationAgent`, `IncidentWorkflow`, `IncidentServiceCollectionExtensions`); those splits are the real work and each gets its own step.

The one design change is decision **D-10** in [requirement.md](requirement.md#decisions): the Microsoft Agent Framework workflow graph is replaced by a plain sequential Application use case. `AGENTS.md` requires stage order in Application and forbids an Agent Framework package reference there, and the three executors are pass-throughs with no branching or persistence, so the graph carries no behaviour that would be lost.

## Changes by layer

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | `LogEvent` (gains `StartsIncident()`), `KustoCall`, `SearchHit`, `DiagnosticHandoff`, `GroundingHandoff`, `IncidentWorkflowResult`, `EventIdentity`, `KqlQuestions`, `LogQueryTargets`, `PostmortemOverlap`, `IncidentDraft` | No packages. `PostmortemOverlap` holds `IndexName` and `MaxHits`. |
| Application | `src/DevOpsCopilot.Application` | `IEventSource`, `ILogQuery`, `IKnowledgeSearch`, `IReportWriter`, `ReceivedEvent`, `AgentHandoff`, `GatherEvidence`, `GroundInHistory`, `RunIncidentPipeline`, `ProcessLogEvents` | References Domain. Packages: `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | `EventHubLogConsumer` (implements `IEventSource`), `LogEventParser`, `KustoLogQuery` (was `ConfiguredKustoQueryRunner`), `PostmortemKnowledgeSearch` (was `ConfiguredPostmortemSearch`), `FileReportWriter`, `DiagnosticAgent`, `RagGroundingAgent`, `RemediationAgent`, `KustoSettings`, `SearchSettings`, `ReportSettings`, `InfrastructureServiceCollectionExtensions` | References Application and Domain. Takes every Azure, Kusto and Agent Framework package. |
| Host | `src/DevOpsCopilot.Worker` | `Program`, `Worker`, `IncidentOptions`, `KustoOptions`, `SearchOptions`, `appsettings*.json`, `launchSettings.json` | References all three. `Microsoft.NET.Sdk.Worker`, `Microsoft.Extensions.Hosting`, carries the existing `UserSecretsId`. |

Also changed: `src/DevOpsCopilot.sln`, `src/DevOpsCopilot.AppHost/AppHost.cs`, `AGENTS.md`, `docs/pipeline.md`, `docs/specs/tech-stack.md`, `docs/specs/roadmap.md`. Deleted: `src/DevOpsCopilot.Daemon`, `tests/DevOpsCopilot.Daemon.Tests`.

New test projects: `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Application.Tests`, `tests/DevOpsCopilot.Infrastructure.Tests`, `tests/DevOpsCopilot.Architecture.Tests`, all xUnit on `net10.0`, all nested under the solution folder `tests`.

## Interfaces

| Interface | Responsibility | Implemented by |
|---|---|---|
| `IEventSource` | Deliver parsed `LogEvent`s with a checkpoint callback, until cancelled. Replaces `IEventHubLogConsumer`; the callback now receives a `ReceivedEvent`, not raw bytes. | `EventHubLogConsumer` (Infrastructure) |
| `ILogQuery` | Report whether the log store is configured, and run one read-only KQL string. Replaces `IKustoQueryRunner`; signature unchanged. | `KustoLogQuery` (Infrastructure) |
| `IKnowledgeSearch` | Report whether search is configured, and return hits for one query. Replaces `IPostmortemSearch`; signature unchanged. | `PostmortemKnowledgeSearch` (Infrastructure) |
| `IReportWriter` | Persist one rendered draft for a diagnostic handoff and return where it went. New: extracted from `RemediationAgent.Write`. | `FileReportWriter` (Infrastructure) |

All four honour the `unavailable` behaviour in [pipeline.md § Evidence rule](../../pipeline.md#evidence-rule): when not configured, the caller records the call that would have been made and gets an empty result.

## Contract changes

One path correction only: [pipeline.md § 1 – Receive](../../pipeline.md#1-receive) names `src/DevOpsCopilot.Daemon` as the consumer and becomes `src/DevOpsCopilot.Worker`. No shape, rule or tool contract changes.

## Tech-stack changes

No new package. [tech-stack.md](../tech-stack.md) is edited in this pull request to record structure, not dependencies:

- § Solution structure – delete the "Current state: one project" paragraph; the four-project table becomes the actual state.
- § Testing – architecture tests move from "Planned with the layer split (M3)" to built, noting they are reflection-based with no extra package.
- § Agents and AI – note that the three agents now sit behind the Application interfaces, as that section already anticipates.

## Configuration and secrets

No new setting and no new secret. The existing `Incident` configuration section keeps its shape (`Incident:Kusto:*`, `Incident:Search:*`, `Incident:DraftsDirectory`). What changes is where it is bound: `Program.cs` in the Worker binds it to `IncidentOptions` and projects it into the narrow settings records each Infrastructure type takes (`KustoSettings`, `SearchSettings`, `ReportSettings`) and into the Domain `LogQueryTargets` the Application evidence use case needs for the two table names (decision D-5).

The Worker project keeps `UserSecretsId` `dotnet-DevOpsCopilot.Daemon-f6bcefe1-0b1e-448b-a7a0-18f3ac74fa34` so existing local secrets keep resolving. The Aspire connection-string names `eventhubs` and `checkpoints` do not change.

## Implementation steps

Nine task groups. Each ends with `dotnet build` and `dotnet test` clean, and is a reviewable commit.

### 1. Scaffolding and architecture tests

1.1 Create `src/DevOpsCopilot.Domain`, `src/DevOpsCopilot.Application`, `src/DevOpsCopilot.Infrastructure` and `src/DevOpsCopilot.Worker`, all `net10.0` with nullable and implicit usings on. Domain and Application are `Microsoft.NET.Sdk`; Infrastructure is `Microsoft.NET.Sdk`; Worker is `Microsoft.NET.Sdk.Worker`.
1.2 Add the inward-only project references and the package references listed under **Changes by layer**.
1.3 Add all four to `src/DevOpsCopilot.sln`, leaving `DevOpsCopilot.Daemon` in place for now.
1.4 Create `tests/DevOpsCopilot.Architecture.Tests` referencing all four, with the repository-root helper that walks up to the directory containing `AGENTS.md`.
1.5 Write `Domain_references_no_external_framework` (AC-2), `Application_references_no_azure_sdk_or_agent_framework` (AC-3), `Domain_does_not_reference_application_or_infrastructure` and `Infrastructure_does_not_reference_the_host` (AC-4), and `Every_layer_project_targets_net10_and_references_only_inwards` (AC-1). They pass against the empty projects.
1.6 Create the three empty test projects `tests/DevOpsCopilot.Domain.Tests`, `.Application.Tests` and `.Infrastructure.Tests`, add them to the solution folder `tests`, then write `Application_tests_reference_no_infrastructure_project` (AC-5) and watch it pass.

### 2. Domain – shapes and identity

2.1 Move `LogEvent`, `KustoCall`, `SearchHit`, `DiagnosticHandoff`, `GroundingHandoff` and `IncidentWorkflowResult` into `DevOpsCopilot.Domain`, namespace `DevOpsCopilot.Domain`. No member changes.
2.2 Move `IncidentWorkflow.ShouldStart` onto `LogEvent` as `StartsIncident()`, keeping the rule exactly: an exception, or level `Error` or `Critical`, case-insensitive.
2.3 Move `KustoQueryTool.EventId` and `ParseEventId` into `EventIdentity` (decision D-6). Formats unchanged: `evt-yyyyMMddHHmmssfff`, parsed with `AssumeUniversal`.
2.4 Add `LogQueryTargets(string ExceptionsTable, string DependenciesTable)` and move `KustoQueryTool.BuildQueries`, `ExceptionType`, `IsControlCommand` and the private `SafeTable`, `Quote` and `KustoDate` helpers into `KqlQuestions`, taking `LogQueryTargets` in place of `IncidentOptions`. The three generated query strings are byte-for-byte what they are today.
2.5 Move `PostmortemSearchTool.IndexName`, `MaxHits`, `OverlapTerms`, `Overlaps` and the `ServiceKeys` list into `PostmortemOverlap` (decision D-7).
2.6 Move `RemediationAgent.Draft` and its private helpers (`Title`, `Slack`, `Teams`, `AppendFindings`, `AppendHits`, `Bicep`, `NamesResourceChange`, `OpenQuestions`, `FirstLine`, `ChangeVerbs`) into `IncidentDraft.Render(DiagnosticHandoff, GroundingHandoff)` (decision D-9). The Bicep section and the resource-change detection are preserved verbatim — M4 removes them, not this feature.
2.7 Move `Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named` (AC-6) into `tests/DevOpsCopilot.Domain.Tests`, rewritten to assert over the string `IncidentDraft.Render` returns rather than a file's contents. The seven headings, their order, the absent `bicep` fence and the "No finding names a resource change." text are asserted unchanged.
2.8 Delete the moved members from the Daemon and have the remaining Daemon types call the Domain ones, so the Daemon still builds and its tests still pass.

### 3. Application – interfaces and the received event

3.1 Add `ILogQuery` and `IKnowledgeSearch` with the signatures of `IKustoQueryRunner` and `IPostmortemSearch`.
3.2 Add `IReportWriter` — given the two handoffs and the rendered draft, persist it and return the path.
3.3 Add `ReceivedEvent(LogEvent Event, Func<CancellationToken, Task> CheckpointAsync)` and `IEventSource.RunAsync(Func<ReceivedEvent, CancellationToken, Task>, CancellationToken)` (decision D-2). Note the shape change from `EventHubLogMessage`: the event arrives parsed, so no payload shape reaches Application.
3.4 Add `AgentHandoff(DiagnosticHandoff, GroundingHandoff)` (decision D-4).
3.5 In the Daemon, make `IKustoQueryRunner`/`IPostmortemSearch` aliases of — or replaced by — the new Application interfaces so the Daemon still compiles against them.

### 4. Application – the stage use cases

4.1 Write `Kql_that_contains_drop_is_not_executed` (AC-6) in `tests/DevOpsCopilot.Application.Tests` against a not-yet-existing `GatherEvidence`, then implement it: build the three questions from `KqlQuestions`, refuse control commands and an unconfigured `ILogQuery` with `KustoCall(kql, false, [])`, take the first row of each executed query, format findings as `Query \`{kql}\` returned {row}.`, and set status `ok` only when both rows and findings are non-empty.
4.2 Write `Error_without_kusto_configuration_records_three_queries_and_no_rows` (AC-6), then confirm `GatherEvidence` satisfies it.
4.3 Write `Missing_search_configuration_returns_no_hits` and `Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service` (AC-6), then implement `GroundInHistory`: build the query from `PostmortemOverlap.OverlapTerms`, return `unavailable` with no hits when `IKnowledgeSearch` is not configured, otherwise filter by overlap, cap at `MaxHits` and set status from whether anything survived.
4.4 Write `Pipeline_runs_evidence_then_grounding_then_report` and `Warning_without_an_exception_does_not_run_the_team` (AC-5, AC-6), then implement `RunIncidentPipeline`: return null when `!logEvent.StartsIncident()`, otherwise gather, ground, render with `IncidentDraft`, write through `IReportWriter`, and return `IncidentWorkflowResult` (decision D-10). Constructed with three interfaces and no DI container.
4.5 Write `Restart_resumes_after_the_last_handled_checkpoint` (AC-7) against `ProcessLogEvents`, then implement it: subscribe to `IEventSource`, log the event at information level with level, category, trace and span as today, run the pipeline, then invoke the checkpoint callback — in that order (decision D-3).
4.6 Delete `DiagnosticExecutor`, `GroundingExecutor`, `RemediationExecutor` and `IncidentWorkflow` from the Daemon, and point the Daemon's `LogEventProcessor` at `RunIncidentPipeline`. The `Microsoft.Agents.AI.Workflows` reference is now unused by the orchestration; it stays, for the Infrastructure agents.

### 5. Infrastructure – adapters

5.1 Move `LogEventParser` into Infrastructure with its `System.Text.Json` dependency, and move `Parse_reads_the_log_fields_and_w3c_trace_context` (AC-6) into `tests/DevOpsCopilot.Infrastructure.Tests` unchanged.
5.2 Move `EventHubLogConsumer` into Infrastructure as the `IEventSource` implementation. It now parses each `args.Data.EventBody` with `LogEventParser` before handing a `ReceivedEvent` inwards (decision D-2). The hub name, consumer group, checkpoint container, Aspire-blob-name stripping and error logging are unchanged.
5.3 Move `ConfiguredKustoQueryRunner` to `KustoLogQuery` implementing `ILogQuery`, taking a `KustoSettings` record instead of `IOptions<IncidentOptions>` (decision D-5). `IsConfigured` keeps its four-field check.
5.4 Move `ConfiguredPostmortemSearch` to `PostmortemKnowledgeSearch` implementing `IKnowledgeSearch`, taking `SearchSettings`. The index name now comes from `PostmortemOverlap.IndexName` in Domain.
5.5 Write `Draft_file_is_named_from_the_event_day_and_id` (AC-6), then implement `FileReportWriter` over `ReportSettings`: create the directory, derive the day from `EventIdentity.ParseEventId`, write `<day>-<eventId>.md`, return the path. Same naming as `RemediationAgent.Write` today.
5.6 Move `DiagnosticAgent`, `RagGroundingAgent` and `RemediationAgent` into Infrastructure (decision D-8). Their `AIFunctionFactory` tools and optional `ChatClientAgent`s are preserved as-is for M5; the two tool-backed agents now delegate to the Application use cases, and `RemediationAgent` delegates to `IncidentDraft` plus `IReportWriter`. No behaviour depends on them while `IChatClient` is null, which is the case in every current code path.
5.7 Add `InfrastructureServiceCollectionExtensions.AddInfrastructure(...)` registering `IEventSource`, `ILogQuery`, `IKnowledgeSearch`, `IReportWriter` and the three agents (decision D-11).

### 6. Host

6.1 Move `Worker` and `Program.cs` into `src/DevOpsCopilot.Worker`, with `appsettings.json`, `appsettings.Development.json` and `Properties/launchSettings.json`.
6.2 Move `IncidentOptions`, `KustoOptions` and `SearchOptions` into the Worker as the configuration binding targets (decision D-5). `SearchOptions.Index` defaults to `PostmortemOverlap.IndexName`.
6.3 In `Program.cs`: bind the `Incident` section, project it into `KustoSettings`, `SearchSettings`, `ReportSettings` and `LogQueryTargets`, call `AddInfrastructure`, register `GatherEvidence`, `GroundInHistory`, `RunIncidentPipeline` and `ProcessLogEvents`, and add the `Worker` hosted service. No decision logic lives here.
6.4 Set the `UserSecretsId` on `DevOpsCopilot.Worker.csproj` to the Daemon's existing value.

### 7. Remove the Daemon

7.1 Confirm `src/DevOpsCopilot.Daemon` holds no type that is not already in a layer project — walk the inventory table in [requirement.md](requirement.md#current-inventory) row by row.
7.2 Delete `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests`, and remove both from `src/DevOpsCopilot.sln` (project entries, both configuration blocks, and the `NestedProjects` line for the test project).
7.3 Write `Daemon_project_no_longer_exists_in_the_solution` (AC-8) and watch it pass.
7.4 Update `AppHost.cs`: `builder.AddProject<Projects.DevOpsCopilot_Worker>("worker")` keeping both `WithReference` names and both `WaitFor` calls (decision D-13).

### 8. Documentation

8.1 `AGENTS.md § Layout` – replace the `src/DevOpsCopilot.Daemon` row with four rows and delete the "Current state: a single project … split by a dedicated feature" sentence.
8.2 `docs/specs/tech-stack.md` – the three edits under **Tech-stack changes** above.
8.3 `docs/pipeline.md § 1` – the consumer path becomes `src/DevOpsCopilot.Worker`.
8.4 `docs/specs/roadmap.md` – mark M3 and its three sub-items done, remove the layer-split row from the known-gaps table, and update "Where the code is now" to describe four projects.

### 9. Verify and close

9.1 `dotnet build src/DevOpsCopilot.sln` and `dotnet test src/DevOpsCopilot.sln`, both clean.
9.2 Run the violation check (AC-9): add an Application → Infrastructure reference, confirm the architecture tests fail, revert, record what failed.
9.3 `aspire run`: the `worker` resource starts, the checkpoint container is created, and a `LogForwarder` error produces a draft matching the pre-split shape (AC-8).
9.4 Fill in the **Results** section of [validation.md](validation.md#results) and open the pull request linking this folder.

## Risks

| Risk | Mitigation |
|---|---|
| Dropping the Agent Framework workflow graph (D-10) loses behaviour someone relied on | The three executors are pass-throughs with no branching, retry or persistence, and the eight existing tests assert the observable behaviour. If a reviewer disagrees, the fallback is to keep `IncidentWorkflow` in Infrastructure as an `IIncidentPipeline` implementation — but that puts the stage order in Infrastructure, against `AGENTS.md`, so it would need a rule change in the same pull request. |
| Parsing moves to Infrastructure (D-2), so a malformed body now throws inside the consumer rather than the processor | Behaviour is the same — it already propagated out of the handler and stopped the run. M2 is the milestone that fixes it; this feature must not quietly change it, so no `try`/`catch` is added. |
| A behaviour change slips in while a type is being split | Each split step moves code verbatim and is guarded by a test moved with unchanged assertions. The KQL strings in particular are asserted character-for-character by `Error_without_kusto_configuration_records_three_queries_and_no_rows`. |
| The architecture tests read `.csproj` and `.sln` files and break when run from an unexpected directory | The repository-root helper walks up to the directory containing `AGENTS.md` and fails with a clear message if it finds none. CI runs from the repository root. |
| Renaming the AppHost resource from `daemon` to `worker` (D-13) breaks a local setting | The two `WithReference` names are unchanged, so no connection string moves. The only visible change is the resource name on the Aspire dashboard. |
| The split and M4's report rewrite collide | M4 touches only `IncidentDraft` and the agent name, both of which this feature leaves semantically untouched. This branch merges first. |
| Test counts drift and a test is lost in the move | The coverage table in [validation.md](validation.md#coverage) lists all eight moved tests by name; group 7.1 checks the inventory row by row before deletion. |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; `dotnet build` and `dotnet test` clean
- [ ] No outward project references; architecture tests pass, and a deliberate violation was shown to fail them
- [ ] `docs/pipeline.md` and `docs/specs/tech-stack.md` updated; `AGENTS.md § Layout` and `docs/specs/roadmap.md` M3 updated
- [ ] `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests` deleted and removed from the solution
- [ ] `aspire run` starts the worker and produces a draft
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
