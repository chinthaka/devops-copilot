# Plan: Solution restructure into domain folders

Folder names are shared across the layers so a domain can be followed vertically: `Events`, `Kql`, `Grounding`, `Diagnoses`, `Reporting`, `Pipeline`, plus host-specific `Options` and `Status`. `Diagnoses` is plural to avoid a namespace/type clash with the `Diagnosis` record (CA1724). `Kql` follows .NET capitalisation for acronyms. Interfaces go in one flat `Interfaces/` folder per project.

## Target layout

**Domain** — `Events/` LogEvent, EventIdentity · `Kql/` KqlQuestions, KustoCall, LogQueryTargets, DiagnosticHandoff · `Grounding/` SearchHit, PostmortemOverlap, GroundingHandoff · `Diagnoses/` Diagnosis, Confidence, Impact, EvidenceCitation, DiagnosisRules, DiagnosisCitations, CitationValidation · `Reporting/` IncidentReport, IncidentDraft · `Pipeline/` IncidentWorkflowResult

**Application** — `Interfaces/` IEventSource, IProcessingObserver, ILogQuery, IKnowledgeSearch, IDiagnosisModel, IReportWriter · `Events/` ProcessLogEvents, ReceivedEvent, NullProcessingObserver · `Kql/` GatherEvidence · `Grounding/` GroundInHistory · `Diagnoses/` Diagnose, UnconfiguredDiagnosisModel · `Pipeline/` RunIncidentPipeline, AgentHandoff

**Infrastructure** — `Events/` EventHubLogConsumer, LogEventParser, EventHubSettings, EventHubsHealthCheck, CheckpointStorageHealthCheck · `Kql/` KustoLogQuery, KustoSettings · `Grounding/` PostmortemKnowledgeSearch, SearchSettings · `Diagnoses/` AzureOpenAIDiagnosisModel, AzureOpenAIChatClient, AzureOpenAISettings, DiagnosisPrompt · `Reporting/` FileReportWriter, ReportSettings · `Agents/` unchanged · root: InfrastructureServiceCollectionExtensions (project-wide DI composition)

**Worker** — root: Program, Worker · `Options/` IncidentOptions, AzureOpenAIOptions, KustoOptions, SearchOptions · `Status/` WorkerStatistics, WorkerStatisticsSnapshot, WorkerUptime, WorkerStatusEndpoint, WorkerStatusResponse, DependencyHealthResponse

**Server** — root: Program, Extensions · `Status/` WorkerStatusClient, StatusDocument, WorkerStatus, WorkerStatisticsDocument, ServiceHealth, WorkerStatusResponse, WorkerStatisticsResponse, WorkerDependencyResponse

**LogForwarder** — root: EventHubLogExporter, EventHubEventDataSender, LogEventMapper · `Interfaces/` IEventDataSender · LogRecord.cs deleted (dead code)

**Tests** mirror the production folders; shared helpers go in `Common/` (Handoffs, FakeChatClient stays beside the Diagnoses tests it fakes for, Repository/ProjectFile, AppHostFixture/Docker/DockerRequiredFactAttribute, LogRecordBuilder). Test doubles sit in the folder of the domain they stand in for (RecordingLogQuery in `Kql/`, RecordingKnowledgeSearch in `Grounding/`, RecordingDiagnosisModel in `Diagnoses/`, RecordingReportWriter in `Reporting/`).

## Steps

1. `git mv` each file into its folder; rewrite its `namespace` to match.
2. Update `using` directives across all projects (scripted sweep, then compiler-driven fixes).
3. Delete `LogForwarder/LogRecord.cs`.
4. Document the folder rules in AGENTS.md; update any doc path references to moved files.
5. `dotnet build` + full `dotnet test`; frontend untouched.

Known non-changes: `Server/Extensions.cs` is a duplicate of `ServiceDefaults/Extensions.cs` in the `Microsoft.Extensions.Hosting` namespace — left untouched; flagged for a separate cleanup.
