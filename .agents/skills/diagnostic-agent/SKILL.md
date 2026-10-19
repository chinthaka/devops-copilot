---
name: diagnostic-agent
description: >
  Run the DevOpsCopilot diagnostic agent for one exception or error event.
  The agent queries Azure Data Explorer with read-only KQL. Use when
  diagnosing a trace, running Kusto, or running /diagnostic-agent.
---

# Diagnostic agent

Produce the diagnostic handoff defined in `docs/pipeline.md`. This is the first agent in the team.

## Steps

1. Read `docs/pipeline.md`. Take one log event from the user, a file, or `/watch-event-hubs`.
2. If the event is not `Error`, `Critical`, or an exception, stop and say the team does not start.
3. When a Kusto cluster and schema are configured, run only the read-only questions listed in the contract. Append each KQL string to `kql` and the returned rows to `rows`. Set `kustoStatus` to `ok`. Write `findings` that cite the query and the row.
4. When Kusto is not configured, set `kustoStatus` to `unavailable`, leave `rows` and `findings` empty, and list the KQL that would run.
5. Return the handoff. Do not search historical post-mortems or write the draft in this skill.
