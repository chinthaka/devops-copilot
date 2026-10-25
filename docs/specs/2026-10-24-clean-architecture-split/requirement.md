# Requirement: Clean Architecture split of DevOpsCopilot.Daemon

| | |
|---|---|
| **Branch** | `features/2026-10-24-clean-architecture-split` |
| **Milestone** | M3 – Clean Architecture split ([roadmap](../roadmap.md#m3--clean-architecture-split-m)) |
| **Status** | Draft |
| **Author** | Chinthaka Kumarasiri |
| **Date** | 2026-10-24 |

## Summary

`src/DevOpsCopilot.Daemon` holds the Event Hubs consumer, the JSON parser, the stage orchestration, the three agents, the Kusto and Azure AI Search clients, the report writer and the dependency-injection wiring in one project. This feature splits it into `DevOpsCopilot.Domain`, `DevOpsCopilot.Application`, `DevOpsCopilot.Infrastructure` and `DevOpsCopilot.Worker`, as described in [AGENTS.md § Architecture](../../../AGENTS.md#architecture), and adds architecture tests that fail the build when a layer references something it must not.

This is a structural change. Observable behaviour does not change: the same events produce the same draft files, the same `unavailable` statuses and the same checkpoint ordering.

## Why

Every milestone after this one adds a stage, a channel or a store ([roadmap](../roadmap.md) M4 onwards). While consumption, orchestration and Azure SDK calls share a project, each of those additions keeps the Azure SDKs reachable from the stage logic, so stage order and rules can only be tested with Azure types in scope. The roadmap lists "one project instead of Domain, Application, Infrastructure and Host" as the first known gap between the code and the contract, and names M3 as the item that closes it. Doing the split now is cheap — the project has 31 types; it becomes expensive once triage, correlation and the state store are added.

The split is also the mitigation recorded in the roadmap risk table for Microsoft Agent Framework API churn: agents behind Application interfaces keep the stage order testable without the framework.

## Scope

**In scope**

- Four new projects — `src/DevOpsCopilot.Domain`, `src/DevOpsCopilot.Application`, `src/DevOpsCopilot.Infrastructure`, `src/DevOpsCopilot.Worker` — with inward-only project references, all on `net10.0`, nullable and implicit usings on.
- Moving every type listed in the inventory below into its target layer, splitting the types that currently mix layers (listed under **Decisions**).
- Naming the Application interfaces as `AGENTS.md` and the roadmap name them: `IEventSource`, `ILogQuery`, `IKnowledgeSearch`, `IReportWriter`.
- Moving the stage order out of the Microsoft Agent Framework workflow graph into an Application use case, keeping the three agents in Infrastructure behind Application interfaces.
- `tests/DevOpsCopilot.Architecture.Tests` enforcing the layer rules.
- Moving the existing tests from `tests/DevOpsCopilot.Daemon.Tests` into `tests/DevOpsCopilot.Domain.Tests`, `tests/DevOpsCopilot.Application.Tests` and `tests/DevOpsCopilot.Infrastructure.Tests`, unchanged in what they assert.
- Updating `src/DevOpsCopilot.sln`, `src/DevOpsCopilot.AppHost/AppHost.cs`, `docs/specs/tech-stack.md` and `AGENTS.md § Layout` to the new structure.
- Deleting `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests`.

**Out of scope**

- Any change to what the worker produces: the draft keeps its current seven headings including **Bicep**, and `RemediationAgent` keeps its name. Both are M4's job.
- Keying handoffs by `incidentId` instead of `eventId` (M12, M13).
- Handling an unparseable record without stopping the consumer (M2).
- Wiring an `IChatClient` or Azure OpenAI (M5).
- Triage, correlation, a state store, notifiers, work items (M6, M9 onwards).
- Parsing diagnostic-settings `records` arrays (M10).
- The leftover sample projects and the weather frontend. They are untouched (M18).
- Introducing value objects such as `IncidentId`, `Fingerprint` or `Confidence`. The Domain gets the types that exist today; DDD modelling arrives with M12.

## Current inventory

Every type in `src/DevOpsCopilot.Daemon` today, with its target layer. 31 types across 22 files.

### Namespace `DevOpsCopilot.Daemon`

| Type | File | Kind | External dependencies today | Target layer |
|---|---|---|---|---|
| `Program` (top-level statements) | `Program.cs` | Composition root | `Microsoft.Extensions.Hosting` | Host |
| `Worker` | `Worker.cs` | `BackgroundService` | Hosting | Host |
| `IEventHubLogConsumer` | `IEventHubLogConsumer.cs` | Interface | none | Application, renamed `IEventSource` |
| `EventHubLogConsumer` | `EventHubLogConsumer.cs` | Class | `Azure.Messaging.EventHubs.Processor`, `Azure.Storage.Blobs`, `IConfiguration`, `ILogger` | Infrastructure |
| `EventHubLogMessage` | `EventHubLogMessage.cs` | Record (`byte[]` body + checkpoint callback) | none | Application, reshaped — see D-2 |
| `LogEventParser` | `LogEventParser.cs` | Static class | `System.Text.Json` | Infrastructure — see D-2 |
| `LogEventProcessor` | `LogEventProcessor.cs` | Class | `ILogger` | Application — see D-3 |

### Namespace `DevOpsCopilot.Daemon.Incident`

| Type | File | Kind | External dependencies today | Target layer |
|---|---|---|---|---|
| `LogEvent` | `LogEvent.cs` | Record | none | Domain |
| `KustoCall` | `KustoCall.cs` | Record | none | Domain |
| `SearchHit` | `SearchHit.cs` | Record | none | Domain |
| `DiagnosticHandoff` | `DiagnosticHandoff.cs` | Record | none | Domain |
| `GroundingHandoff` | `GroundingHandoff.cs` | Record | none | Domain |
| `AgentHandoff` | `AgentHandoff.cs` | Record | none | Application — see D-4 |
| `IncidentWorkflowResult` | `IncidentWorkflowResult.cs` | Record | none | Domain |
| `IncidentOptions` | `IncidentOptions.cs` | Options POCO | none | Host — see D-5 |
| `KustoOptions` | `KustoOptions.cs` | Options POCO | none | Host — see D-5 |
| `SearchOptions` | `SearchOptions.cs` | Options POCO | references `PostmortemSearchTool.IndexName` | Host — see D-5 |
| `IKustoQueryRunner` | `IKustoQueryRunner.cs` | Interface | none | Application, renamed `ILogQuery` |
| `IPostmortemSearch` | `IPostmortemSearch.cs` | Interface | none | Application, renamed `IKnowledgeSearch` |
| `ConfiguredKustoQueryRunner` | `ConfiguredKustoQueryRunner.cs` | Class | `Microsoft.Azure.Kusto.Data`, `IOptions` | Infrastructure |
| `ConfiguredPostmortemSearch` | `ConfiguredPostmortemSearch.cs` | Class | `Azure.Search.Documents`, `IOptions` | Infrastructure |
| `KustoQueryTool` | `KustoQueryTool.cs` | Class + statics (`BuildQueries`, `IsControlCommand`, `EventId`, `ParseEventId`, `ExceptionType`) | none | Split: Domain + Application — see D-6 |
| `PostmortemSearchTool` | `PostmortemSearchTool.cs` | Class + statics (`OverlapTerms`, `Overlaps`), `IndexName`, `MaxHits` | none | Split: Domain + Application — see D-7 |
| `DiagnosticAgent` | `DiagnosticAgent.cs` | Class | `Microsoft.Agents.AI`, `Microsoft.Extensions.AI` | Infrastructure — see D-8 |
| `RagGroundingAgent` | `RagGroundingAgent.cs` | Class | `Microsoft.Agents.AI`, `Microsoft.Extensions.AI` | Infrastructure — see D-8 |
| `RemediationAgent` | `RemediationAgent.cs` | Class + static `Draft` | `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, `System.IO` | Split: Domain + Infrastructure — see D-9 |
| `DiagnosticExecutor` | `DiagnosticExecutor.cs` | `internal`, `Executor<,>` | `Microsoft.Agents.AI.Workflows` | Deleted — see D-10 |
| `GroundingExecutor` | `GroundingExecutor.cs` | `internal`, `Executor<,>` | `Microsoft.Agents.AI.Workflows` | Deleted — see D-10 |
| `RemediationExecutor` | `RemediationExecutor.cs` | `internal`, `Executor<,>` | `Microsoft.Agents.AI.Workflows` | Deleted — see D-10 |
| `IncidentWorkflow` | `IncidentWorkflow.cs` | Class + static `ShouldStart` | `Microsoft.Agents.AI.Workflows` | Split: Domain + Application — see D-10 |
| `IncidentServiceCollectionExtensions` | `IncidentServiceCollectionExtensions.cs` | Static class | DI, `IOptions`, `Microsoft.Extensions.AI` | Split: Infrastructure + Host — see D-11 |

### Non-code files

| File | Target |
|---|---|
| `DevOpsCopilot.Daemon.csproj` (`Microsoft.NET.Sdk.Worker`, `UserSecretsId` `dotnet-DevOpsCopilot.Daemon-f6bcefe1-…`) | Becomes `DevOpsCopilot.Worker.csproj`; package references distributed per layer; the same `UserSecretsId` is carried over so existing local secrets keep working |
| `appsettings.json`, `appsettings.Development.json` | Host |
| `Properties/launchSettings.json` | Host |

### Tests to move

| Test | From | To |
|---|---|---|
| `Parse_reads_the_log_fields_and_w3c_trace_context` | `EventHubLogProcessingTests` | `tests/DevOpsCopilot.Infrastructure.Tests` (the parser is an Infrastructure mapper) |
| `Restart_resumes_after_the_last_handled_checkpoint` | `EventHubLogProcessingTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Warning_without_an_exception_does_not_run_the_team` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Error_without_kusto_configuration_records_three_queries_and_no_rows` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Missing_search_configuration_returns_no_hits` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Search_keeps_five_overlapping_hits_and_drops_an_unrelated_service` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Draft_has_seven_sections_and_no_bicep_fence_when_no_resource_is_named` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |
| `Kql_that_contains_drop_is_not_executed` | `IncidentWorkflowTests` | `tests/DevOpsCopilot.Application.Tests` |

## Decisions

These are the calls this spec makes where the current code mixes layers. Each one is settled here so `plan.md` can be mechanical.

**D-1 – The four projects and their references.** `Domain` references nothing. `Application` references `Domain` only. `Infrastructure` references `Application` and `Domain`. `Worker` references all three. Package references follow: the Azure SDKs, Kusto and both Agent Framework packages sit in `Infrastructure`; `Microsoft.Extensions.Hosting` sits in `Worker`; `Application` takes only `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`; `Domain` takes none.

**D-2 – Parsing happens at the Infrastructure boundary.** Today `IEventHubLogConsumer` hands raw `ReadOnlyMemory<byte>` inwards and `LogEventProcessor` calls `LogEventParser`. `AGENTS.md` requires external data shapes to be mapped to Domain or Application types at the Infrastructure boundary. So `IEventSource` in Application delivers an already-parsed `LogEvent`, and `LogEventParser` (and its `System.Text.Json` dependency) moves into Infrastructure, called by `EventHubLogConsumer`. `EventHubLogMessage` becomes `ReceivedEvent(LogEvent Event, Func<CancellationToken, Task> CheckpointAsync)` in Application. Exception behaviour is unchanged: a malformed body still throws out of the handler. M2 changes that.

**D-3 – `LogEventProcessor` becomes an Application use case.** It keeps its two responsibilities — subscribe to `IEventSource`, and for each event run the pipeline then checkpoint, in that order. It is renamed `ProcessLogEvents`. It keeps its `ILogger<T>` dependency; `Microsoft.Extensions.Logging.Abstractions` is not an Azure SDK or an Agent Framework package and is allowed in Application.

**D-4 – `AgentHandoff` is an Application type.** It is not a pipeline shape; it exists only because the workflow graph passes one message between executors. With the graph gone (D-10) it survives as the internal pair the orchestrator carries between stages, and it lives in Application.

**D-5 – Configuration binding stays in the Host; the layers take plain values.** `IncidentOptions`, `KustoOptions` and `SearchOptions` are `IConfiguration` binding targets, so they belong with the composition root. Infrastructure implementations stop taking `IOptions<IncidentOptions>` and take their own narrow settings records (`KustoSettings`, `SearchSettings`, `ReportSettings`) constructed by the Host. `KustoQueryTool` currently reads `options.Kusto.ExceptionsTable` and `DependenciesTable` to build KQL; those two table names become an Application-level `LogQueryTargets` record supplied by the Host, so Application never sees the options tree. `SearchOptions.Index` defaults to `PostmortemSearchTool.IndexName`; the constant moves to Domain and the default now reads from there.

**D-6 – `KustoQueryTool` splits in two.** The pure functions `BuildQueries`, `IsControlCommand`, `EventId`, `ParseEventId` and `ExceptionType` are rules with no dependencies and move to Domain (`KqlQuestions`, `EventIdentity`). The part that calls `ILogQuery`, applies the control-command guard and assembles `DiagnosticHandoff` becomes the Application use case `GatherEvidence`.

**D-7 – `PostmortemSearchTool` splits in two.** `OverlapTerms`, `Overlaps`, `IndexName` and `MaxHits` are contract rules and move to Domain (`PostmortemOverlap`). The part that calls `IKnowledgeSearch`, caps the hits and assembles `GroundingHandoff` becomes the Application use case `GroundInHistory`.

**D-8 – The agents stay in Infrastructure as implementations of the Application interfaces.** `DiagnosticAgent` and `RagGroundingAgent` are today thin wrappers that build an `AIFunction` and an optional `ChatClientAgent` around the tools. They move to Infrastructure unchanged in behaviour, but they now wrap the Application use cases rather than being wrapped by them: Application calls `ILogQuery` and `IKnowledgeSearch`, and the Agent Framework surface (`AIFunctionFactory`, `ChatClientAgent`) is exposed from Infrastructure for M5 to use. No agent currently invokes an LLM — `IChatClient` is always null in the running host — so nothing observable changes.

**D-9 – `RemediationAgent` splits in two.** `Draft` and all its private helpers are pure string building and move to Domain as `IncidentDraft` (name, sections and Bicep behaviour unchanged — M4 rewrites them). The `Directory.CreateDirectory` / `File.WriteAllText` part becomes `IReportWriter` in Application with a `FileReportWriter` in Infrastructure that owns the drafts directory and the `YYYY-MM-DD-<event-id>.md` naming. The optional `ChatClientAgent` stays in Infrastructure with the writer.

**D-10 – The Agent Framework workflow graph is replaced by an Application orchestrator.** `AGENTS.md` states that stage order is orchestrated in Application and that Application references no Agent Framework package. `IncidentWorkflow` uses `WorkflowBuilder`, `InProcessExecution` and three `Executor<,>` subclasses, so it cannot move to Application as it stands. The three executors are pass-throughs with no branching, retry or persistence, so the graph adds no behaviour. It is replaced by an Application use case `RunIncidentPipeline` that calls gather-evidence, then ground, then write-report in sequence and returns `IncidentWorkflowResult?`. `DiagnosticExecutor`, `GroundingExecutor`, `RemediationExecutor` and the `WorkflowBuilder` wiring are deleted. `IncidentWorkflow.ShouldStart` is a domain rule and moves to Domain as `LogEvent.StartsIncident()`. The `Microsoft.Agents.AI.Workflows` package reference stays in Infrastructure, unused by this feature, because M5 wires the chat agents.

**D-11 – DI registration splits.** `IncidentServiceCollectionExtensions` becomes `AddInfrastructure(this IServiceCollection, …)` in Infrastructure, registering the interface implementations, and `Program.cs` in the Worker, which binds configuration, builds the settings records and registers the Application use cases and the `Worker` hosted service.

**D-12 – Architecture tests use reflection, not a new package.** `tests/DevOpsCopilot.Architecture.Tests` asserts over `Assembly.GetReferencedAssemblies()` and over the namespaces of the types each assembly exposes. This avoids adding NetArchTest or similar, which would need a `tech-stack.md` entry, and is enough for the four rules that matter.

**D-13 – The AppHost resource is renamed.** `builder.AddProject<Projects.DevOpsCopilot_Daemon>("daemon")` becomes `builder.AddProject<Projects.DevOpsCopilot_Worker>("worker")`. Its two `WithReference` names, `eventhubs` and `checkpoints`, do not change, so connection-string resolution is unaffected.

**D-14 – Documentation updated in the same pull request.** `docs/specs/tech-stack.md` § Solution structure moves from "current state: one project" to the four projects, and § Testing marks architecture tests as built. `AGENTS.md` § Layout replaces the `src/DevOpsCopilot.Daemon` row with four rows and drops the "Current state: a single project" note. `docs/pipeline.md` § 1 names `src/DevOpsCopilot.Daemon` as the consumer and is updated to `src/DevOpsCopilot.Worker`. `docs/specs/roadmap.md` marks M3 done.

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | Unchanged: one JSON log event from hub `logs` | [pipeline.md § 1 – Receive](../../pipeline.md#1-receive) |
| **Output** | Unchanged: one draft file in the drafts directory, plus the checkpoint | [pipeline.md § 7 – Report and notify](../../pipeline.md#7-report-and-notify) |

## Contract and rules touched

- [pipeline.md § 1 – Receive](../../pipeline.md#1-receive) — the consumer, consumer group and checkpoint-after-handling rule are preserved; the project path named in this section changes (D-14).
- [pipeline.md § Evidence rule](../../pipeline.md#evidence-rule) — the `unavailable` behaviour of both tools is preserved across the move.
- [pipeline.md § 4 – Gather evidence](../../pipeline.md#4-gather-evidence-diagnostic-agent) — the three fixed questions and the control-command guard move layers but do not change.
- [pipeline.md § 5 – Ground](../../pipeline.md#5-ground-grounding-agent) — the index name, five-hit cap and overlap filter move layers but do not change.

No contract shape changes. The only `pipeline.md` edit is the project path in § 1.

## Domain language

| Term | Meaning in this feature |
|---|---|
| event | `LogEvent`, the parsed log record. Moves to Domain. |
| diagnostic handoff | `DiagnosticHandoff`, the output of the evidence stage. Moves to Domain. |
| grounding handoff | `GroundingHandoff`, the output of the grounding stage. Moves to Domain. |
| hit | `SearchHit`. Moves to Domain. |
| finding | The strings in `DiagnosticHandoff.Findings`. Unchanged. |
| report | The draft file. Rendering moves to Domain, writing to Infrastructure behind `IReportWriter`. |

No new terms are introduced, so `pipeline.md` needs no vocabulary change.

## Acceptance criteria

**AC-1** – The four projects exist with inward-only references
- **Given** the solution after the split
- **When** `dotnet build src/DevOpsCopilot.sln` runs
- **Then** `DevOpsCopilot.Domain`, `DevOpsCopilot.Application`, `DevOpsCopilot.Infrastructure` and `DevOpsCopilot.Worker` all build on `net10.0`, and no project reference points outwards

**AC-2** – Domain depends on nothing but the base class library
- **Given** the compiled `DevOpsCopilot.Domain` assembly
- **When** its referenced assemblies are inspected
- **Then** none is an Azure SDK, a Kusto package, a Microsoft Agent Framework package, a logging package, a serialiser or a dependency-injection package

**AC-3** – Application has no Azure SDK or Agent Framework reference
- **Given** the compiled `DevOpsCopilot.Application` assembly
- **When** its referenced assemblies are inspected
- **Then** it references `DevOpsCopilot.Domain` and no assembly whose name starts with `Azure.`, `Kusto.`, `Microsoft.Azure.` or `Microsoft.Agents.`

**AC-4** – Infrastructure does not reference the Host, and Domain does not reference Application
- **Given** the compiled assemblies
- **When** their referenced assemblies are inspected
- **Then** `DevOpsCopilot.Infrastructure` does not reference `DevOpsCopilot.Worker`, and `DevOpsCopilot.Domain` does not reference `DevOpsCopilot.Application` or `DevOpsCopilot.Infrastructure`

**AC-5** – The stage order lives in Application
- **Given** an error event and test doubles for `ILogQuery`, `IKnowledgeSearch` and `IReportWriter`
- **When** the Application pipeline use case runs it
- **Then** evidence is gathered, then grounding, then the report is written, and the use case can be constructed and run without any Infrastructure type

**AC-6** – Behaviour is unchanged
- **Given** the eight existing tests, moved to their new projects with their assertions untouched
- **When** `dotnet test src/DevOpsCopilot.sln` runs
- **Then** all of them pass: parse fields and trace context, restart resumes after the checkpoint, a `Warning` without an exception runs nothing, an unconfigured Kusto records three distinct queries and no rows, an unconfigured search returns no hits, five overlapping hits are kept and the unrelated one dropped, the draft has its seven headings with no `bicep` fence, and a KQL string containing `.drop` is not executed

**AC-7** – Checkpoint ordering is preserved
- **Given** an event source test double that records when the checkpoint callback runs
- **When** the Application use case handles an event
- **Then** the report is written before the checkpoint callback is invoked, and a second run resumes at the next event

**AC-8** – `DevOpsCopilot.Daemon` is gone and the AppHost runs the Worker
- **Given** the solution after the split
- **When** the repository and `AppHost.cs` are inspected
- **Then** `src/DevOpsCopilot.Daemon` and `tests/DevOpsCopilot.Daemon.Tests` no longer exist, neither appears in `src/DevOpsCopilot.sln`, and the AppHost registers `Projects.DevOpsCopilot_Worker` with references to `eventhubs` and `checkpoints`

**AC-9** – A violation fails the build
- **Given** the architecture tests
- **When** a reference that breaks a layer rule is added deliberately
- **Then** at least one architecture test fails

**AC-10** – The documentation matches the structure
- **Given** the pull request
- **When** `AGENTS.md`, `docs/specs/tech-stack.md`, `docs/specs/roadmap.md` and `docs/pipeline.md` are read
- **Then** none describes the worker as a single project, the layout table lists the four projects, architecture tests are marked built, and M3 is marked done

## Non-functional requirements

- The worker keeps its `UserSecretsId`, so developers' existing user secrets resolve without reconfiguration.
- Startup behaviour and the Aspire connection-string names (`eventhubs`, `checkpoints`) are unchanged, so `aspire run` works with no local setup change.
- No new NuGet package is added, so `tech-stack.md` needs no new version row.

## Open questions

- None. The twelve decisions above settle the layer placement of every type in the inventory; D-10 (dropping the Agent Framework workflow graph in favour of an Application orchestrator) is the one with real design content and is flagged for the reviewer's attention.
