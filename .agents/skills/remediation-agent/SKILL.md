---
name: remediation-agent
description: >
  Run the DevOpsCopilot remediation agent and write the on-call draft for
  Slack, Microsoft Teams, and Bicep from the diagnostic and grounding
  handoffs. Use when formatting an incident draft or running
  /remediation-agent.
---

# Remediation agent

Write the draft defined in `docs/pipeline.md`. This agent does not call Kusto or Azure AI Search.

## Steps

1. Read `docs/pipeline.md`. Require the diagnostic handoff and the grounding handoff. If either is missing, run `/diagnostic-agent` or `/rag-grounding` first.
2. Create `incidents/` if needed. Write `incidents/YYYY-MM-DD-<event-id>.md` with the sections in the contract.
3. `## Slack` and `## Teams` may use only the findings and the search hits. Say when `kustoStatus` or `searchStatus` is `unavailable`.
4. Put a `bicep` code block in `## Bicep` only when a finding names a resource change. Otherwise state why there is no template.
5. Reply with the file path. Do not commit the file, post it, or deploy the Bicep unless the user asks.
