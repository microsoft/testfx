# Informal Specification — `EnvironmentVariableParser.ParseBool`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Function**: `static bool ParseBool(string? str, bool defaultValue)`
- **Container**: `private static class EnvironmentVariableParser` (nested inside `LLMEnvironmentDetector`)
- **Namespace**: `Microsoft.Testing.Platform.Helpers`
- **File**: `src/Platform/Microsoft.Testing.Platform/Helpers/LLMEnvironmentDetector.cs` (lines 91–114)
- **Phase**: 2 — Informal Spec

---

## Purpose

`ParseBool` converts an environment variable string value into a `bool`. It recognises a fixed set of canonical truthy and falsy string representations (case-insensitively) used across Unix and Windows conventions, and returns a caller-supplied default for unrecognised or absent values.

Its primary caller is `BooleanEnvironmentRule.IsMatch()`, which uses it to detect whether an environment variable is set to a truthy value to decide whether an AI-coding-agent is present.

---

## Signature

```csharp
private static bool ParseBool(string? str, bool defaultValue)
```

| Parameter      | Type      | Description                                               |
|---------------|-----------|-----------------------------------------------------------|
| `str`         | `string?` | Raw string from `Environment.GetEnvironmentVariable(…)`.  |
| `defaultValue`| `bool`    | Value returned when `str` is not a recognised token.      |
| **return**    | `bool`    | The parsed boolean, or `defaultValue` on no-match.        |

---

## Classification of Inputs

The function partitions all possible `string?` values into three mutually-exclusive, collectively-exhaustive sets:

### Truthy set `T`

A string `s` belongs to `T` if and only if one of the following holds (ordinal / case-insensitive):

| Token  | Notes                              |
|--------|------------------------------------|
| `"1"`  | Exact ordinal match (only one digit) |
| `"true"` | Case-insensitive                 |
| `"yes"`  | Case-insensitive                 |
| `"on"`   | Case-insensitive                 |

Membership: `s ∈ T  ⟺  s = "1" ∨ s ≅ "true" ∨ s ≅ "yes" ∨ s ≅ "on"`
(where `≅` means case-insensitive ordinal equality).

### Falsy set `F`

A string `s` belongs to `F` if and only if one of the following holds:

| Token    | Notes                              |
|----------|------------------------------------|
| `"0"`    | Exact ordinal match                |
| `"false"`| Case-insensitive                   |
| `"no"`   | Case-insensitive                   |
| `"off"`  | Case-insensitive                   |

Membership: `s ∈ F  ⟺  s = "0" ∨ s ≅ "false" ∨ s ≅ "no" ∨ s ≅ "off"`

### Default set `D`

`D = (string? \ T) \ F` — everything else, including `null`, the empty string `""`,
and any unrecognised token (e.g. `"2"`, `"yes please"`, `"TRUE1"`).

### Disjointness

`T` and `F` are disjoint: `T ∩ F = ∅`.

---

## Preconditions

None. The function accepts any `string?` value including `null`.

---

## Postconditions

| Condition on `str` | Return value       |
|--------------------|--------------------|
| `str ∈ T`          | `true`             |
| `str ∈ F`          | `false`            |
| `str ∈ D`          | `defaultValue`     |

Formally:

```
ParseBool(str, def) = true        ∀ str ∈ T
ParseBool(str, def) = false       ∀ str ∈ F
ParseBool(str, def) = def         ∀ str ∈ D
```

---

## Invariants

1. **Totality**: `ParseBool` is defined for every `(str, defaultValue)` pair without exceptions.
2. **Determinism**: Given the same `str` and `defaultValue`, the result is always the same.
3. **Partition**: Every `str` belongs to exactly one of `T`, `F`, `D`.
4. **Default independence**: For `str ∈ T` or `str ∈ F`, the result does not depend on `defaultValue`.
5. **Default identity**: For `str ∈ D`, the result equals `defaultValue` exactly.
6. **Null is default**: `null ∈ D`, so `ParseBool(null, def) = def`.
7. **Empty is default**: `"" ∈ D`, so `ParseBool("", def) = def`.

---

## Case-Insensitivity Details

The implementation uses `StringComparison.OrdinalIgnoreCase` for all comparisons except `"1"` and `"0"`, which use pattern matching (`str is "1"`, `str is "0"`) — effectively ordinal exact-match.

Consequence: `"TRUE"`, `"True"`, `"tRuE"` all map to `true`; `"FALSE"`, `"False"`, `"fAlSe"` all map to `false`.

The exact-match check for `"1"` means `"1.0"`, `" 1"`, `"01"` are all in `D` (not truthy).
Similarly `"0"` means `"0.0"`, `" 0"`, `"00"` are all in `D` (not falsy).

---

## Edge Cases

| `str` value   | Expected result                  | Rationale                                        |
|---------------|----------------------------------|--------------------------------------------------|
| `null`        | `defaultValue`                   | No variable set; unambiguously default.          |
| `""`          | `defaultValue`                   | Set but empty; unambiguously default.            |
| `"1"`         | `true`                           | Exact ordinal match.                             |
| `"0"`         | `false`                          | Exact ordinal match.                             |
| `"TRUE"`      | `true`                           | Case-insensitive.                                |
| `"False"`     | `false`                          | Case-insensitive.                                |
| `"YES"`       | `true`                           | Case-insensitive.                                |
| `"NO"`        | `false`                          | Case-insensitive.                                |
| `"ON"`        | `true`                           | Case-insensitive.                                |
| `"OFF"`       | `false`                          | Case-insensitive.                                |
| `"2"`         | `defaultValue`                   | Not in recognised set.                           |
| `"yes please"`| `defaultValue`                   | Not an exact (case-insensitive) match.           |
| `"true "`     | `defaultValue`                   | Trailing space — not a match.                    |
| `"01"`        | `defaultValue`                   | Ordinal mismatch with `"1"`.                     |
| `"00"`        | `defaultValue`                   | Ordinal mismatch with `"0"`.                     |

---

## Theorems of Interest (for Lean 4)

All of the following are decidable and can be proved by `decide` after defining the finite truthy/falsy token sets or by `simp` with the appropriate lemmas.

1. **Truthy completeness**: for all `s ∈ {"1","true","True","TRUE","yes","Yes","YES","on","On","ON"}`, `ParseBool s def = true`
2. **Falsy completeness**: for all `s ∈ {"0","false","False","FALSE","no","No","NO","off","Off","OFF"}`, `ParseBool s def = false`
3. **Disjointness**: no string belongs to both `T` and `F`
4. **Null → default**: `ParseBool null def = def`
5. **Empty → default**: `ParseBool "" def = def`
6. **Idempotency**: `ParseBool (toString (ParseBool str def)) def' = ParseBool str def` — *does not hold in general*; noted as an open question below.
7. **Default independence**: `∀ s ∈ T, ∀ def₁ def₂, ParseBool s def₁ = ParseBool s def₂`
8. **Default independence** (falsy): `∀ s ∈ F, ∀ def₁ def₂, ParseBool s def₁ = ParseBool s def₂`

---

## Open Questions

**OQ-1 (Idempotency)**: Is `ParseBool(ParseBool(str, def).ToString(), def')` equal to `ParseBool(str, def)`? The "round trip" would hold if `"True"` and `"False"` (the C# `bool.ToString()` output) are in `T` and `F` respectively. They are (case-insensitive match), so the round-trip *does* hold for all inputs. Worth formally proving.

**OQ-2 (Default symmetry)**: There is no symmetric function that maps `true`/`false`/default in a symmetric way — the function is not bijective. Is the one-way nature a design limitation or intentional?

**OQ-3 (Locale independence)**: The use of `StringComparison.OrdinalIgnoreCase` ensures the result is culture-independent. This is an important invariant for environment variable parsing. Worth documenting explicitly in the Lean model.

**OQ-4 (Token coverage)**: Are the eight recognised tokens (`1/0/true/false/yes/no/on/off`) the right set? Other conventions use `enabled`/`disabled`, `t`/`f`, `y`/`n`. The choice appears to be sourced from the dotnet/sdk telemetry module (the file comment says "Copy from https://github.com/dotnet/sdk/...").

---

## Inferred Design Intent

The function is a minimal, portable environment-variable boolean parser that handles the most common conventions across Unix (`1`/`0`, `true`/`false`, `yes`/`no`) and Windows (`on`/`off`). It is strictly case-insensitive for all string tokens, is total (never throws), and defers to the caller for unrecognised values.

The design deliberately avoids:
- Numeric parsing beyond `"1"` and `"0"` (e.g., no `int.TryParse`)
- Trimming whitespace (so `" true"` is rejected)
- Culture-sensitive comparison

---

## Approximation Notes

A Lean model will represent `string?` as `Option String` and `StringComparison.OrdinalIgnoreCase` equality as case-folding to lowercase. The eight recognised tokens are finite, so the model needs no further approximation.

The full C# `string` type is approximated by Lean's `String` type (UTF-16 vs. UTF-8 difference); this does not affect the logic since all recognised tokens are ASCII.
