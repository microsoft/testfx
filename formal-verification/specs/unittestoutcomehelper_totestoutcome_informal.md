# Informal Specification: `UnitTestOutcomeHelper.ToTestOutcome`

> 🔬 **Lean Squad** — auto-generated formal-verification artifact.
> **Target ID**: 8  |  **Phase**: 2 (informal spec)  |  **Date**: 2026-05-08

## 1. Purpose

`UnitTestOutcomeHelper.ToTestOutcome` maps the MSTest-internal `UnitTestOutcome` enum to the VSTest-platform `TestOutcome` enum.  It acts as the **bridge between the MSTest execution layer and the test-platform reporting layer**: every test result that is exposed to IDE, CI, and other consumers passes through this function.

The mapping is **almost deterministic** but has two **configuration-dependent branches** controlled by `MSTestSettings`:
- `MapNotRunnableToFailed` — when `true`, `NotRunnable` is reported as `Failed` instead of `None`.
- `MapInconclusiveToFailed` — when `true`, `Inconclusive` is reported as `Failed` instead of `Skipped`.

## 2. Function Signature

```csharp
internal static TestOutcome ToTestOutcome(
    UnitTestOutcome unitTestOutcome,
    MSTestSettings currentSettings)
```

**Source**: `src/Adapter/MSTestAdapter.PlatformServices/Helpers/UnitTestOutcomeHelper.cs`

## 3. Input Domain

### `UnitTestOutcome` (source enum)

| Value | Int | Meaning |
|-------|-----|---------|
| `Failed`      | 0 | Test executed; assertion or exception failed |
| `Inconclusive`| 1 | Ambiguous result — no assertion passed or failed |
| `Passed`      | 2 | Test executed without issues |
| `InProgress`  | 3 | Test currently executing |
| `Error`       | 4 | System error during execution |
| `Timeout`     | 5 | Test timed out |
| `Aborted`     | 6 | Test was aborted |
| `Unknown`     | 7 | Unknown state |
| `NotRunnable` | 8 | Test cannot be executed |
| `NotFound`    | 9 | Specific test was not found |
| `Ignored`     | 10 | Test is marked as ignored |

Note: the enum has no upper bound enforced by C#; out-of-range integer casts are possible.

### `MSTestSettings` (relevant fields)

| Field | Default | Effect |
|-------|---------|--------|
| `MapNotRunnableToFailed` | `true`  | Changes `NotRunnable` mapping |
| `MapInconclusiveToFailed`| `false` | Changes `Inconclusive` mapping |

## 4. Output Domain

### `TestOutcome` (target enum — VSTest platform)

| Value | Meaning |
|-------|---------|
| `Passed`   | Test passed |
| `Failed`   | Test failed or errored |
| `Skipped`  | Test was skipped or inconclusive |
| `None`     | No outcome (not run, not applicable) |
| `NotFound` | Test was not found |

## 5. Complete Mapping Table

| `unitTestOutcome` | `MapNotRunnableToFailed` | `MapInconclusiveToFailed` | → `TestOutcome` |
|-------------------|--------------------------|---------------------------|-----------------|
| `Passed`          | any                      | any                       | `Passed`        |
| `Failed`          | any                      | any                       | `Failed`        |
| `Error`           | any                      | any                       | `Failed`        |
| `Timeout`         | any                      | any                       | `Failed`        |
| `Aborted`         | any                      | any                       | `Failed`        |
| `Unknown`         | any                      | any                       | `Failed`        |
| `Ignored`         | any                      | any                       | `Skipped`       |
| `NotFound`        | any                      | any                       | `NotFound`      |
| `NotRunnable`     | `true`                   | any                       | `Failed`        |
| `NotRunnable`     | `false`                  | any                       | `None`          |
| `Inconclusive`    | any                      | `true`                    | `Failed`        |
| `Inconclusive`    | any                      | `false`                   | `Skipped`       |
| `InProgress`      | any                      | any                       | `None`          |
| _out-of-range_    | any                      | any                       | `None`          |

## 6. Preconditions

- `unitTestOutcome` is any value of type `UnitTestOutcome` (including out-of-range integer casts).
- `currentSettings` is a non-null `MSTestSettings` instance.
- `MapNotRunnableToFailed` and `MapInconclusiveToFailed` are independently set Booleans.

## 7. Postconditions

1. **Passed → Passed**: `unitTestOutcome = Passed` ⟹ result = `TestOutcome.Passed`.
2. **Failure group → Failed**: `unitTestOutcome ∈ {Failed, Error, Timeout, Aborted, Unknown}` ⟹ result = `TestOutcome.Failed`.
3. **Ignored → Skipped**: `unitTestOutcome = Ignored` ⟹ result = `TestOutcome.Skipped`.
4. **NotFound → NotFound**: `unitTestOutcome = NotFound` ⟹ result = `TestOutcome.NotFound`.
5. **NotRunnable/MapTrue → Failed**: `unitTestOutcome = NotRunnable ∧ currentSettings.MapNotRunnableToFailed = true` ⟹ result = `TestOutcome.Failed`.
6. **NotRunnable/MapFalse → None**: `unitTestOutcome = NotRunnable ∧ currentSettings.MapNotRunnableToFailed = false` ⟹ result = `TestOutcome.None`.
7. **Inconclusive/MapTrue → Failed**: `unitTestOutcome = Inconclusive ∧ currentSettings.MapInconclusiveToFailed = true` ⟹ result = `TestOutcome.Failed`.
8. **Inconclusive/MapFalse → Skipped**: `unitTestOutcome = Inconclusive ∧ currentSettings.MapInconclusiveToFailed = false` ⟹ result = `TestOutcome.Skipped`.
9. **InProgress → None**: `unitTestOutcome = InProgress` ⟹ result = `TestOutcome.None`.
10. **Out-of-range → None**: `unitTestOutcome ∉ {Passed, Failed, Inconclusive, InProgress, Error, Timeout, Aborted, Unknown, NotRunnable, NotFound, Ignored}` ⟹ result = `TestOutcome.None`.
11. **Total coverage**: every input produces exactly one of `{Passed, Failed, Skipped, None, NotFound}`.
12. **Passed is never demoted**: the output is `Passed` if and only if the input is `Passed`.

## 8. Invariants

- The function is **pure** given a fixed `currentSettings`: same inputs always produce the same output.
- The function is **total**: it returns a value for every possible `(UnitTestOutcome, bool, bool)` triple.
- The set `{Passed, Failed, Skipped, NotFound}` is only reached by specific named inputs; only `None` is reachable by out-of-range values.
- **Monotonicity (informal)**: enabling `MapNotRunnableToFailed` or `MapInconclusiveToFailed` can only change the output from `{None, Skipped}` to `Failed`, never the other direction.

## 9. Edge Cases

| Scenario | Expected | Notes |
|----------|----------|-------|
| `InProgress` | `None` | Not handled by named branch; falls to default `_ => TestOutcome.None` |
| Out-of-range int cast | `None` | Also falls to default |
| `NotRunnable` with `MapNotRunnableToFailed=false` | `None` | Default setting changed from `true` to `false` in config |
| `Inconclusive` with `MapInconclusiveToFailed=false` | `Skipped` | Default — not a test failure |
| `Aborted` | `Failed` | Grouped with `Failed/Error/Timeout/Unknown` |
| `Unknown` | `Failed` | Same group |

## 10. Formal Properties for Lean (Preview)

The following properties are suitable for `decide`-based proofs in Lean 4:

```
-- P1: Passed maps to Passed (both configurations)
∀ s : Settings, ToTestOutcome Passed s = TestOutcome.Passed

-- P2: Failure group all map to Failed
∀ s, ToTestOutcome Failed s = Failed
∀ s, ToTestOutcome Error s = Failed
∀ s, ToTestOutcome Timeout s = Failed
∀ s, ToTestOutcome Aborted s = Failed
∀ s, ToTestOutcome Unknown s = Failed

-- P3: NotRunnable respects the flag
ToTestOutcome NotRunnable {mapNotRunnableToFailed := true, ..} = Failed
ToTestOutcome NotRunnable {mapNotRunnableToFailed := false, ..} = None

-- P4: Inconclusive respects the flag
ToTestOutcome Inconclusive {mapInconclusiveToFailed := true, ..} = Failed
ToTestOutcome Inconclusive {mapInconclusiveToFailed := false, ..} = Skipped

-- P5: Output range is contained in {Passed, Failed, Skipped, None, NotFound}
∀ uo s, ToTestOutcome uo s ∈ {Passed, Failed, Skipped, None, NotFound}

-- P6: Passed is the unique pre-image of TestOutcome.Passed
∀ uo s, ToTestOutcome uo s = TestOutcome.Passed ↔ uo = UnitTestOutcome.Passed
```

All six properties are decidable given finite enumerations and Boolean flags.

## 11. Open Questions

- **OQ-1**: Why is `InProgress` mapped to `None` rather than `Skipped`? Is this intentional? A test still executing should arguably not have a final outcome.
- **OQ-2**: Can `NotRunnable` and `Inconclusive` be meaningfully round-tripped (i.e., can the original `UnitTestOutcome` be recovered from a `TestOutcome`)? If so, the mapping is lossy and the two flag-dependent cases create aliasing.
- **OQ-3**: Out-of-range integer casts are silently mapped to `None`. Is there a diagnostic or telemetry hook that should fire in this case?

## 12. Existing Test Coverage

The file `test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Helpers/UnitTestOutcomeHelperTests.cs` covers:

| Input | Covered? |
|-------|----------|
| `Passed` | ✅ |
| `Failed` | ✅ |
| `Error` | ✅ |
| `Timeout` | ✅ |
| `NotRunnable` (default `MapNotRunnableToFailed=true`) | ✅ |
| `Ignored` | ✅ |
| `Inconclusive` (default `MapInconclusiveToFailed=false`) | ✅ |
| `NotFound` | ✅ |
| `InProgress` | ✅ |
| `Aborted` | ❌ (not tested) |
| `Unknown` | ❌ (not tested) |
| `NotRunnable` with `MapNotRunnableToFailed=false` | ❌ (not tested) |
| `Inconclusive` with `MapInconclusiveToFailed=true` | ❌ (not tested) |
| Out-of-range | ❌ (not tested) |

Five cases are not covered by existing tests.
