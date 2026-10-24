# Requirement: <Feature name>

<!--
Copy this folder to docs/specs/YYYY-MM-DD-feature-name/ (same date and name as the branch).
Write this file first. Describe WHAT and WHY, not HOW. Delete these comments when done.
-->

| | |
|---|---|
| **Branch** | `features/YYYY-MM-DD-feature-name` |
| **Milestone** | <e.g. M5 – Triage> ([roadmap](../roadmap.md)) |
| **Status** | Draft / In review / Approved / Done |
| **Author** | <name> |
| **Date** | YYYY-MM-DD |

## Summary

<!-- Two or three sentences: what this feature does and the problem it solves. -->

## Why

<!-- The need behind it. Link to the mission or roadmap item it serves. -->

## Scope

**In scope**

- <!-- What this feature delivers. -->

**Out of scope**

- <!-- What it deliberately does not do, including anything a reader might assume it covers. -->

## Inputs and outputs

| | Description | Shape |
|---|---|---|
| **Input** | <!-- e.g. a normalised event --> | <!-- link to the pipeline.md section --> |
| **Output** | <!-- e.g. a candidate --> | <!-- link to the pipeline.md section --> |

## Contract and rules touched

<!--
List the docs/pipeline.md sections this feature implements or depends on, by link.
Do not copy their content here. If the feature needs a contract change, say so here
and describe it in plan.md.
-->

- [pipeline.md – <section>](../../pipeline.md#section)

## Domain language

<!--
The domain concepts this feature uses or introduces (aggregates, value objects, domain events),
named as in pipeline.md. New terms must be added to pipeline.md in the same pull request.
-->

| Term | Meaning in this feature |
|---|---|
| | |

## Acceptance criteria

<!--
Numbered, observable and testable. Each one must map to at least one entry in validation.md.
Prefer Given / When / Then.
-->

**AC-1** – <short title>
- **Given** <starting state>
- **When** <action or event>
- **Then** <observable outcome>

**AC-2** – <short title>
- **Given**
- **When**
- **Then**

## Non-functional requirements

<!-- Only those that apply: performance, cost limits, security, idempotency, multi-replica behaviour. Delete if none. -->

-

## Open questions

<!-- Anything that must be decided before approval. Approval requires this list to be empty or answered. -->

-
