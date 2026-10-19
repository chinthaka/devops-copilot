---
name: incident-response
description: >
  Run the DevOpsCopilot daemon loop for one exception or error event: diagnose
  with Kusto, ground against historical post-mortems in Azure AI Search, and
  write the Slack, Teams, and Bicep draft. Use when the user asks to
  investigate an event, handle an incident, or runs /incident-response.
---

# Incident response

Run the Semantic Kernel team once for one event, in order. Handoff shapes are in `docs/pipeline.md`. Read it first. Read each stage skill and follow it. Do not copy those steps into this file.

## Steps

1. Read `docs/pipeline.md`.
2. Establish the event.
   - Pasted event or a file path: use that text.
   - A live hub: follow `.agents/skills/watch-event-hubs/SKILL.md` and take the next `Error`, `Critical`, or exception event.
   - No event and no hub: stop. Say what is missing. Do not synthesize an event.
3. Follow `.agents/skills/diagnostic-agent/SKILL.md`.
4. Follow `.agents/skills/rag-grounding/SKILL.md` with the diagnostic handoff.
5. Follow `.agents/skills/remediation-agent/SKILL.md` with both handoffs.
6. Reply with the event id, `kustoStatus`, `searchStatus`, and the draft path.

Do not post the Slack or Teams section, and do not deploy the Bicep, unless the user asks.
