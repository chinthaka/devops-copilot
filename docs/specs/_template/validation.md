# Validation: <Feature name>

<!--
Write this file second, before plan.md and before any code.
Every acceptance criterion in requirement.md must appear here at least once.
Leave the Results section empty until the work is done. Delete these comments when done.
-->

## Coverage

| AC | Test or check | Type | Layer | Project |
|---|---|---|---|---|
| AC-1 | `<BehaviourUnderTest_WhenCondition_ThenOutcome>` | Unit | Domain | `tests/DevOpsCopilot.Domain.Tests` |
| AC-2 | | | | |

<!--
Type: Unit, Integration, Architecture, Evaluation or Manual.
Layer: Domain, Application, Infrastructure or Host.
Domain and Application tests are plain unit tests: no Azure SDKs, network or emulator.
-->

## Unit tests (written first)

<!-- One line per test: the behaviour it proves, written as Arrange, Act, Assert. -->

- **`<TestName>`** (AC-1) – Arrange: … Act: … Assert: …

## Integration tests

<!-- Infrastructure behaviour against the emulator or test resources. Delete if none. -->

- **`<TestName>`** (AC-n) – what it exercises and which emulator or resource it needs.

## Architecture tests

<!-- Any new or changed layer rule this feature must not break. Delete if the existing tests already cover it. -->

-

## Evaluation

<!-- Only for LLM behaviour: which staged incidents are run and the pass condition. Delete if not applicable. -->

| Staged incident | Expected outcome | Pass condition |
|---|---|---|
| | | |

## Manual checks

<!-- Only for what cannot reasonably be automated. Delete if none. -->

-

---

## Results

<!-- Fill in after implementation. Include this section in the pull request. -->

| AC | Test or check | Result | Notes |
|---|---|---|---|
| AC-1 | | Pass / Fail | |
| AC-2 | | | |

- **`dotnet build`:** 
- **`dotnet test`:** <passed> / <total>
- **Evaluation run:** <result, or not applicable>
- **Date:** YYYY-MM-DD
