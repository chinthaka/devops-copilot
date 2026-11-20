# Tech stack

The technologies, versions and services DevOpsCopilot uses. Do not introduce anything outside this list without updating this file in the same pull request. Versions are those pinned in the repo as of 2026-11-20; bump them here when you bump them in a project file. [§ NuGet packages](#nuget-packages) lists every package and the projects that use it.

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
| Domain | `src/DevOpsCopilot.Domain` | Nothing beyond the base class library | None |
| Application | `src/DevOpsCopilot.Application` | Domain | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | Application, Domain | The Azure, Kusto, Agent Framework and email packages below, plus `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 and `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` 10.0.14 |
| Host | `src/DevOpsCopilot.Worker` | All of the above plus `ServiceDefaults`, for wiring only | None; ASP.NET Core comes from the web SDK |

The rules for each layer are in [AGENTS.md](../../AGENTS.md#architecture), and automated architecture checks fail the build when a layer breaks them.

## Local orchestration

| Item | Choice | Version |
|---|---|---|
| Orchestration | .NET Aspire AppHost (`src/DevOpsCopilot.AppHost`), a plain `Microsoft.NET.Sdk` project. .NET projects are added by path with `AddDotnetProject`; the Functions sample is the one exception, through `AddAzureFunctionsProject` | Aspire 17.0.0 (`Aspire.Hosting.AppHost`, `Aspire.Hosting.Dotnet` and the `Aspire.Hosting.*` integrations) |
| Aspire CLI | Required to build and run the AppHost: it supplies the orchestrator (DCP) and the dashboard, as the CLI bundle in `~/.aspire/bundle`. With no `aspire` on the `PATH`, the AppHost build runs the paired `Aspire.Cli` through `dnx` (.NET 10 SDK or later) and extracts the bundle itself, so CI needs no separate install. `aspire run --watch` is not yet supported for projects added by path. | Paired with `Aspire.Hosting.AppHost`, so 17.0.0 |
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

The log forwarder exists because Azure Monitor cannot send logs to the local emulator. It is not how the product receives logs in production.

## Agents and AI

| Item | Choice | Status |
|---|---|---|
| Agent framework | Microsoft Agent Framework | `Microsoft.Agents.AI` 1.28.0, `Microsoft.Agents.AI.Workflows` 1.28.0 |
| Chat abstraction | `Microsoft.Extensions.AI` (`IChatClient`) | `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0 |
| LLM | Azure OpenAI | `Azure.AI.OpenAI` 2.1.0, `Azure.Identity` 1.21.0. Built (roadmap M5): the diagnosis stage asks the configured deployment for a structured `Diagnosis`. Without an endpoint and a deployment the stage runs deterministically and the report says the model was unavailable. The deployment's model family is chosen per environment and is not pinned here. |
| Evaluation | Staged incidents with known causes, run in CI | Not yet built. See roadmap M8. |

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
| Framework | xUnit, projects under `tests/` in the solution folder `tests` |
| Unit tests | Domain and Application: plain tests, no Azure SDK, network or emulator |
| Integration tests | Infrastructure against the Event Hubs and Azurite emulators or test resources. The Azure OpenAI test is marked `Category=RequiresAzureOpenAI` and skipped unless `Incident:AzureOpenAI` is in user secrets or the environment; it never runs in CI. The channel tests are gated the same way: `RequiresTeams`, `RequiresSlack`, `RequiresDiscord`, `RequiresEmail` (Communication Services) and `RequiresSmtp`, each skipped unless its `Incident:Notifications:<channel>` settings are set. |
| Local SMTP server | Not prescribed. The `RequiresSmtp` test and the SMTP manual checks run against any SMTP server; each developer chooses their own local test tool. Nothing is added to the AppHost. |
| Host tests | `tests/DevOpsCopilot.Worker.Tests`: the worker's statistics and status endpoint on an in-process test server (`Microsoft.AspNetCore.TestHost` 10.0.14). `tests/DevOpsCopilot.Server.Tests`: the status-document mapping against a stubbed worker HTTP handler. |
| AppHost and end-to-end tests | `tests/DevOpsCopilot.AppHost.Tests` on `Aspire.Hosting.Testing` 17.0.0, producing events with `Azure.Messaging.EventHubs` 5.12.2. Needs Docker for the emulators; marked `Category=RequiresDocker` and skipped when no daemon answers. |
| Architecture tests | `tests/DevOpsCopilot.Architecture.Tests`. Reflection over the built assemblies and the project files, so no extra package. Fail the build when a layer references something it must not. |
| Evaluation | Staged-incident set for diagnosis quality (M8) |

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

## NuGet packages

Every `PackageReference` in the solution as of 2026-11-20, after a full update to the latest
stable versions on nuget.org (`dotnet list package --outdated` reports nothing for any project).
The same list is the source of truth for the versions quoted above. The leftover samples
(`ApiService`, `Functions`, `EmailNotifier`) were updated only for security and currency; they are
still not extended.

| Package | Version | Projects |
|---|---|---|
| `Aspire.Azure.Data.Tables` | 17.0.0 | ApiService |
| `Aspire.Azure.Storage.Queues` | 17.0.0 | ApiService |
| `Aspire.Hosting.AppHost` | 17.0.0 | AppHost |
| `Aspire.Hosting.Azure.EventHubs` | 17.0.0 | AppHost |
| `Aspire.Hosting.Azure.Functions` | 17.0.0 | AppHost |
| `Aspire.Hosting.Azure.Storage` | 17.0.0 | AppHost |
| `Aspire.Hosting.Dotnet` | 17.0.0 | AppHost |
| `Aspire.Hosting.JavaScript` | 17.0.0 | AppHost |
| `Aspire.Hosting.Testing` | 17.0.0 | AppHost.Tests |
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
project, as of 2026-11-20.

That audit does not cover the Aspire dashboard or the orchestrator (DCP). Since the migration to the
Aspire 17 project model they come from the Aspire CLI bundle, not from NuGet packages, so no NuGet
audit sees them. They are kept current by keeping the Aspire CLI at the version paired with
`Aspire.Hosting.AppHost`, which the AppHost build does by default; an `aspire` installed separately
on the `PATH` takes precedence and must be updated with `aspire update --self`.

Two advisories were published against Aspire 13.6.4 after the 2026-11-08 audit was written, and
the build warned about both. The 2026-11-20 upgrade to Aspire 17.0.0 resolved them; both were
first fixed in 13.6.5:

| Advisory | Package | Was | Now | Fixed by |
|---|---|---|---|---|
| [GHSA-4g86-54fm-h9h8](https://github.com/advisories/GHSA-4g86-54fm-h9h8) (critical, NU1904) | `Aspire.Dashboard.Sdk.*`, transitive in `AppHost` and `AppHost.Tests` | 13.6.4 | 17.0.0 | Aspire 13.6.4 → 17.0.0 |
| [GHSA-f7vh-h5g3-7jrh](https://github.com/advisories/GHSA-f7vh-h5g3-7jrh) (high, NU1903) | `Aspire.Hosting` and `Aspire.Hosting.AppHost`, transitive in `AppHost` and `AppHost.Tests` | 13.6.4 | 17.0.0 | Aspire 13.6.4 → 17.0.0 |

Resolved earlier, in the 2026-11-08 update:

| Advisory | Package | Was | Now | Fixed by |
|---|---|---|---|---|
| [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc) (high, NU1903) | `Microsoft.OpenApi`, transitive in `Server`, `ApiService` and their tests | 2.0.0 | 2.12.2 | `Microsoft.AspNetCore.OpenApi` 10.0.8 → 10.0.14 |
| [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (moderate, NU1902) | `MessagePack`, transitive in `AppHost` and `AppHost.Tests` | 2.5.302 | 2.5.307 | Aspire 13.6.1 → 13.6.4 |

Major-version upgrades so far: Aspire 13 → 17 (2026-11-20), and in the 2026-11-08 update
`Microsoft.NET.Test.Sdk` 17 → 18, `coverlet.collector` 6 → 10, `xunit.runner.visualstudio` 3 → 4,
and `Microsoft.ApplicationInsights.WorkerService` 2 → 3 (leftover samples `Functions` and
`EmailNotifier` only).

**Migration to the Aspire 17 project model (2026-11-20).** Aspire 17 made the `Aspire.AppHost.Sdk`
project model obsolete (warning ASPIRE012) and `AddProject<TProject>` with it (warning CS0618).
Following [the migration guide](https://aspire.dev/integrations/dotnet/migrate-dotnet-project-resource/),
the AppHost is now a plain `Microsoft.NET.Sdk` project with explicit `Aspire.Hosting.AppHost` and
`Aspire.Hosting.Dotnet` references, and the server, the worker and the sample API are added with
`AddDotnetProject` and a project path, so the AppHost no longer references them. The Functions
sample keeps `AddAzureFunctionsProject`, which the guide says not to replace, and its reference is
marked `IsAspireProjectResource` so its `Projects.*` type is still generated. A dump of the resource
model before and after showed the same resources, endpoints, health check and publish-mode
environment variables; the only additions are a hidden build step that the projects wait for and
the new resource type's own annotations.

## Not used

Listed so nobody adds them by habit: event sourcing, CQRS, separate bounded-context projects, MediatR, AutoMapper, Semantic Kernel (superseded by Agent Framework), `Microsoft.Graph` (Teams posts through a Workflows webhook, decision D3 of the M6 spec), Slack and Discord SDKs (plain HTTP), `System.Net.Mail.SmtpClient` (Microsoft no longer recommends it for new development; SMTP uses MailKit, decision D15 of the M6 spec), and templating packages such as Scriban, Handlebars.Net or Razor (the placeholder renderer lives in Domain).
