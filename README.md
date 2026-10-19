# devops-copilot

DevOpsCopilot is a .NET 11 worker that listens to enterprise exception and error logs on Azure Event Hubs. It reads W3C trace context with OpenTelemetry and runs a Microsoft Agent Framework workflow: diagnose with Kusto, ground the case in historical post-mortems, and write a Slack, Teams, and Bicep draft for the on-call team.

Agent instructions: [AGENTS.md](AGENTS.md). Handoff shapes and tool rules: [docs/pipeline.md](docs/pipeline.md). Skills live under `.agents/skills/`.

## Architecture

```text
 [System Error / Exception Event]
               │
               ▼
   [Azure Event Hubs Stream]
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│ .NET 11 Worker Service (DevOpsCopilot Daemon)               │
│                                                             │
│  1. Ingests W3C Trace Context via OpenTelemetry             │
│  2. Triggers Microsoft Agent Framework team                 │
│                                                             │
│    ┌──────────────────┐      ┌───────────────────────────┐  │
│    │ Diagnostic Agent │ ───► │ Tool: Kusto Query (KQL)   │  │
│    └────────┬─────────┘      └───────────────────────────┘  │
│             │                                               │
│             ▼                                               │
│    ┌──────────────────┐      ┌───────────────────────────┐  │
│    │ RAG Grounding    │ ───► │ Tool: Azure AI Search     │  │
│    │ Agent            │      │ (Historical Post-Mortems) │  │
│    └────────┬─────────┘      └───────────────────────────┘  │
│             │                                               │
│             ▼                                               │
│    ┌──────────────────┐      ┌───────────────────────────┐  │
│    │ Remediation      │ ───► │ Output: Formatted Draft   │  │
│    │ Agent            │      │ (Slack / Teams / Bicep)   │  │
│    └──────────────────┘      └───────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

The daemon project is `src/DevOpsCopilot.Daemon`. It consumes the `logs` hub. The diagnostic agent queries Kusto. The grounding agent searches the `historical-postmortems` index. The remediation agent writes a draft under `incidents/` and does not post or deploy it unless asked.
