---
name: rag-grounding
description: >
  Run the DevOpsCopilot RAG grounding agent. It searches Azure AI Search
  historical post-mortems for an exception already diagnosed with Kusto.
  Use when grounding an incident, searching past post-mortems, or running
  /rag-grounding.
---

# RAG grounding agent

Produce the grounding handoff defined in `docs/pipeline.md`. Run this agent only after the diagnostic handoff exists.

## Steps

1. Read `docs/pipeline.md`. Require the diagnostic handoff. If it is missing, run `/diagnostic-agent` first.
2. Build the search text from the event's exception type, `Category`, and any service names present in diagnostic `rows`.
3. When Azure AI Search is configured, query the index `historical-postmortems`. Append the query text to `queries`. Keep at most 5 hits that overlap the exception type, the category, or a service name from the rows. Set `searchStatus` to `ok`.
4. When search is not configured, set `searchStatus` to `unavailable` and leave `hits` empty.
5. Return the handoff. Do not invent past incidents. Do not write the Slack, Teams, or Bicep draft here.
