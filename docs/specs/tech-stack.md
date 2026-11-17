# Tech stack

The technologies, versions and services DevOpsCopilot uses. Do not introduce anything outside this list without updating this file in the same pull request. Versions are those in use as of 2026-10-24; update them here whenever they change in the project. Exact library versions are listed in [Library versions](#library-versions) at the end.

## Runtime and language

| Item | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 | Every .NET project uses .NET 10, with null-safety checks switched on. |
| Language | C# | Match the style of the project you edit. |
| Host model | A long-running background worker inside a small ASP.NET Core web host | The product is a worker; the web host exists only for the health check and the status information the dashboard reads (roadmap M1). |
| Packaging | Containers | Built from the .NET Aspire setup for deployment. |

## Solution structure

Strict Clean Architecture in four layers, where each layer may depend only on the layers inside it. Built by [roadmap.md](roadmap.md) M3.

| Layer | What it holds | May depend on | Libraries |
|---|---|---|---|
| Domain | The business concepts and rules | Nothing | None |
| Application | The use cases, the stage order, and descriptions of what each outside service must do | Domain | Logging abstractions only |
| Infrastructure | The connections to outside services: Azure, log queries, search and the AI agents | Application, Domain | The Azure, query and agent libraries below, plus the standard .NET wiring and health-check abstractions |
| Host | Start-up and wiring only | All of the above, plus the shared service defaults | None beyond ASP.NET Core |

The rules for each layer are in [AGENTS.md](../../AGENTS.md#architecture), and automated architecture checks fail the build when a layer breaks them.

## Local orchestration

| Item | Choice |
|---|---|
| Orchestration | .NET Aspire, which starts the whole product locally with one command |
| Event stream | Azure Event Hubs, run as a local emulator, with one hub for logs and one consumer group for the worker |
| Storage | Azure Storage, run locally with the Azurite emulator, holding the worker's progress checkpoints |
| Shared defaults | Health checks, service discovery and OpenTelemetry, shared by every service |

## Ingestion

| Item | Choice |
|---|---|
| Stream | Azure Event Hubs |
| Checkpoints | Azure Blob Storage |
| Production sources | Azure Monitor diagnostic settings (platform logs) and Application Insights diagnostic settings (application logs, exceptions and traces). Nothing to install; configured in Azure. |
| Local test producer | A log forwarder that sends application logs to the event stream through OpenTelemetry |

The log forwarder exists because Azure Monitor cannot send logs to the local emulator. It is not how the product receives logs in production.

## Agents and AI

| Item | Choice | Status |
|---|---|---|
| Agent framework | Microsoft Agent Framework | Built |
| AI abstraction | Microsoft.Extensions.AI, so the AI model can be swapped without changing the agents | Built |
| AI model | Azure OpenAI | Built (roadmap M5). The diagnosis stage asks the configured model for a structured diagnosis. Without Azure OpenAI configured, the stage uses simple rules instead and the report says the model was unavailable. The model family is chosen per environment and is not fixed here. |
| Quality check | Test incidents with known causes, run automatically | Not yet built. See roadmap M8. |

The three agents (evidence, grounding and diagnosis) are built with the Agent Framework in the Infrastructure layer. The order of the stages is controlled by the Application layer, which does not use the Agent Framework, so the stages can be tested without it.

## Evidence and knowledge

| Item | Choice |
|---|---|
| Live evidence | Azure Monitor Log Analytics, queried with read-only KQL |
| Knowledge search | Azure AI Search, one index of past incidents, combining keyword and meaning-based search |

When Log Analytics or Azure AI Search is not configured, the stage reports it as unavailable and returns no results. Query limits and allowed questions are in [the pipeline contract](../pipeline.md).

## Report channels

| Channel | Content | Status |
|---|---|---|
| Microsoft Teams | A card summarising the report | Planned (roadmap M6) |
| Slack | A message summarising the report | Planned (Phase 2) |
| Azure DevOps work item | The full report | Planned (Phase 2) |
| Local file | The report saved on disk when no channel is configured | Built. Saved reports are never committed. |

## State

| Item | Choice | Status |
|---|---|---|
| Triage and grouping state | Shared storage, so the product behaves the same with more than one copy of the worker | Planned (Phase 2). Not yet chosen; Azure Table Storage and Cosmos DB are the candidates. Decided in roadmap M9 and recorded here. |
| Incident storage | Incidents are saved in the shared storage | Planned (Phase 2) |

## Dashboard

| Item | Choice |
|---|---|
| Front end | React with Vite and TypeScript, checked with ESLint |
| Back end for the front end | ASP.NET Core, which also serves the built front end |
| Component tests | Vitest with jsdom and React Testing Library |
| Node | Node 22, required by the build pipeline |

The dashboard shows the worker's status. Its back end collects the status from the worker and the health of the supporting services and gives the front end one summary. How often the dashboard refreshes is fixed in the front end, not configurable.

## Telemetry

| Item | Choice |
|---|---|
| Instrumentation | OpenTelemetry, through the shared service defaults |
| Export | OpenTelemetry export only when an export destination is configured; Application Insights and Azure Monitor in Azure |

## Testing

| Item | Choice |
|---|---|
| Framework | xUnit, with all test projects grouped together |
| Unit tests | Domain and Application: plain tests with no Azure services, network or emulator |
| Integration tests | Infrastructure, against the Event Hubs and storage emulators or test resources. The Azure OpenAI test runs only when a developer has configured Azure OpenAI on their own machine; it never runs in the build pipeline. |
| Host tests | The worker's statistics and status information, and the dashboard back end's status summary, each tested on an in-process test server |
| End-to-end tests | The whole local environment started by .NET Aspire's testing support, with test events sent to the stream. These need Docker for the emulators and are skipped when Docker is not running. |
| Architecture tests | Checks over the built code and project setup that fail the build when a layer depends on something it must not |
| Quality check | Test incidents for diagnosis quality (roadmap M8) |

## Build, CI and hosting

| Item | Choice | Status |
|---|---|---|
| CI | GitHub Actions: build, tests (except those needing Docker), and the dashboard's lint, tests and build, on every pull request and every change to the main branch | Built |
| Deployment | GitHub Actions, deploying the product after the main branch passes its checks | Planned (roadmap M7) |
| Hosting | Azure Container Apps | Planned (roadmap M7) |
| Secrets | Kept in .NET Aspire settings or private developer settings locally, and in GitHub and Azure settings for automation. Never in the repository. | Rule |

## Leftover projects

The sample API, the Azure Functions sample and the email notifier sample came with the starter template. Do not extend them. They are removed by roadmap M18 once nothing depends on them.

The dashboard and its back end are no longer samples: M1 replaced the weather screen with the product's status dashboard, so they are developed like any other part of the product.

## Not used

Listed so nobody adds them by habit: event sourcing, CQRS, separate bounded-context projects, MediatR, AutoMapper, and Semantic Kernel (replaced by the Agent Framework).

## Library versions

The exact versions in use, for developers updating them.

| Area | Library | Version |
|---|---|---|
| Orchestration | .NET Aspire | 13.6.1 |
| Event stream | Azure.Messaging.EventHubs.Processor | 5.12.2 |
| Event stream | Azure.Messaging.EventHubs | 5.12.2 |
| Checkpoints | Azure.Storage.Blobs | 12.27.0 |
| Local test producer | OpenTelemetry | 1.15.3 |
| Agents | Microsoft.Agents.AI and Microsoft.Agents.AI.Workflows | 1.25.0 |
| AI abstraction | Microsoft.Extensions.AI and Microsoft.Extensions.AI.OpenAI | 10.11.0 |
| AI model | Azure.AI.OpenAI | 2.1.0 |
| Azure sign-in | Azure.Identity | 1.21.0 |
| Live evidence | Microsoft.Azure.Kusto.Data | 14.2.4 |
| Knowledge search | Azure.Search.Documents | 12.0.0 |
| Application layer | Microsoft.Extensions.Logging.Abstractions | 10.0.12 |
| Infrastructure layer | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 |
| Infrastructure layer | Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 10.0.12 |
| Host tests | Microsoft.AspNetCore.TestHost | 10.0.12 |
| End-to-end tests | Aspire.Hosting.Testing | 13.6.1 |
| Dashboard | React 19.2, Vite 8, TypeScript 5.9, ESLint 9 | |
| Dashboard tests | Vitest 5.0, jsdom 30.1, React Testing Library 16.3, jest-dom 7.0 | |
