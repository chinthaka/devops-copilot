# DevOps Copilot

This repo is the DevOpsCopilot worker service: an AI assistant that investigates Azure production incidents and reports the likely cause and suggested fixes to on-call engineers. It diagnoses and reports only. It never changes production resources, never generates deployable infrastructure code, and never declares an incident resolved by itself.

The worker consumes error events from Azure Event Hubs and runs eight stages in order: receive, triage, correlate, gather evidence (KQL), ground (Azure AI Search), diagnose, report and notify (Teams, Slack, work item), and learn.

## Read first

| File | What it is |
|---|---|
| [docs/specs/mission.md](docs/specs/mission.md) | What the product is for, who it serves, and what it will not do. |
| [docs/specs/tech-stack.md](docs/specs/tech-stack.md) | The technologies, versions and services the project uses. Do not introduce anything outside it without updating it in the same pull request. |
| [docs/specs/roadmap.md](docs/specs/roadmap.md) | Phases, milestones and scope. |
| [docs/pipeline.md](docs/pipeline.md) | The system contract: stages, handoff shapes, evidence rules and tool rules. Read it before changing any stage, schema, tool contract or report section. |
| `docs/specs/YYYY-MM-DD-feature-name/` | One folder per feature: `requirement.md`, `validation.md` and `plan.md`. |
| [README.md](README.md) | Project overview for people. |

Do not restate `docs/pipeline.md` here or in specs. Link to the relevant section instead.

## How to work

This project follows Spec-Driven Development and uses Test-Driven Development wherever possible.

1. **Branch.** Create a branch from the latest `main` before any change, named `features/YYYY-MM-DD-name-of-the-feature`. Never commit directly to `main`.
2. **Spec first.** Find the feature's folder `docs/specs/YYYY-MM-DD-feature-name/`, using the same date and name as the branch. If it does not exist, create it by copying `docs/specs/_template/`. Check the feature against `mission.md`, `tech-stack.md` and `roadmap.md` first. Write the three files in this order:
   1. `requirement.md` – what the feature must do and why, its inputs and outputs, the `pipeline.md` rules it touches, and numbered acceptance criteria.
   2. `validation.md` – how each acceptance criterion will be proven: the unit tests (written first, TDD), integration tests and evaluation runs, each linked to a criterion number. Leave a **Results** section empty.
   3. `plan.md` – the technical approach, the files and classes affected, any `pipeline.md` contract changes, and ordered implementation steps.

   Stop for review after all three files exist. Do not write code until they are approved.
3. **Test first.** Implement the tests listed in `validation.md` as failing tests, then write the code to make them pass, then refactor with the tests green. Where a test cannot come first (LLM output quality, live Azure services), use the staged-incident evaluation set or an integration test against the emulator, as `validation.md` says. When done, fill in the **Results** section of `validation.md` with what passed.
4. **Stay in scope.** One feature per branch. Change only what the spec covers. If the work needs a different shape or rule, update the spec and `docs/pipeline.md` in the same pull request and say so in the description.
5. **Verify.** Run `dotnet build` and `dotnet test` before every commit. Do not commit with failing tests.
6. **Pull request.** Open a pull request into `main` that links the feature folder and includes the completed **Results** section of `validation.md`. Do not merge it yourself.

### Tests

- Write each test as Arrange, Act, then Assert, in that order.
- Add test projects under the solution folder `tests`.
- Name tests after the behaviour in the acceptance criterion they cover.
- Domain and Application tests are plain unit tests with no Azure SDKs, network or emulator. Infrastructure is tested with integration tests.

## Architecture

### Clean Architecture (strict)

The solution is split into four layers. Dependencies point inwards only. This rule has no exceptions.

| Layer | Project | Contains | May reference |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | Entities, aggregates, value objects, domain events, domain rules | Nothing (no NuGet packages except the base class library) |
| Application | `src/DevOpsCopilot.Application` | Use cases, stage orchestration, interfaces for everything external | Domain |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | Implementations of Application interfaces: Event Hubs, Log Analytics, Azure AI Search, Azure OpenAI, Microsoft Agent Framework, Teams, Slack, Azure DevOps, state store | Application, Domain |
| Host | `src/DevOpsCopilot.Worker` | Composition root: dependency injection, configuration, `BackgroundService` hosting | All of the above, for wiring only |

Rules:

- **Domain** has no reference to Azure SDKs, Microsoft Agent Framework, logging frameworks, serialisers or persistence.
- **Application** defines the interfaces the stages need (for example `IEventSource`, `ILogQuery`, `IKnowledgeSearch`, `IDiagnosisModel`, `INotifier`, `IIncidentRepository`) and orchestrates the stages in the order set by `docs/pipeline.md`. It references no Azure SDK or Agent Framework package.
- **Infrastructure** implements those interfaces. Microsoft Agent Framework lives here: each agent is an implementation of an Application interface, so the stage order and rules stay testable without it.
- **Host** contains no business logic. If code in the Host makes a decision, it belongs in Application or Domain.
- Data shapes from external systems (Event Hubs records, KQL rows, search documents) are mapped to Domain or Application types at the Infrastructure boundary and never leak inwards.
- Layer boundaries are enforced by project references and by architecture tests in `tests/`, which fail the build if a layer references something it must not.

### Domain-Driven Design (pragmatic)

Apply DDD where it makes the model clearer, not as ceremony.

- **Ubiquitous language:** use the terms from `docs/pipeline.md` (event, candidate, fingerprint, incident, evidence, finding, hit, diagnosis, report, confirmed report) in class, method and test names.
- **Aggregate:** `Incident` is the aggregate root. It owns its candidates and lifecycle, and only its own methods may change its status, following the lifecycle in `docs/pipeline.md`.
- **Value objects:** immutable types for identifiers and small concepts, such as `IncidentId`, `TraceId`, `ResourceId`, `Fingerprint` and `Confidence`. Validate on creation; no raw strings for these in Domain or Application.
- **Domain events:** raise events for lifecycle changes, such as `IncidentOpened`, `IncidentReady`, `IncidentReported` and `IncidentResolved`.
- **Repositories:** only for aggregates (`IIncidentRepository`), defined in Application and implemented in Infrastructure.
- Not required: separate bounded-context projects, event sourcing, CQRS, or domain services for logic that fits naturally on an entity.

### SOLID

- **Single responsibility:** one reason to change per class. Each stage, agent, notifier and mapper is its own class.
- **Open/closed:** add a channel, source or cloud by adding a new implementation, not by editing existing ones with `if`/`switch` on provider.
- **Liskov substitution:** every implementation of an interface honours its full contract, including the `unavailable` behaviour in `docs/pipeline.md`.
- **Interface segregation:** small, focused interfaces. No general-purpose "Azure service" interface.
- **Dependency inversion:** Application and Domain depend on abstractions; concrete types are wired only in the Host.

## Evidence and output

- Use only the evidence listed in `docs/pipeline.md`. Never invent KQL rows, search hits, log lines or causes.
- Deliver reports only to channels configured for the environment. In local runs with no channel configured, the report is written to `incidents/`.
- Never generate Bicep, ARM, Terraform or scripts that change resources. Suggested fix steps are written for an engineer to carry out.

## Layout

Solution: `src/DevOpsCopilot.sln`. Orchestration: `src/DevOpsCopilot.AppHost/AppHost.cs`.

| Path | Role |
|---|---|
| `src/DevOpsCopilot.AppHost` | Aspire composition root. Local Event Hubs emulator, hub `logs`. |
| `src/DevOpsCopilot.Daemon` | .NET 10 worker. Consumes `logs` with consumer group `devops-copilot`, checkpoints in Blob Storage, and runs the incident workflow. **Current state:** a single project. It is split into the Domain, Application, Infrastructure and Worker projects described under Architecture by a dedicated feature. New code goes into the target layers. |
| `src/DevOpsCopilot.LogForwarder` | OpenTelemetry exporter that sends application logs to hub `logs`. Used as a **local test producer**, because Azure Monitor and Application Insights cannot stream into the local emulator. It is not the product's ingestion path. |
| `src/DevOpsCopilot.ServiceDefaults` | Health checks, service discovery, OpenTelemetry. OTLP export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. |
| `docs/pipeline.md` | System contract for stages, handoffs and tool rules. |
| `docs/specs/` | `mission.md`, `tech-stack.md`, `roadmap.md`, `_template/` (blank feature files to copy), and one `YYYY-MM-DD-feature-name/` folder per feature containing `requirement.md`, `validation.md` and `plan.md`. |
| `incidents/` | Local report output. Gitignored; do not commit unless the user asks. |
| `tests/` | xUnit projects, grouped in the solution folder `tests`. |

Leftover projects: the weather frontend (`src/frontend`, `src/DevOpsCopilot.Server`) and the samples `src/DevOpsCopilot.ApiService`, `src/DevOpsCopilot.Functions` and `src/DevOpsCopilot.EmailNotifier`. Do not extend them. Remove them only in a feature whose spec says so.

## Build

- Target `net10.0` for every .NET project.
- Nullable reference types on. Match the style of the project you edit.
- Run the stack from the AppHost. Event Hubs uses the emulator locally.
- Keep secrets out of the repo. Read Event Hubs, Log Analytics, Azure AI Search, Azure OpenAI, Teams, Slack and Azure DevOps settings from Aspire references or user secrets.

## Change boundaries

- The worker is the only consumer of hub `logs`. Consumer group: `devops-copilot`. Checkpoint an Event Hubs event only after every record in it has been handled.
- Triage and correlation state lives in the shared state store, never only in memory, so behaviour is the same with more than one replica.
- The stage order is orchestrated in Application. The three agents (diagnostic, then grounding, then diagnosis) are Microsoft Agent Framework implementations in Infrastructure, behind Application interfaces. Do not add a fourth agent unless the user asks.
- Never add a project reference that points outwards (for example Domain → Application, or Application → Infrastructure). If a feature seems to need one, the design is wrong; stop and raise it in the spec.
- KQL is read-only and stays within the allowed questions and query limits in `docs/pipeline.md`.
- Search stays on the `historical-postmortems` index. Only engineer-confirmed reports are indexed.
- Keep `LogForwarder` wired to hub `logs` for local development until a spec replaces it.
