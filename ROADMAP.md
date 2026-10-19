# Roadmap

DevOps Copilot listens to enterprise exception and error logs on Azure Event Hubs, reads the W3C trace context, and runs a Semantic Kernel team. The team diagnoses with Kusto, grounds the case in historical post-mortems, and writes a Slack, Teams, and Bicep draft for the on-call team.

The contract for handoffs and tool rules is [docs/pipeline.md](docs/pipeline.md). This file is the order of work. It does not change those rules.

## Now

Already in the repo:

- Aspire AppHost with a local Event Hubs emulator and hub `logs`.
- `DevOpsCopilot.LogForwarder`, an OpenTelemetry exporter that JSON-encodes a log event. It is not referenced by the AppHost yet.
- Agent instructions, stage skills, and the pipeline contract.
- CI on pull requests and pushes to `main`: Release build of the solution, the email notifier, tests, and the frontend.
- Projects target `net11.0`. Aspire packages are `13.6.1`.

Not built yet:

- `src/DevOpsCopilot.Daemon`, the only consumer. Consumer group `devops-copilot`.
- A wired path from an application log to hub `logs`.
- Kusto, Azure AI Search, and draft files under `incidents/`.

The weather frontend and the sample API, Functions, and email notifier projects are leftover. Do not extend them.

## Next

Work in this order. Each item is done only when the check under it passes.

1. **Publish logs to Event Hubs.** Reference `DevOpsCopilot.LogForwarder` from the service that emits logs and point `EventHubLogExporter` at hub `logs`. An `Error` or `Critical` log from a local run appears on the emulator hub as one JSON log event.
2. **Consume the stream.** Add `src/DevOpsCopilot.Daemon`, a `net11.0` worker registered from the AppHost. It reads hub `logs` with consumer group `devops-copilot`, checkpoints after each handled batch, and extracts `TraceId` and `SpanId` before any agent runs. Restarting the daemon does not replay a checkpointed batch.
3. **Diagnose with Kusto.** The diagnostic agent runs only the read-only KQL in the pipeline contract. When no cluster is configured, the handoff status is `unavailable` and no rows are invented.
4. **Ground in past post-mortems.** The RAG agent searches the `historical-postmortems` index and keeps at most five overlapping hits. When search is not configured, the status is `unavailable` and no past incident is invented.
5. **Write the on-call draft.** The remediation agent writes `incidents/YYYY-MM-DD-<event-id>.md` with the sections in the pipeline contract. It does not post to Slack or Teams and does not deploy Bicep.
6. **Prove the loop.** Add tests for the log-event parse, the decision to start the team, checkpoint resume, and a run where both tools are unavailable. CI already runs `dotnet test`.

## Later

- Index real post-mortems into `historical-postmortems`.
- Point Kusto at the workspace that stores traces and exceptions for the traced services.
- Let an operator send the draft to Slack or Teams, or deploy the Bicep, only after an explicit request.
- Remove the leftover weather UI and sample projects once nothing in the AppHost depends on them.

## Out of scope

- A fourth agent.
- KQL that changes data.
- Treating `Warning` by itself as an incident.
- Committing files under `incidents/` unless someone asks.
