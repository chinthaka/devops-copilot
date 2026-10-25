# Tech stack

The technologies, versions and services DevOpsCopilot uses. Do not introduce anything outside this list without updating this file in the same pull request. Versions are those pinned in the repo as of 2026-10-24; bump them here when you bump them in a project file.

## Runtime and language

| Item | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (`net10.0`) | Every .NET project targets `net10.0`. Nullable reference types and implicit usings on. |
| Language | C# | Match the style of the project you edit. |
| Host model | .NET Worker Service (`BackgroundService`) | The product is a long-running worker, not a web API. |
| Packaging | Containers | Built from the Aspire AppHost for deployment. |

## Solution structure

Strict Clean Architecture in four projects, with dependencies pointing inwards only. Built by [roadmap.md](roadmap.md) M3.

| Layer | Project | May reference | Packages |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | Nothing beyond the base class library | None |
| Application | `src/DevOpsCopilot.Application` | Domain | `Microsoft.Extensions.Logging.Abstractions` 10.0.12 |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | Application, Domain | The Azure, Kusto and Agent Framework packages below, plus `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12 |
| Host | `src/DevOpsCopilot.Worker` | All of the above, for wiring only | `Microsoft.Extensions.Hosting` 10.0.8 |

The rules for each layer are in [AGENTS.md](../../AGENTS.md#architecture), and the architecture tests fail the build when a layer breaks them.

## Local orchestration

| Item | Choice | Version |
|---|---|---|
| Orchestration | .NET Aspire AppHost (`src/DevOpsCopilot.AppHost`) | Aspire 13.6.1 |
| Event Hubs | Aspire Azure Event Hubs resource `eventhubs`, run as emulator, hub `logs`, consumer group `devops-copilot` | |
| Storage | Aspire Azure Storage resource `storage`, run as Azurite emulator; blob container `checkpoints` | |
| Shared defaults | `src/DevOpsCopilot.ServiceDefaults`: health checks, service discovery, OpenTelemetry | |

## Ingestion

| Item | Choice | Version |
|---|---|---|
| Stream | Azure Event Hubs | `Azure.Messaging.EventHubs.Processor` 5.12.2 |
| Checkpoints | Azure Blob Storage | `Azure.Storage.Blobs` 12.27.0 |
| Production sources | Azure Monitor diagnostic settings (platform logs) and Application Insights diagnostic settings (application logs, exceptions, traces) | No SDK; configured in Azure |
| Local test producer | `src/DevOpsCopilot.LogForwarder`, an OpenTelemetry log exporter that JSON-encodes application logs to hub `logs` | `OpenTelemetry` 1.15.3, `Azure.Messaging.EventHubs` 5.12.2 |

The `LogForwarder` exists because Azure Monitor cannot stream into the local emulator. It is not the product's ingestion path.

## Agents and AI

| Item | Choice | Version |
|---|---|---|
| Agent framework | Microsoft Agent Framework | `Microsoft.Agents.AI` 1.25.0, `Microsoft.Agents.AI.Workflows` 1.25.0 |
| Chat abstraction | `Microsoft.Extensions.AI` (`IChatClient`) | Transitive via Agent Framework |
| LLM | Azure OpenAI | Not yet wired. The agents accept an optional `IChatClient` and run deterministically without one. |
| Evaluation | Staged incidents with known causes, run in CI | Not yet built. See roadmap M8. |

The three agents (diagnostic, grounding, diagnosis) are Agent Framework implementations in Infrastructure behind Application interfaces. Stage order is orchestrated in Application by `RunIncidentPipeline`, which uses no Agent Framework type, so the stages stay testable without one.

## Evidence and knowledge

| Item | Choice | Version |
|---|---|---|
| Live evidence | Azure Monitor Log Analytics, queried with read-only KQL | `Microsoft.Azure.Kusto.Data` 14.2.4 |
| Knowledge search | Azure AI Search, index `historical-postmortems`, hybrid keyword and vector search | `Azure.Search.Documents` 12.0.0 |

When a workspace or search service is not configured, the stage reports `unavailable` and returns no rows or hits. Query limits and allowed questions are in [docs/pipeline.md](../pipeline.md).

## Report channels

| Channel | Content | Status |
|---|---|---|
| Microsoft Teams | Adaptive Card summarising the report | Planned (roadmap M6) |
| Slack | mrkdwn message summarising the report | Planned (Phase 2) |
| Azure DevOps work item | Full report | Planned (Phase 2) |
| Local file | `incidents/YYYY-MM-DD-<id>.md` when no channel is configured | Built. `incidents/` is gitignored. |

## State

| Item | Choice | Status |
|---|---|---|
| Triage and correlation state | Shared state store, so behaviour is identical across replicas | Planned (Phase 2). Store not yet chosen; Azure Table Storage or Cosmos DB are the candidates. Decide in the triage spec and record it here. |
| Incident repository | `IIncidentRepository` in Application, implemented in Infrastructure | Planned (Phase 2) |

## Dashboard

| Item | Choice | Version |
|---|---|---|
| Frontend | React with Vite and TypeScript, in `src/DevOpsCopilot.Frontend` | React 19.2, Vite, TypeScript 5.9, ESLint 9 |
| Backend for frontend | `src/DevOpsCopilot.Server` (ASP.NET Core), publishes the built frontend into `wwwroot` | |
| Node | Node 22 | Required by CI |

Currently the frontend is the Aspire weather sample. Roadmap M1 replaces it with the status dashboard.

## Telemetry

| Item | Choice |
|---|---|
| Instrumentation | OpenTelemetry via `ServiceDefaults` |
| Export | OTLP, only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; Application Insights and Azure Monitor in Azure |

## Testing

| Item | Choice |
|---|---|
| Framework | xUnit, projects under `tests/` in the solution folder `tests` |
| Unit tests | Domain and Application: plain tests, no Azure SDK, network or emulator |
| Integration tests | Infrastructure against the Event Hubs and Azurite emulators or test resources |
| Architecture tests | `tests/DevOpsCopilot.Architecture.Tests`. Reflection over the built assemblies and the project files, so no extra package. Fail the build when a layer references something it must not. |
| Evaluation | Staged-incident set for diagnosis quality (M8) |

## Build, CI and hosting

| Item | Choice | Status |
|---|---|---|
| CI | GitHub Actions `.github/workflows/ci.yml`: Release build of the solution, tests, frontend lint and build, on pull requests and pushes to `main` | Built |
| Deployment | GitHub Actions workflow deploying the AppHost resources on `main` after CI passes | Planned (M7) |
| Hosting | Azure Container Apps | Planned (M7) |
| Secrets | Aspire references, user secrets locally; GitHub and Azure configuration in CI. Never in the repo. | Rule |

## Leftover projects

`src/DevOpsCopilot.ApiService`, `src/DevOpsCopilot.Functions` and `src/DevOpsCopilot.EmailNotifier` are samples from the template, and `src/DevOpsCopilot.Server` plus `src/DevOpsCopilot.Frontend` are the weather sample. Do not extend the samples. They are removed by a dedicated roadmap item once nothing in the AppHost depends on them.

## Not used

Listed so nobody adds them by habit: event sourcing, CQRS, separate bounded-context projects, MediatR, AutoMapper, Semantic Kernel (superseded by Agent Framework), any infrastructure-as-code generator in the product.
