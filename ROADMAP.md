# Roadmap

DevOps Copilot listens to enterprise exception and error logs on Azure Event Hubs, reads the W3C trace context, and runs a Microsoft Agent Framework workflow. The team diagnoses with Kusto, grounds the case in historical post-mortems, and writes a Slack, Teams, and Bicep draft for the on-call team.

The contract for handoffs and tool rules is [docs/pipeline.md](docs/pipeline.md). This file is the order of work. It does not change those rules.

## Now

Already in the repo:

- Aspire AppHost with a local Event Hubs emulator and hub `logs`.
- `DevOpsCopilot.LogForwarder`, an OpenTelemetry exporter that JSON-encodes a log event. It is not referenced by the AppHost yet.
- `src/DevOpsCopilot.Daemon`, a `net11.0` worker registered from the AppHost. It is not subscribed to hub `logs`.
- A Microsoft Agent Framework workflow, `IncidentWorkflow`, that runs the diagnostic agent, the RAG grounding agent, then the remediation agent for one log event.
- Read-only Kusto queries and Azure AI Search over `historical-postmortems`. With no cluster or search configuration, the handoff status is `unavailable` and no rows or hits are invented. The remediation agent writes `incidents/YYYY-MM-DD-<event-id>.md` and does not post or deploy it. `incidents/` is gitignored.
- Tests for a `Warning` that does not start the team, both tools unavailable, the five-hit cap, the draft sections, and a KQL string containing `.drop` that is not executed.
- Agent instructions, stage skills, and the pipeline contract.
- CI on pull requests and pushes to `main`: Release build of the solution, the email notifier, tests, and the frontend.
- Projects target `net11.0`. Aspire packages are `13.6.1`.

Not built yet:

- A wired path from an application log to hub `logs`.
- The daemon as a consumer: consumer group `devops-copilot`, checkpoints, and `TraceId` and `SpanId` taken from the hub event before the workflow runs.
- The dashboard that replaces the weather UI.
- A deployment workflow. CI builds and tests only. Nothing in this repo deploys to Azure.

The sample API, Functions, and email notifier projects are leftover. Do not extend them. The weather frontend is replaced by the dashboard in Next.

## Next

Work in this order. Each item is done only when the check under it passes.

1. **Replace the weather UI with a dashboard.** Change `src/frontend` from the weather forecast into a dashboard. It shows the status of the application and statistics for how much data has been processed. Status includes whether the app is running and the health of the AppHost services. The statistics include events read, events skipped, batches checkpointed, and the time of the last processed event. Until the daemon is consuming hub `logs`, status is not running and the counts are zero. The weather forecast screen is gone.
2. **Publish logs to Event Hubs.** Reference `DevOpsCopilot.LogForwarder` from the service that emits logs and point `EventHubLogExporter` at hub `logs`. An `Error` or `Critical` log from a local run appears on the emulator hub as one JSON log event.
3. **Consume the stream.** Subscribe the existing daemon to hub `logs` with consumer group `devops-copilot`. Checkpoint after each handled batch, read `TraceId` and `SpanId`, then call `IncidentWorkflow`. Restarting the daemon does not replay a checkpointed batch. Processed counts on the dashboard increase from the events this daemon handles.
4. **Prove the consumer.** Add tests for parsing a hub log event and for resuming after a checkpoint. The team-start and unavailable-tool tests already exist. CI already runs `dotnet test`.
5. **Deploy to Azure.** Add a GitHub Actions deployment workflow that runs after the CI build and tests pass on `main`. It deploys the AppHost resources for hub `logs`, the daemon, and the dashboard to Azure. Connection strings and keys stay in GitHub or Azure configuration, not in the repo. A push to `main` produces a running Azure environment. The workflow does not deploy a remediation Bicep draft from `incidents/`.

## Later

- Index real post-mortems into `historical-postmortems`.
- Point Kusto at the workspace that stores traces and exceptions for the traced services.
- Let an operator send the draft to Slack or Teams, or deploy the Bicep, only after an explicit request.
- Remove the leftover sample projects once nothing in the AppHost depends on them.

## Out of scope

- A fourth agent.
- KQL that changes data.
- Treating `Warning` by itself as an incident.
- Committing files under `incidents/` unless someone asks.
