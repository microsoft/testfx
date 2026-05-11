# Informal Specification: `CommandLineOptionsValidator.ValidateOptionsArgumentArity`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

**Function**: `CommandLineOptionsValidator.ValidateOptionsArgumentArity`  
**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs`  
**Visibility**: `private static`  
**Return type**: `ValidationResult`

## Purpose

Given a parsed command line and a dictionary mapping each known option name to its
declared `ArgumentArity`, this function checks that the total number of arguments
supplied for each option falls within the declared arity bounds `[Min, Max]`.

The function is one validation step inside the larger `ValidateAsync` pipeline and runs
_after_ `ValidateNoUnknownOptions`, so every option name found in `parseResult` is
expected to exist in `providerAndOptionByOptionName`.

## Signature

```csharp
private static ValidationResult ValidateOptionsArgumentArity(
    CommandLineParseResult parseResult,
    Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)>
        providerAndOptionByOptionName)
```

## Key Types

| Type | Relevant Members |
|------|-----------------|
| `CommandLineParseResult` | `Options : IEnumerable<CommandLineParseOption>` |
| `CommandLineParseOption` | `Name : string`, `Arguments : string[]` |
| `ArgumentArity` | `Min : int`, `Max : int` |
| `ValidationResult` | `Valid()`, `Invalid(string message)`, `IsValid : bool` |

## Preconditions

1. `parseResult` is non-null and `parseResult.HasError` is `false` (checked by the
   caller before reaching this step).
2. Every option name in `parseResult.Options` exists as a key in
   `providerAndOptionByOptionName`. Violating this precondition causes a
   `KeyNotFoundException` at runtime (see **Open Questions** below).
3. `ArgumentArity` values satisfy `0 ≤ Min ≤ Max` (invariant of `CommandLineOption`
   construction, not enforced here).

## Postconditions

- Returns `ValidationResult.Valid()` if and only if, for every distinct option name `n`
  in `parseResult.Options`, the total argument count `T(n)` satisfies
  `Min(n) ≤ T(n) ≤ Max(n)`.
- Returns `ValidationResult.Invalid(message)` with a human-readable message describing
  every violation if any exist. Multiple violations are aggregated into a single result.

## Algorithm

```
for each distinct option name n in parseResult.Options:
    T(n) = sum of Arguments.Length over all occurrences of n
    arity = providerAndOptionByOptionName[n].Option.Arity
    if T(n) > 0 AND arity.Max == 0:
        record "option n expects no arguments" error
    else if T(n) < arity.Min:
        record "option n expects at least arity.Min arguments" error
    else if T(n) > arity.Max:
        record "option n expects at most arity.Max arguments" error
return Valid() if no errors, else Invalid(aggregated errors)
```

### Why "Max == 0" is a special case

The first branch (`Max == 0 AND T > 0`) is guarded by an explicit `Max == 0` check
rather than falling through to the generic `T > Max` branch. This selects a distinct
error message ("option expects **no** arguments") rather than the generic "expects at
most N arguments" message. Both branches are semantically equivalent when `Max == 0`,
but the special case improves user-facing error quality.

### Argument accumulation across repeated options

A single option may appear more than once on the command line
(e.g., `--filter A --filter B`). Arguments are _summed_ across all occurrences:

```
T("filter") = Arguments["--filter A"].Length + Arguments["--filter B"].Length = 1 + 1 = 2
```

The function groups by `Name` before summing, so the total count reflects the union
of all invocations.

## Invariants

1. **Determinism**: the result depends only on `parseResult.Options` and the arity map;
   no external I/O or mutable state.
2. **Idempotency**: calling the function twice with the same arguments returns the same
   result.
3. **Monotonicity of errors**: adding more arguments to a previously-valid option can
   only introduce new errors; removing arguments from a previously-invalid option can
   only remove errors.
4. **Independent errors**: each option's arity violation is reported independently; a
   violation on option `a` does not suppress reporting of a violation on option `b`.

## Error Messages (resource strings)

| Condition | Resource key |
|-----------|-------------|
| `Max == 0 AND T > 0` | `CommandLineOptionExpectsNoArguments` (format: option name, provider display name, provider UID) |
| `T < Min` | `CommandLineOptionExpectsAtLeastArguments` (format: option name, provider display name, provider UID, `Min`) |
| `T > Max AND Max > 0` | `CommandLineOptionExpectsAtMostArguments` (format: option name, provider display name, provider UID, `Max`) |

## Edge Cases

| Scenario | Expected Behaviour |
|----------|--------------------|
| No options in `parseResult.Options` | Returns `Valid()` immediately (loop body never executes) |
| Option present zero times | Not possible — the `GroupBy` only yields groups for options that appear at least once |
| Option present once with zero arguments | `T = 0`; valid if `Min == 0` |
| Option with `ExactlyOne` arity, present twice each with one argument | `T = 2 > Max = 1` → `Invalid` |
| Option with `ZeroOrMore` arity, any count | Always valid (Min=0, Max=int.MaxValue) |
| Option with `Zero` arity and one argument | `Max == 0 AND T > 0` → special "no arguments" error |

## Examples

```
Option "--verbose", Arity = Zero (Min=0, Max=0)
  Input: --verbose                 → T=0 → Valid
  Input: --verbose foo             → T=1, Max==0 → Invalid("expects no arguments")

Option "--output", Arity = ExactlyOne (Min=1, Max=1)
  Input: (missing)                 → option not in GroupBy → not checked here
  Input: --output foo              → T=1 → Valid
  Input: --output foo --output bar → T=2 > Max=1 → Invalid("expects at most 1")

Option "--filter", Arity = OneOrMore (Min=1, Max=int.MaxValue)
  Input: --filter                  → T=0 < Min=1 → Invalid("expects at least 1")
  Input: --filter a --filter b     → T=2 ≥ Min=1 → Valid
```

## Inferred Intent

The function is intended to enforce the contract declared by each extension or system
provider for how many arguments their option accepts. It treats repeated option
appearances as additive (combining all arguments from all occurrences), which is the
natural interpretation for variadic options like `--filter`.

The two-step check (`Max == 0` special case first, then `T > Max` generic case) suggests
the author wanted a "flag" option (one that accepts no arguments) to have a clearly
distinct error message from a bounded-arity option.

## Open Questions / Design Issues

1. **OQ-1 (KeyNotFoundException)**: The `providerAndOptionByOptionName[optionName]`
   lookup will throw `KeyNotFoundException` if an option name from `parseResult` is
   missing from the dictionary. The caller is supposed to call
   `ValidateNoUnknownOptions` first, but this ordering is enforced only by code
   convention, not by the type system. A safe alternative would be `TryGetValue`.

2. **OQ-2 (Max == 0 special-case ordering)**: When `Max == 0` and `T > 0`, the first
   branch fires before the `T > Max` branch. This means the generic "at most 0" message
   is never emitted for flag options; only the dedicated "no arguments" message is used.
   The intent is sound, but a formal verifier should confirm the branches are disjoint
   (they are, since the first branch requires `arity.Max == 0`).

3. **OQ-3 (Missing option not flagged here)**: If an option was declared with
   `Min > 0` but does not appear in the parse result at all, `GroupBy` will not produce
   a group for it and the function will NOT report a missing-argument error. Required
   option enforcement is expected to happen elsewhere (in `ValidateConfigurationAsync`
   or provider-level validation); this function only validates options that **were**
   provided.

4. **OQ-4 (Interaction with `ZeroOrMore`)**: `ZeroOrMore` has `Max = int.MaxValue`.
   The second special-case check (`T > Max`) will never trigger for these options
   because no realistic argument count can exceed `int.MaxValue`. This is correct by
   design but worth noting as an approximation in the Lean model (use `⊤` or a large
   sentinel in Lean rather than `Int.max`).

## Approximations for Lean Modelling

- **Strings as opaque identifiers**: option names and error messages can be modelled as
  `String` or as abstract `Name` tokens; the content of error messages need not be
  verified.
- **`int.MaxValue` as `⊤`**: `ZeroOrMore.Max = int.MaxValue` can be replaced by a
  sentinel `∞` in the arity type, or by `Nat` with a separate `Unbounded` constructor.
- **Provider metadata elided**: `ICommandLineOptionsProvider` (display name, UID) is
  needed only for error messages; it can be abstracted away for the core arity logic.
- **Single-step focus**: the Lean model should target `ValidateOptionsArgumentArity`
  in isolation, treating `parseResult.Options` as an arbitrary `List` of
  `(name, argCount)` pairs and the arity map as a `HashMap Name ArgumentArity`.

## Proposed Lean Theorems

1. **`valid_iff_all_in_range`**: The result is `Valid` iff for all `(n, T)` in the
   grouped input, `arity(n).Min ≤ T ≤ arity(n).Max`.
2. **`invalid_if_flag_has_args`**: If some option has `Max = 0` and `T > 0`, the result
   is `Invalid`.
3. **`invalid_if_below_min`**: If some option has `T < Min`, the result is `Invalid`.
4. **`invalid_if_above_max`**: If some option has `T > Max` (and `Max > 0`), the result
   is `Invalid`.
5. **`errors_independent`**: The validity of one option's check does not affect
   the error reported for a different option (all violations are reported).
6. **`empty_input_is_valid`**: With no options, the result is always `Valid`.
7. **`zeroOrMore_always_valid`**: For any option with `Min = 0` and `Max = ∞`, any
   argument count is valid.
