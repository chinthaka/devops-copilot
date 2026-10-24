# Plan: <Feature name>

<!--
Write this file third. Describe HOW the requirement will be met, within the rules in AGENTS.md
(strict Clean Architecture, pragmatic DDD, SOLID). Delete these comments when done.
-->

## Approach

<!-- A short description of the design and why it was chosen over the obvious alternatives. -->

## Changes by layer

<!-- List only the layers this feature touches. Dependencies must point inwards only. -->

| Layer | Project | New or changed types | Notes |
|---|---|---|---|
| Domain | `src/DevOpsCopilot.Domain` | <!-- aggregates, value objects, domain events --> | |
| Application | `src/DevOpsCopilot.Application` | <!-- use cases, interfaces --> | |
| Infrastructure | `src/DevOpsCopilot.Infrastructure` | <!-- interface implementations, mappers --> | |
| Host | `src/DevOpsCopilot.Worker` | <!-- registration and configuration only --> | |

## Interfaces

<!-- New or changed Application interfaces, with their responsibility. Keep them small and focused. Delete if none. -->

| Interface | Responsibility | Implemented by |
|---|---|---|
| | | |

## Contract changes

<!--
Changes to docs/pipeline.md (shapes, rules, tool contracts), made in the same pull request.
Write "None" if the feature only implements the existing contract.
-->

None.

## Tech-stack changes

<!-- New packages, services or versions. Each must be added to docs/specs/tech-stack.md in the same pull request. Write "None" if none. -->

None.

## Configuration and secrets

<!-- New settings, where they are read from, and local defaults. Never put secret values here. Delete if none. -->

-

## Implementation steps

<!--
Ordered, small steps. Each step starts from a failing test listed in validation.md.
Keep each step reviewable on its own.
-->

1. **<Step>** – write `<TestName>` (AC-1), then implement `<Type>` in <layer>.
2. **<Step>** – …
3. **Wire up** – register implementations in the Host.
4. **Verify** – `dotnet build`, `dotnet test`, evaluation if listed; fill in Results in validation.md.

## Risks

<!-- What could go wrong or take longer, and the fallback. Delete if none. -->

| Risk | Mitigation |
|---|---|
| | |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; `dotnet build` and `dotnet test` clean
- [ ] No outward project references; architecture tests pass
- [ ] `docs/pipeline.md` and `docs/specs/tech-stack.md` updated if changed
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
