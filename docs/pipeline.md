# Incident pipeline contract

Single source for the daemon, agent handoffs, and draft shape. `AGENTS.md` and the skills point here. Change this file when a shape or a tool contract changes. Do not copy it into skills.

## Flow

A system error or exception is published to Azure Event Hubs. The DevOpsCopilot daemon consumes that stream, reads the W3C trace context, and runs a Semantic Kernel team in this order:

1. **Diagnostic agent** calls the Kusto (KQL) tool.
2. **RAG grounding agent** calls Azure AI Search over historical post-mortems.
3. **Remediation agent** writes a formatted draft for Slack, Microsoft Teams, and Bicep.

The team does not run in parallel. Each agent receives the previous agent's object. No agent invents tool results.

## Stream and daemon

- Publisher: application logs and exceptions. Local producer is `EventHubLogExporter` in `src/DevOpsCopilot.LogForwarder`.
- Resource: Azure Event Hubs, Aspire name `eventhubs`, emulator in local runs. Hub: `logs`.
- Consumer: `src/DevOpsCopilot.Daemon`, a .NET 11 worker (`net11.0`). Consumer group: `devops-copilot`.
- The daemon checkpoints after a batch is handled. It extracts W3C trace context (`TraceId`, `SpanId`, trace flags) from the OpenTelemetry payload before it starts the team.
- Start the team only for `Error` and `Critical` events, or events whose `Exception` is set. `Warning` alone does not start the team.

### Log event

| Field | Meaning |
|---|---|
| `Timestamp` | Event time |
| `Level` | Log level name |
| `Category` | Logger category |
| `Message` | Formatted message |
| `Exception` | Exception string, or null |
| `TraceId` | W3C trace id |
| `SpanId` | W3C span id |
| `Attribute` | Map of attribute name to string |

## Evidence

Use only the event, rows returned by Kusto, documents returned by Azure AI Search, and facts the user supplied. If a tool is not configured, set that stage's status to `unavailable`, record the call that would have been made, and leave the result list empty.

## Diagnostic handoff

The diagnostic agent may run KQL. It stores every query string it executes. `kustoStatus` is `ok` only when `rows` came back from a configured cluster.

```json
{
  "eventId": "evt-<utc-compact>",
  "traceId": "",
  "spanId": "",
  "category": "",
  "exception": "",
  "kustoStatus": "unavailable",
  "kql": [],
  "rows": [],
  "findings": []
}
```

`findings` are sentences that cite a `kql` entry and a row. When `kustoStatus` is `unavailable`, `findings` and `rows` stay empty.

Questions the KQL is allowed to answer, when a cluster and schema are configured:

- Exceptions for this `TraceId` in the event's surrounding 15 minutes.
- Count of the same exception type and `Category` in that window versus the previous 15 minutes.
- Failed dependencies on that trace.

Do not add a query that mutates data.

## Grounding handoff

The RAG agent searches the Azure AI Search index `historical-postmortems`. `searchStatus` is `ok` only when `hits` were returned by that index. Quote excerpts. Do not rewrite a hit into a new incident.

```json
{
  "eventId": "",
  "searchStatus": "unavailable",
  "index": "historical-postmortems",
  "queries": [],
  "hits": [{ "id": "", "title": "", "excerpt": "", "score": 0 }]
}
```

Keep at most 5 hits. A hit must overlap the exception type, the `Category`, or a service name present in the diagnostic rows.

## Remediation draft

Write one file per event: `incidents/YYYY-MM-DD-<event-id>.md`. Create `incidents/` if needed. Do not commit it, post it, or deploy it unless the user asks.

Sections, in this order:

1. `# Incident draft: <short title>` with status `Draft`, event id, trace id, and hub `logs`.
2. `## Diagnosis` — findings from the diagnostic handoff. If Kusto was unavailable, say so.
3. `## Similar incidents` — search hits. If search was unavailable, say so.
4. `## Slack` — mrkdwn body the on-call channel would receive.
5. `## Teams` — markdown body for a Teams message.
6. `## Bicep` — a fenced `bicep` block only when a finding names a resource change. Otherwise the section states why there is no template.
7. `## Open questions` — missing tool results and unanswered facts.

Slack and Teams text must match the diagnosis and the hits. Bicep is a proposal inside the draft. The daemon does not deploy it and does not declare the incident resolved.
