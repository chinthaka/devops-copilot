# Incident pipeline contract

Single source for the worker, stage handoffs, tool rules and report shape. `AGENTS.md` and every feature spec point here. Change this file when a shape, a rule or a tool contract changes, in the same pull request as the change. Do not copy it into specs; link to the relevant section instead.

## Scope

DevOpsCopilot diagnoses and reports. It never changes production resources, never generates deployable infrastructure code, and never declares an incident resolved on its own.

## Flow

Errors from Azure resources and from applications are streamed to Azure Event Hubs. The DevOpsCopilot worker consumes the stream and runs these stages in order:

1. **Receive** – read and normalise events.
2. **Triage** – keep genuine errors, collapse duplicates and bursts.
3. **Correlate** – group related candidates into one incident and wait until it has settled.
4. **Gather evidence** – the diagnostic agent runs KQL against Log Analytics.
5. **Ground** – the grounding agent searches past incidents in Azure AI Search.
6. **Diagnose** – the diagnosis agent produces a structured diagnosis.
7. **Report and notify** – the report is delivered to the configured channels.
8. **Learn** – confirmed reports are indexed for future grounding.

Stages do not run in parallel. Each stage receives the previous stage's object. No stage invents tool results.

## Evidence rule

Use only the incident's events, rows returned by KQL, documents returned by Azure AI Search, and facts supplied by an engineer. If a tool is not configured or not reachable, set that stage's status to `unavailable`, record the call that would have been made, and leave the result list empty.

---

## 1. Receive

### Sources

| Source | Path to Event Hubs |
|---|---|
| `platform` | Azure Monitor diagnostic settings on Azure resources |
| `application` | Application Insights diagnostic settings (OpenTelemetry logs, exceptions and traces) |

### Stream and worker

- Resource: Azure Event Hubs, Aspire name `eventhubs`, emulator in local runs. Hub: `logs`.
- Consumer: `src/DevOpsCopilot.Daemon`, a .NET 10 worker (`net10.0`). Consumer group: `devops-copilot`.
- Diagnostic-settings payloads arrive as a `records` array. Each record is normalised into one event. Mapping rules per log category belong in the relevant feature spec.
- The worker checkpoints an Event Hubs event only after every record in it has been handled.
- A record that cannot be parsed is logged with its category and skipped. It never stops the consumer.

### Normalised event

| Field | Meaning |
|---|---|
| `eventId` | Deterministic id derived from the source record |
| `source` | `platform` or `application` |
| `timestamp` | Event time (UTC) |
| `level` | `Verbose`, `Information`, `Warning`, `Error` or `Critical` |
| `category` | Log category or logger category |
| `resourceId` | Azure resource id |
| `operationName` | Operation name, or null |
| `message` | Formatted message |
| `exceptionType` | Exception type, or null |
| `exception` | Exception string, or null |
| `traceId` | W3C trace id, or null (usually null for `platform`) |
| `spanId` | W3C span id, or null |
| `attributes` | Map of attribute name to string |

---

## 2. Triage

- An event becomes a candidate only if `level` is `Error` or `Critical`, or `exception` is set. `Warning` alone never becomes a candidate.
- **Fingerprint:** `exceptionType` (or `operationName` when null) + `category` + `resourceId` + `message` with numbers, GUIDs and timestamps removed.
- Events with the same fingerprint within 5 minutes are one candidate; increment its count instead of creating another.
- Triage state lives in the shared state store, so results are identical with one or more worker replicas.

### Candidate

```json
{
  "fingerprint": "",
  "firstSeen": "",
  "lastSeen": "",
  "count": 0,
  "sampleEvent": {},
  "traceIds": [],
  "resourceIds": []
}
```

---

## 3. Correlate

- A candidate joins an existing open incident when it shares a `traceId` with it, or when its `resourceId` matches a resource in the incident (or a dependency target recorded in the incident's events) within 10 minutes.
- Otherwise it opens a new incident.
- **Ready rule:** an incident is ready when no new candidate has joined for 90 seconds, or 5 minutes after it opened, whichever comes first. Investigation starts only when the incident is ready.
- `incidentId` is deterministic from the first candidate, so a replay after a restart never creates a duplicate.
- A candidate arriving after the incident is `reported` is attached to that incident and updates its report. It does not open a new incident.

### Lifecycle

`open` → `settling` → `ready` → `investigating` → `reported` → `resolved`

Only an engineer's confirmation (see Learn) moves an incident to `resolved`.

### Incident

```json
{
  "incidentId": "inc-<utc-compact>-<hash>",
  "status": "ready",
  "openedAt": "",
  "readyAt": "",
  "traceIds": [],
  "resourceIds": [],
  "services": [],
  "candidates": [],
  "eventCount": 0
}
```

---

## 4. Gather evidence (diagnostic agent)

The diagnostic agent may run KQL against the configured Log Analytics workspace. It records every query it executes, with its round number. `kqlStatus` is `ok` only when `rows` came back from a configured workspace.

### Allowed questions

- Exceptions for the incident's `traceIds` from 15 minutes before `openedAt` until now.
- Count of the same exception types and categories in that window versus the previous 15 minutes.
- Failed dependencies on the incident's traces.
- Platform errors for the incident's `resourceIds` in the same window.

### Query rules

- Read-only queries only. Never run management or ingestion commands (anything starting with `.`), or any query that changes data.
- One initial round plus at most 2 follow-up rounds; at most 8 queries per incident.
- Cap rows returned per query; summarise instead of returning raw volume.

### Diagnostic handoff

```json
{
  "incidentId": "",
  "kqlStatus": "unavailable",
  "kql": [{ "round": 1, "query": "" }],
  "rows": [],
  "findings": []
}
```

`findings` are sentences that cite a `kql` entry and a row. When `kqlStatus` is `unavailable`, `findings` and `rows` stay empty.

---

## 5. Ground (grounding agent)

The grounding agent searches the Azure AI Search index `historical-postmortems`. `searchStatus` is `ok` only when `hits` came back from that index. Quote excerpts; do not rewrite a hit into a new incident.

- Keep at most 5 hits.
- A hit must overlap an exception type, a category, a resource or a service in the incident or the diagnostic rows.
- Prefer `confirmed-report` hits over `postmortem` hits when relevance is similar.

### Grounding handoff

```json
{
  "incidentId": "",
  "searchStatus": "unavailable",
  "index": "historical-postmortems",
  "queries": [],
  "hits": [{ "id": "", "source": "postmortem", "title": "", "excerpt": "", "score": 0 }]
}
```

`source` is `postmortem` (imported) or `confirmed-report` (written by DevOpsCopilot and confirmed by an engineer).

---

## 6. Diagnose (diagnosis agent)

The diagnosis agent combines the incident, the diagnostic handoff and the grounding handoff into one structured diagnosis.

- Every claim cites a finding, a row or a hit.
- `confidence` is `low` when both `kqlStatus` and `searchStatus` are `unavailable`.
- `suggestedSteps` are written for an engineer to carry out. No infrastructure-as-code, deployable templates or scripts that change resources.

### Diagnosis

```json
{
  "incidentId": "",
  "title": "",
  "likelyCause": "",
  "confidence": "low",
  "impact": { "services": [], "resources": [], "eventCount": 0, "window": "" },
  "evidence": [{ "kql": 0, "row": 0 }],
  "similarIncidents": [""],
  "suggestedSteps": [],
  "openQuestions": []
}
```

---

## 7. Report and notify

One report per incident, rendered from the diagnosis.

### Report sections, in this order

1. Title, status, incident id, trace ids and hub `logs`.
2. **Impact** – services, resources, event count and time window.
3. **Likely cause** – with confidence.
4. **Evidence** – findings from the diagnostic handoff. If KQL was unavailable, say so.
5. **Similar incidents** – grounding hits. If search was unavailable, say so.
6. **Suggested steps** – for an engineer to carry out.
7. **Open questions** – missing tool results and unanswered facts.

### Channels

| Channel | Content |
|---|---|
| Microsoft Teams | Card summarising the report, linking to the work item |
| Slack | mrkdwn message summarising the report, linking to the work item |
| Work item (Azure DevOps) | The full report |

- Each channel is optional. An unconfigured channel is recorded as `unavailable` and does not fail the run.
- One message per incident per channel. When the incident changes, update the existing message where the channel supports it; otherwise post a single update linked to the original.
- Teams and Slack text must match the diagnosis and the hits.
- When no channel is configured (for example in local runs), write the report to `incidents/YYYY-MM-DD-<incident-id>.md`. `incidents/` is gitignored and is never committed unless asked.

---

## 8. Learn

- Triggered when the incident's work item is resolved.
- An engineer must confirm the likely cause or correct it. Unconfirmed reports are never indexed.
- The confirmed report is indexed into `historical-postmortems` as a `confirmed-report` document:

```json
{
  "id": "",
  "source": "confirmed-report",
  "incidentId": "",
  "title": "",
  "confirmedCause": "",
  "resolution": "",
  "services": [],
  "exceptionTypes": [],
  "resolvedAt": ""
}
```

- The incident then moves to `resolved`. DevOpsCopilot never moves an incident to `resolved` by itself.
