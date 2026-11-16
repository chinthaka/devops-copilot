# Tech stack

The technologies, versions and services DevOpsCopilot uses. Do not introduce anything outside this list without updating this file in the same pull request. Versions are those pinned in the repo as of 2026-11-08; bump them here when you bump them in a project file. [§ NuGet packages](#nuget-packages) lists every package and the projects that use it.

## Runtime and language

| Item | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (`net10.0`) | Every .NET project targets `net10.0`. Nullable reference types and implicit usings on. |
| Language | C# | Match the style of the project you edit. |
| Host model | `BackgroundService` in the ASP.NET Core minimal host | The product is a long-running worker; the web host exists only for `/health` and the status endpoint the dashboard reads (roadmap M1). |
| Packaging | Containers | Built from the Aspire AppHost for deployment. |

## Solution structure

Strict Clean Architecture in four projects, with dependencies pointing inwards only. Built by [roadmap.md](roadmap.md) M3.

| Layer | Project | May reference | Packages |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | Nothing beyond the base class library | None |
| Application | `src/DevOpsCopilot.Application` | Domain | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | Application, Domain | The Azure, Kusto, Agent Framework and email packages below, plus `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 and `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` 10.0.14 |
| Host | `src/DevOpsCopilot.Worker` | All of the above plus `ServiceDefaults`, for wiring only | None; ASP.NET Core comes from the web SDK |

The rules for each layer are in [AGENTS.md](../../AGENTS.md#architecture), and the architecture tests fail the build when a layer breaks them.

## Local orchestration

| Item | Choice | Version |
|---|---|---|
| Orchestration | .NET Aspire AppHost (`src/DevOpsCopilot.AppHost`) | Aspire 13.6.4 (`Aspire.AppHost.Sdk` and the `Aspire.Hosting.*` packages) |
| Event Hubs | Aspire Azure Event Hubs resource `eventhubs`, run as emulator, hub `logs`, consumer group `devops-copilot` | |
| Storage | Aspire Azure Storage resource `storage`, run as Azurite emulator; blob container `checkpoints` | |
| Shared defaults | `src/DevOpsCopilot.ServiceDefaults`: health checks, service discovery, OpenTelemetry | |

## Ingestion

| Item | Choice | Version |
|---|---|---|
| Stream | Azure Event Hubs | `Azure.Messaging.EventHubs.Processor` 5.12.2 |
| Checkpoints | Azure Blob Storage | `Azure.Storage.Blobs` 12.30.1 |
| Production sources | Azure Monitor diagnostic settings (platform logs) and Application Insights diagnostic settings (application logs, exceptions, traces) | No SDK; configured in Azure |
| Local test producer | `src/DevOpsCopilot.LogForwarder`, an OpenTelemetry log exporter that JSON-encodes application logs to hub `logs` | `OpenTelemetry` 1.20.1, `Azure.Messaging.EventHubs` 5.12.2 |

The `LogForwarder` exists because Azure Monitor cannot stream into the local emulator. It is not the product's ingestion path.

## Agents and AI

| Item | Choice | Version |
|---|---|---|
| Agent framework | Microsoft Agent Framework | `Microsoft.Agents.AI` 1.28.0, `Microsoft.Agents.AI.Workflows` 1.28.0 |
| Chat abstraction | `Microsoft.Extensions.AI` (`IChatClient`) | `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0 |
| LLM | Azure OpenAI | `Azure.AI.OpenAI` 2.1.0, `Azure.Identity` 1.21.0. Built (roadmap M5): the diagnosis stage asks the configured deployment for a structured `Diagnosis`. Without an endpoint and a deployment the stage runs deterministically and the report says the model was unavailable. The deployment's model family is chosen per environment and is not pinned here. |
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
| Microsoft Teams | Adaptive Card summarising the report, posted to a Teams Workflows webhook ("Post to a channel when a webhook request is received"). The trigger URL is a credential. | Built (roadmap M6.2) |
| Slack | Block Kit message summarising the report, posted with `chat.postMessage` and a bot token. A `200` carrying `"ok": false` is a failure. | Built (roadmap M6.3, moved forward from Phase 2) |
| Discord | Embed summarising the report, posted to a channel webhook with `?wait=true`. One `Retry-After` retry on `429`. | Built (roadmap M6.4) |
| Email | The report summary, rendered from a customisable email template and sent as one message to every configured address through one transport selected by `Incident:Notifications:Email:Transport`: **Azure Communication Services Email** (`Azure.Communication.Email` 1.1.0; managed identity through `Azure.Identity` when only an endpoint is set), or **SMTP** (`MailKit` 4.18.2, which brings MimeKit; STARTTLS or implicit TLS required unless explicitly set to none for a local test server). Both packages are in Infrastructure only. | Built (Communication Services roadmap M6.5, SMTP roadmap M6.7) |
| Azure DevOps work item | Full report | Planned (Phase 2) |
| Local file | `incidents/YYYY-MM-DD-<id>.md`, always written before any channel is called; the only delivery when no channel is configured | Built. `incidents/` is gitignored. |

Slack and Discord have no first-party .NET SDK, and Teams needs none for a webhook POST, so all
three are plain HTTP over `IHttpClientFactory` (`Microsoft.Extensions.Http` 10.0.14, Infrastructure
only). The report and the email render from templates with embedded defaults; a custom template is
a file named in configuration and re-read on every render, with no templating package. Channel
secrets live in `Incident:Notifications:*` configuration, read only by the Worker.

## State

| Item | Choice | Status |
|---|---|---|
| Triage and correlation state | Shared state store, so behaviour is identical across replicas | Planned (Phase 2). Store not yet chosen; Azure Table Storage or Cosmos DB are the candidates. Decide in the triage spec and record it here. |
| Incident repository | `IIncidentRepository` in Application, implemented in Infrastructure | Planned (Phase 2) |

## Dashboard

| Item | Choice | Version |
|---|---|---|
| Frontend | React with Vite and TypeScript, in `src/DevOpsCopilot.Frontend` | React 19.2, Vite 8, TypeScript 5.9, ESLint 9 |
| Backend for frontend | `src/DevOpsCopilot.Server` (ASP.NET Core), publishes the built frontend into `wwwroot` | |
| Component tests | Vitest with jsdom and React Testing Library | Vitest 5.0, jsdom 30.1, `@testing-library/react` 16.3, `@testing-library/jest-dom` 7.0 |
| Node | Node 22 | Required by CI |

The frontend is the status dashboard: it reads the status document from `/api/status` on
`DevOpsCopilot.Server`, which in turn reads the worker's `/status` over Aspire service discovery.
The polling interval is a constant in the frontend, not configuration.

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
| Integration tests | Infrastructure against the Event Hubs and Azurite emulators or test resources. The Azure OpenAI test is marked `Category=RequiresAzureOpenAI` and skipped unless `Incident:AzureOpenAI` is in user secrets or the environment; it never runs in CI. The channel tests are gated the same way: `RequiresTeams`, `RequiresSlack`, `RequiresDiscord`, `RequiresEmail` (Communication Services) and `RequiresSmtp`, each skipped unless its `Incident:Notifications:<channel>` settings are set. |
| Local SMTP server | Not prescribed. The `RequiresSmtp` test and the SMTP manual checks run against any SMTP server; each developer chooses their own local test tool. Nothing is added to the AppHost. |
| Host tests | `tests/DevOpsCopilot.Worker.Tests`: the worker's statistics and status endpoint on an in-process test server (`Microsoft.AspNetCore.TestHost` 10.0.14). `tests/DevOpsCopilot.Server.Tests`: the status-document mapping against a stubbed worker HTTP handler. |
| AppHost and end-to-end tests | `tests/DevOpsCopilot.AppHost.Tests` on `Aspire.Hosting.Testing` 13.6.4, producing events with `Azure.Messaging.EventHubs` 5.12.2. Needs Docker for the emulators; marked `Category=RequiresDocker` and skipped when no daemon answers. |
| Architecture tests | `tests/DevOpsCopilot.Architecture.Tests`. Reflection over the built assemblies and the project files, so no extra package. Fail the build when a layer references something it must not. |
| Evaluation | Staged-incident set for diagnosis quality (M8) |

## Build, CI and hosting

| Item | Choice | Status |
|---|---|---|
| CI | GitHub Actions `.github/workflows/ci.yml`: Release build of the solution, tests (excluding `Category=RequiresDocker`), frontend lint, Vitest tests and build, on pull requests and pushes to `main` | Built |
| Deployment | GitHub Actions workflow deploying the AppHost resources on `main` after CI passes | Planned (M7) |
| Hosting | Azure Container Apps | Planned (M7) |
| Secrets | Aspire references, user secrets locally; GitHub and Azure configuration in CI. Never in the repo. | Rule |

## Leftover projects

`src/DevOpsCopilot.ApiService`, `src/DevOpsCopilot.Functions` and `src/DevOpsCopilot.EmailNotifier` are samples from the template. Do not extend them. They are removed by a dedicated roadmap item (M18) once nothing in the AppHost depends on them.

`src/DevOpsCopilot.Server` and `src/DevOpsCopilot.Frontend` are no longer samples: M1 replaced the weather screen with the product status dashboard, so they are extended like any other project.

## NuGet packages

Every `PackageReference` in the solution as of 2026-11-08, after a full update to the latest
stable versions on nuget.org (`dotnet list package --outdated` reports nothing for any project).
The same list is the source of truth for the versions quoted above. The leftover samples
(`ApiService`, `Functions`, `EmailNotifier`) were updated only for security and currency; they are
still not extended.

| Package | Version | Projects |
|---|---|---|
| `Aspire.AppHost.Sdk` | 13.6.4 | AppHost |
| `Aspire.Azure.Data.Tables` | 13.6.4 | ApiService |
| `Aspire.Azure.Storage.Queues` | 13.6.4 | ApiService |
| `Aspire.Hosting.Azure.EventHubs` | 13.6.4 | AppHost |
| `Aspire.Hosting.Azure.Functions` | 13.6.4 | AppHost |
| `Aspire.Hosting.Azure.Storage` | 13.6.4 | AppHost |
| `Aspire.Hosting.JavaScript` | 13.6.4 | AppHost |
| `Aspire.Hosting.Testing` | 13.6.4 | AppHost.Tests |
| `Azure.AI.OpenAI` | 2.1.0 | Infrastructure |
| `Azure.Communication.Email` | 1.1.0 | Infrastructure |
| `Azure.Data.Tables` | 12.13.0 | EmailNotifier |
| `Azure.Identity` | 1.21.0 | Infrastructure |
| `Azure.Messaging.EventHubs` | 5.12.2 | AppHost.Tests, LogForwarder |
| `Azure.Messaging.EventHubs.Processor` | 5.12.2 | Infrastructure |
| `Azure.Search.Documents` | 12.0.0 | Infrastructure |
| `Azure.Storage.Blobs` | 12.30.1 | EmailNotifier, Infrastructure |
| `Azure.Storage.Files.Shares` | 12.28.0 | EmailNotifier |
| `Azure.Storage.Queues` | 12.28.1 | EmailNotifier |
| `coverlet.collector` | 10.1.0 | ApiService.Tests, AppHost.Tests, Application.Tests, Architecture.Tests, Domain.Tests, Infrastructure.Tests, LogForwarder.Tests, Server.Tests, Worker.Tests |
| `MailKit` | 4.18.2 | Infrastructure |
| `Microsoft.Agents.AI` | 1.28.0 | Infrastructure |
| `Microsoft.Agents.AI.Workflows` | 1.28.0 | Infrastructure |
| `Microsoft.ApplicationInsights.WorkerService` | 3.2.0 | EmailNotifier, Functions |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.14 | ApiService.Tests |
| `Microsoft.AspNetCore.OpenApi` | 10.0.14 | ApiService, Server |
| `Microsoft.AspNetCore.TestHost` | 10.0.14 | Worker.Tests |
| `Microsoft.Azure.Functions.Worker` | 2.52.0 | EmailNotifier, Functions |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights` | 2.51.0 | EmailNotifier, Functions |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.1.1 | EmailNotifier, Functions |
| `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` | 5.5.5 | EmailNotifier, Functions |
| `Microsoft.Azure.Functions.Worker.Extensions.Tables` | 1.5.0 | Functions |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.1.0 | EmailNotifier, Functions |
| `Microsoft.Azure.Kusto.Data` | 14.2.4 | Infrastructure |
| `Microsoft.Extensions.AI` | 10.11.0 | Infrastructure |
| `Microsoft.Extensions.AI.OpenAI` | 10.11.0 | Infrastructure |
| `Microsoft.Extensions.Azure` | 1.14.1 | EmailNotifier |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.14 | Infrastructure.Tests |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.14 | EmailNotifier, Infrastructure.Tests |
| `Microsoft.Extensions.DependencyInjection` | 10.0.14 | Infrastructure.Tests |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.14 | Infrastructure |
| `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` | 10.0.14 | Infrastructure |
| `Microsoft.Extensions.Http` | 10.0.14 | Infrastructure |
| `Microsoft.Extensions.Http.Resilience` | 10.11.0 | Server, ServiceDefaults |
| `Microsoft.Extensions.Logging` | 10.0.14 | LogForwarder |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.14 | Application |
| `Microsoft.Extensions.ServiceDiscovery` | 10.11.0 | Server, ServiceDefaults |
| `Microsoft.NET.Test.Sdk` | 18.11.0 | ApiService.Tests, AppHost.Tests, Application.Tests, Architecture.Tests, Domain.Tests, Infrastructure.Tests, LogForwarder.Tests, Server.Tests, Worker.Tests |
| `OpenTelemetry` | 1.20.1 | LogForwarder |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.20.1 | Server, ServiceDefaults |
| `OpenTelemetry.Extensions.Hosting` | 1.20.1 | Server, ServiceDefaults |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.20.2 | Server, ServiceDefaults |
| `OpenTelemetry.Instrumentation.Http` | 1.20.2 | Server, ServiceDefaults |
| `OpenTelemetry.Instrumentation.Runtime` | 1.20.1 | Server, ServiceDefaults |
| `SendGrid` | 9.29.3 | Functions |
| `xunit` | 2.9.3 | ApiService.Tests, AppHost.Tests, Application.Tests, Architecture.Tests, Domain.Tests, Infrastructure.Tests, LogForwarder.Tests, Server.Tests, Worker.Tests |
| `xunit.runner.visualstudio` | 4.0.1 | ApiService.Tests, AppHost.Tests, Application.Tests, Architecture.Tests, Domain.Tests, Infrastructure.Tests, LogForwarder.Tests, Server.Tests, Worker.Tests |

Test tooling: xUnit 2.9.3 (the latest of the `xunit` v2 package) runs under
`xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk` 18.11.0, with coverage by
`coverlet.collector` 10.1.0. The same 268 tests are discovered before and after the runner's major
upgrade.

### Package audit

`dotnet list package --vulnerable --include-transitive` reports no vulnerable package in any
project. The 2026-11-08 update resolved the two advisories the build used to warn about:

| Advisory | Package | Was | Now | Fixed by |
|---|---|---|---|---|
| [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc) (high, NU1903) | `Microsoft.OpenApi`, transitive in `Server`, `ApiService` and their tests | 2.0.0 | 2.12.2 | `Microsoft.AspNetCore.OpenApi` 10.0.8 → 10.0.14 |
| [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (moderate, NU1902) | `MessagePack`, transitive in `AppHost` and `AppHost.Tests` | 2.5.302 | 2.5.307 | Aspire 13.6.1 → 13.6.4 |

Major-version upgrades in this update: `Microsoft.NET.Test.Sdk` 17 → 18, `coverlet.collector`
6 → 10, `xunit.runner.visualstudio` 3 → 4, and `Microsoft.ApplicationInsights.WorkerService`
2 → 3 (leftover samples `Functions` and `EmailNotifier` only).

## Not used

Listed so nobody adds them by habit: event sourcing, CQRS, separate bounded-context projects, MediatR, AutoMapper, Semantic Kernel (superseded by Agent Framework), `Microsoft.Graph` (Teams posts through a Workflows webhook, decision D3 of the M6 spec), Slack and Discord SDKs (plain HTTP), `System.Net.Mail.SmtpClient` (Microsoft no longer recommends it for new development; SMTP uses MailKit, decision D15 of the M6 spec), and templating packages such as Scriban, Handlebars.Net or Razor (the placeholder renderer lives in Domain).
