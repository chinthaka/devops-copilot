# Mission

DevOpsCopilot is an open-source AI assistant that investigates Azure production incidents and sends on-call engineers the likely cause and suggested fixes.

It diagnoses and recommends. It never changes anything in production. Engineers make every change.

## Problem

When something breaks in production, most of the time to resolution is spent finding out *what* is wrong: opening several tools, searching logs, and trying to remember whether this has happened before. Alert noise makes this worse, and post-mortem knowledge is rarely reused because nobody can find it under pressure.

## What DevOpsCopilot does

By the time an engineer opens the alert, they already have:

- the errors that belong to the incident, grouped across services,
- live log evidence from around the failure,
- similar past incidents and how they were fixed,
- a likely cause, a confidence level and suggested resolution steps.

The intended outcomes are lower mean time to resolution (MTTR), fewer noisy alerts, and post-mortem knowledge that is actually reused.

## Who it serves

| Audience | What they get |
|---|---|
| **On-call engineers** | A report in Teams or Slack with a likely cause and steps to try, before they start digging. |
| **Platform and SRE teams** | Zero-touch onboarding of new systems through Azure diagnostic settings, enforceable with Azure Policy. No code changes in the monitored systems. |
| **Engineering leads** | A knowledge base of confirmed incident reports that grows with every resolved incident. |

## How it works, in one paragraph

Azure resources and applications stream errors and logs to Azure Event Hubs through diagnostic settings. A .NET worker consumes the stream and runs eight stages in order: receive, triage, correlate, gather evidence with read-only KQL, ground in past incidents with Azure AI Search, diagnose with an LLM, report and notify, and learn from engineer-confirmed reports. The stage handoffs and tool rules are defined in [docs/pipeline.md](../pipeline.md).

## Principles

These are the rules every feature is checked against. A feature that breaks one of them is out of scope until the principle itself is changed here.

1. **Diagnose, don't act.** Read-only by design. KQL cannot change data. No production resource is changed, and no deployable infrastructure code, template or script is produced. Suggested steps are written for an engineer to carry out.
2. **Never invent evidence.** Every claim cites an event, a KQL row, a search hit or a fact an engineer supplied. If a data source is unavailable, the report says so and leaves the result empty.
3. **Fixed, bounded investigation.** The agents follow a fixed stage order with limits on follow-up queries, rows and hits. This keeps behaviour predictable, testable and cost-controlled.
4. **Learn only from confirmed causes.** A report enters the knowledge base only after an engineer confirms or corrects it. DevOpsCopilot never marks an incident resolved by itself.
5. **Zero-touch onboarding.** New systems are added through configuration, not code.
6. **Cloud-neutral core.** The domain and application layers depend on interfaces. Azure services are the first implementations, not the only possible ones.
7. **Spec first, test first.** Every feature starts with a written spec in `docs/specs/`, and tests are written from its acceptance criteria before the code. See [AGENTS.md](../../AGENTS.md).

## Boundaries

**In scope**

- Azure-hosted resources and applications whose logs reach Event Hubs through Azure Monitor or Application Insights diagnostic settings.
- Microsoft Teams, Slack and Azure DevOps work items as report channels.
- A dashboard showing the worker's status and processing statistics.

**Out of scope, for now**

- Automated remediation of any kind, including generating Bicep, ARM, Terraform or scripts.
- AWS and GCP support. The core is designed to allow it later; nothing is built for it.
- A direct OpenTelemetry exporter for applications that do not use Application Insights. The in-repo `LogForwarder` exists only as a local test producer.
- Collecting historical data in advance. The knowledge base starts empty and grows from confirmed incidents.
- A fourth agent. The workflow has a diagnostic agent, a grounding agent and a diagnosis agent.
- Treating a `Warning` on its own as an incident.

## Success looks like

- An engineer opening an alert finds the report already waiting, and it names the right area to look at more often than not.
- Hundreds of identical errors arrive as one incident, not hundreds of alerts.
- A recurring incident is grounded in the previous confirmed report and says so.
- Nothing DevOpsCopilot does needs a production change approval.

## Related documents

- [tech-stack.md](tech-stack.md) – technologies and services.
- [roadmap.md](roadmap.md) – phases and order of work.
- [docs/pipeline.md](../pipeline.md) – the system contract.
- [README.md](../../README.md) – overview for people.
- [AGENTS.md](../../AGENTS.md) – working rules for AI coding agents.
