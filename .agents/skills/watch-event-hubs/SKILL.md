---
name: watch-event-hubs
description: >
  Connect the DevOpsCopilot daemon to Azure Event Hubs and read W3C trace
  context from hub `logs`. Use when wiring the producer, adding the .NET 11
  worker, checkpointing, reading a local emulator batch, or running
  /watch-event-hubs.
---

# Watch Event Hubs

Hub name, consumer group, and the log-event fields are in `docs/pipeline.md`. Use those names.

## When the user wants code

1. Keep the Aspire `eventhubs` resource and hub `logs` in `src/DevOpsCopilot.AppHost/AppHost.cs`. Add them only if they are missing.
2. Reference `DevOpsCopilot.LogForwarder` from the service that emits logs, and point `EventHubLogExporter` at hub `logs`.
3. Add `src/DevOpsCopilot.Daemon` if it does not exist. It is a `net11.0` worker and the only consumer. Consumer group: `devops-copilot`.
4. On each event, read `TraceId` and `SpanId` before any agent runs. Checkpoint after the batch is handled. Do not start the incident workflow from this skill.

## When the user wants an event to investigate

1. Prefer an event the user pasted or a file they named.
2. Otherwise read hub `logs` from the running emulator and return the next `Error`, `Critical`, or exception event.
3. Parse the body as a log event from `docs/pipeline.md`. Count bodies that are not that JSON and skip them.
4. Return the event and its trace context. Do not call Kusto or Azure AI Search here.
