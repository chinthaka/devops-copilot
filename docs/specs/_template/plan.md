# Plan: <Feature name>

<!--
Write this file third. Describe HOW the requirement will be met, within the rules in AGENTS.md
(strict Clean Architecture, pragmatic DDD, SOLID).

Plain English only. Say which layer changes and what responsibility it gains, not which classes,
interfaces, types or files will be written — the developer chooses those while building, and a
spec that fixes them goes out of date at the first rename. See "Writing specs" in AGENTS.md.

Delete these comments when done.
-->

## Approach

<!-- A short description of the design and why it was chosen over the obvious alternatives. -->

## Changes by layer

<!-- List only the layers this feature touches. Dependencies must point inwards only. -->

| Layer | What changes here | Notes |
|---|---|---|
| Domain | <!-- the business concepts and rules this feature adds or changes --> | |
| Application | <!-- the use case, where it sits in the stage order, what it needs from outside --> | |
| Infrastructure | <!-- which outside services are connected, and what is translated at the boundary --> | |
| Host | <!-- wiring and configuration only --> | |

## What this feature needs from outside

<!--
Each capability the use case needs from an outside service: what it must do, what it must
return, and how it must behave when the service is unavailable. Describe the responsibility,
not the interface. Delete if none.
-->

| Capability | Responsibility | Provided by |
|---|---|---|
| | | |

## Contract changes

<!--
Changes to docs/pipeline.md (shapes, rules, tool contracts), made in the same pull request.
Write "None" if the feature only implements the existing contract.
-->

None.

## Tech-stack changes

<!-- New packages, services or versions. Each must be added to docs/specs/tech-stack.md in the same pull request, which is where exact names and versions belong. Write "None" if none. -->

None.

## Configuration and secrets

<!--
New settings: what each one controls, what happens when it is not set, and what a local run
uses. Do not write the setting keys or any secret value. Delete if none.
-->

-

## Implementation steps

<!--
Ordered, small steps. Each step starts from a failing test listed in validation.md.
Keep each step reviewable on its own.
-->

1. **<Step>** – write the test for <behaviour> (AC-1), then build the behaviour in <layer>.
2. **<Step>** – …
3. **Wire up** – connect the new implementations in the Host.
4. **Verify** – build, run the tests, run the evaluation if listed; fill in Results in validation.md.

## Risks

<!-- What could go wrong or take longer, and the fallback. Delete if none. -->

| Risk | Mitigation |
|---|---|
| | |

## Definition of done

- [ ] All acceptance criteria in requirement.md are covered in validation.md
- [ ] Tests written first and passing; the build and the full test run are clean
- [ ] No outward project references; architecture tests pass
- [ ] `docs/pipeline.md` and `docs/specs/tech-stack.md` updated if changed
- [ ] All three spec files are in plain English, with no code syntax
- [ ] Results section of validation.md completed
- [ ] Pull request links this feature folder
