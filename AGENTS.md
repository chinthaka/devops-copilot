# DevOps Copilot

This repo is the DevOpsCopilot daemon. It consumes exception and error events from Azure Event Hubs, reads W3C trace context with OpenTelemetry, and runs a Semantic Kernel team: a diagnostic agent (Kusto / KQL), a RAG grounding agent (Azure AI Search over historical post-mortems), and a remediation agent (Slack, Teams, and Bicep draft).

Handoff shapes and tool rules live in [docs/pipeline.md](docs/pipeline.md). Read that file before changing a schema, a tool contract, or a draft section. Do not restate those rules here or in skills.

## How to work

- Create a new feature branch from `main` before starting any new feature. Do not commit feature work directly to `main`.
- For a full pass over an event, follow `.agents/skills/incident-response/SKILL.md`.
- For one stage, follow the matching skill: `watch-event-hubs`, `diagnostic-agent`, `rag-grounding`, `remediation-agent`.
- Use only evidence listed in `docs/pipeline.md`. Do not invent Kusto rows, search hits, or log lines.
- The draft is the output. Do not post to Slack or Teams, and do not deploy Bicep, unless the user asks.

## Layout

Solution: `src/DevOpsCopilot.sln`. Orchestration: `src/DevOpsCopilot.AppHost/AppHost.cs`.

| Path | Role |
|---|---|
| `src/DevOpsCopilot.AppHost` | Aspire composition root. Local Event Hubs emulator, hub `logs`. |
| `src/DevOpsCopilot.LogForwarder` | OpenTelemetry exporter that JSON-encodes logs for Event Hubs. Not referenced by the AppHost yet. |
| `src/DevOpsCopilot.ServiceDefaults` | Health checks, service discovery, OpenTelemetry. OTLP export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. |
| `src/DevOpsCopilot.Daemon` | .NET 11 worker. Event Hubs consumer and Semantic Kernel team. Add it when implementing the daemon. |
| `incidents/` | Remediation drafts. Do not commit them unless the user asks. |

The weather frontend (`src/frontend`, `src/DevOpsCopilot.Server`) and the sample projects `src/DevOpsCopilot.ApiService`, `src/DevOpsCopilot.Functions`, and `src/DevOpsCopilot.EmailNotifier` are leftover. Do not extend them.

## Build

- Target `net11.0` for every project, including the daemon.
- Nullable reference types on. Match the style of the project you edit.
- Run the stack from the AppHost. Event Hubs uses the emulator locally.
- Keep secrets out of the repo. Read Event Hubs, Kusto, and Azure AI Search settings from Aspire references or user secrets.

## Change boundaries

- Wire `EventHubLogExporter` to the hub `logs` before adding a second exporter.
- The daemon is the only consumer. Consumer group: `devops-copilot`. Checkpoint after each handled batch.
- Register the Semantic Kernel team inside the daemon. Diagnostic, then RAG grounding, then remediation. Do not add a fourth agent unless the user asks.
- Kusto calls are read-only KQL. Search calls stay on the `historical-postmortems` index.
