# Validation: <Feature name>

<!--
Write this file second, before plan.md and before any code.
Every acceptance criterion in requirement.md must appear here at least once.

Plain English only. Describe each test by the behaviour it proves, in a sentence. Do not write
test names, class names or file paths; those are chosen when the test is written. Name the layer
and the kind of test instead. See "Writing specs" in AGENTS.md.

Leave the Results section empty until the work is done. Delete these comments when done.
-->

## Coverage

| AC | Behaviour proven | Type | Layer |
|---|---|---|---|
| AC-1 | <!-- e.g. a report with no channel configured is still written to disk --> | Unit | Domain |
| AC-2 | | | |

<!--
Type: Unit, Integration, Architecture, Evaluation or Manual.
Layer: Domain, Application, Infrastructure or Host.
Domain and Application tests are plain unit tests: no cloud services, network or emulator.
-->

## Unit tests (written first)

<!-- One line per test: the starting state, the action, and the outcome it checks. -->

- (AC-1) Given … when … then …

## Integration tests

<!-- Behaviour checked against the local emulator or a real test resource, and which one it needs. Delete if none. -->

- (AC-n) …

## Architecture tests

<!-- Any layering rule this feature must not break. Delete if the existing tests already cover it. -->

-

## Evaluation

<!-- Only for AI model behaviour: which staged incidents are run and the pass condition. Delete if not applicable. -->

| Staged incident | Expected outcome | Pass condition |
|---|---|---|
| | | |

## Manual checks

<!-- Only for what cannot reasonably be automated. Delete if none. -->

-

---

## Results

<!-- Fill in after implementation. Include this section in the pull request. -->

| AC | Behaviour proven | Result | Notes |
|---|---|---|---|
| AC-1 | | Pass / Fail | |
| AC-2 | | | |

- **Build:** <clean, or what failed>
- **Tests:** <passed> / <total>
- **Evaluation run:** <result, or not applicable>
- **Date:** YYYY-MM-DD
