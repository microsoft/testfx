# Informal Specification — `CommandLineParser.ParseOptionAndSeparators`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Type**: `static void ParseOptionAndSeparators(string arg, out string? currentOption, out string? currentArg)` (local function inside `CommandLineParser.Parse`)
- **Namespace**: `Microsoft.Testing.Platform.CommandLine`
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs`
- **Phase**: 2 — Informal Spec
- **Related spec**: `commandlineparser_tryunescape_informal.md`

---

## Purpose

`ParseOptionAndSeparators` splits a single command-line token that starts with `-` or `--` into:

1. An **option name** (`currentOption`) — the part before the first `:` or `=` delimiter, with leading dashes stripped.
2. An **inline value** (`currentArg`) — the part after the delimiter, or `null` if no delimiter is present.

This implements the convention that allows options to be supplied in two styles:

- **Separate token style**: `--option value` — handled by the caller; `ParseOptionAndSeparators` gets `"--option"` and returns `("option", null)`.
- **Inline style**: `--option=value` or `--option:value` — `ParseOptionAndSeparators` gets the combined token and splits it.

The function is a local `static` method: it accesses no state, has no side effects, and is a pure string transformation.

---

## Data Model

```
ParseOptionAndSeparators : string → (string × string?)
ParseOptionAndSeparators(arg) = (currentOption, currentArg)
```

where:

- `currentOption : string` — never null; contains no leading `-` characters
- `currentArg : string?` — null if no delimiter; possibly empty string `""` if delimiter is the last character

---

## Preconditions

1. `arg` is not null (enforced by the caller's loop variable).
2. `arg` has been validated by the caller to match one of:
   - **Single-dash form**: `arg.Length > 1 ∧ arg[0] = '-' ∧ arg[1] ≠ '-'`
   - **Double-dash form**: `arg.Length > 2 ∧ arg[0] = '-' ∧ arg[1] = '-' ∧ arg[2] ≠ '-'`
   
   In both cases, `arg` starts with `-` but does NOT start with `---`.

3. `arg.Length ≥ 2` by the above preconditions.

> **Note**: The function itself does not check these preconditions. It accepts any non-null `string` as input. Callers outside `Parse` may pass unusual inputs.

---

## Algorithm (Reference Implementation)

```csharp
(currentOption, currentArg) = arg.IndexOfAny([':', '=']) switch
{
    -1 => (arg, null),
    var delimiterIndex => (arg[..delimiterIndex], arg[(delimiterIndex + 1)..]),
};
currentOption = currentOption.TrimStart('-');
```

Steps:
1. Find `delimiterIndex = arg.IndexOfAny([':', '='])` — the 0-based index of the FIRST occurrence of `:` or `=` in `arg` (or −1 if absent).
2. **No delimiter** (`delimiterIndex = -1`): `currentOption := arg`, `currentArg := null`.
3. **Delimiter found** (`delimiterIndex ≥ 0`): `currentOption := arg[0..delimiterIndex)`, `currentArg := arg[delimiterIndex+1..]`.
4. Strip leading dashes: `currentOption := currentOption.TrimStart('-')`.

---

## Postconditions / Properties

### Property Group 1 — Delimiter-free case

1. **No delimiter → null arg**: If `arg` contains no `:` and no `=`, then `currentArg = null`.
2. **No delimiter → full option**: If `arg` contains no `:` and no `=`, then `currentOption = arg.TrimStart('-')`.
3. **No dashes in option**: `currentOption` has no leading `-` in all cases (TrimStart removes them).

### Property Group 2 — Delimiter present case

4. **Delimiter → non-null arg**: If `arg` contains `:` or `=`, then `currentArg ≠ null`.
5. **First delimiter only**: Let `i = arg.IndexOfAny([':', '='])`. Then `currentOption = arg[..i].TrimStart('-')` and `currentArg = arg[(i+1)..]`.
6. **Value may be empty**: `currentArg` may be `""` when the delimiter is the last character (`i = arg.Length - 1`).
7. **Value preserves subsequent delimiters**: Characters after the first delimiter are preserved verbatim in `currentArg`. In particular, `currentArg` may itself contain `:` or `=`.

### Property Group 3 — Structural / Reconstructibility

8. **Option contains no delimiters**: `currentOption` contains no `:` and no `=` character. (Because we split at the first occurrence and then `TrimStart` only removes `-`.)
9. **Lossless split (with delimiter)**: If `delimiterIndex ≥ 0`, let `prefix = arg[..delimiterIndex]`. Then:
   - `currentOption = prefix.TrimStart('-')`
   - `currentArg = arg[(delimiterIndex + 1)..]`
   - `prefix = ('-' × k) + currentOption` for some `k ≥ 0` (exactly the number of leading dashes in `prefix`)
   - `arg = prefix + arg[delimiterIndex] + currentArg`
   - Therefore `arg.Length = prefix.Length + 1 + currentArg.Length`
10. **Lossless split (no delimiter)**: If `delimiterIndex = -1`, then:
    - `currentOption = arg.TrimStart('-')`
    - `arg = ('-' × k) + currentOption` for some `k ≥ 0`

### Property Group 4 — Determinism and independence

11. **Purity**: `ParseOptionAndSeparators` is a pure function — same input always yields same output.
12. **Delimiter priority is lexicographic order**: `IndexOfAny` finds the leftmost occurrence of either delimiter character. The type of delimiter (`:` vs `=`) is irrelevant; only position matters.
13. **Which delimiter came first determines the split**: If `arg = "x=y:z"`, then `currentOption = "x"` and `currentArg = "y:z"`. If `arg = "x:y=z"`, then `currentOption = "x"` and `currentArg = "y=z"`.

### Property Group 5 — Edge cases

14. **Option can be empty**: If `arg` starts with `:` or `=` (violates preconditions but valid input to function), then `currentOption = ""`.
15. **Option prefix is entirely dashes**: If `arg = "--:value"` (double-dash immediately followed by delimiter), `currentOption = ""` (all dashes stripped), `currentArg = "value"`.
16. **Delimiter immediately after prefix**: If `arg = "--opt="` (delimiter is last char), `currentOption = "opt"`, `currentArg = ""`.

---

## Edge Cases

| Input `arg` | `currentOption` | `currentArg` | Notes |
|-------------|-----------------|--------------|-------|
| `"--option1"` | `"option1"` | `null` | Standard no-value form |
| `"-option1"` | `"option1"` | `null` | Single-dash form |
| `"--option1:a"` | `"option1"` | `"a"` | Colon delimiter |
| `"--option1=a"` | `"option1"` | `"a"` | Equals delimiter |
| `"--option1=a=a"` | `"option1"` | `"a=a"` | Second `=` preserved in value |
| `"--option1:a:a"` | `"option1"` | `"a:a"` | Second `:` preserved in value |
| `"--option1:a=a"` | `"option1"` | `"a=a"` | `:` wins (leftmost) |
| `"--option1=a:a"` | `"option1"` | `"a:a"` | `=` wins (leftmost) |
| `"--option1="` | `"option1"` | `""` | Empty value (delimiter at end) |
| `"--option1:"` | `"option1"` | `""` | Empty value (delimiter at end) |
| `"--:"` | `""` | `""` | Empty option AND empty value |
| `"-a"` | `"a"` | `null` | Single-dash, no delimiter |
| `"---option1"` | Not called (rejected by caller) | | Three dashes: caller produces error |

---

## Invariants

1. `currentOption` is never `null` — it is always a `string` (possibly empty).
2. `currentArg` is `null` if and only if no delimiter character appears in `arg`.
3. The function never throws (no index-out-of-bounds; `arg[..delimiterIndex]` with `delimiterIndex ≥ 0` is always valid; `arg[(delimiterIndex + 1)..]` when `delimiterIndex = arg.Length - 1` yields `""`).
4. `currentOption` contains no `:` and no `=`.
5. `currentOption` has no leading `-` characters.

---

## Inferred Design Intent

The function implements the `--key=value` and `--key:value` option-argument delimiter convention documented in `dotnet/command-line-api` and the [System.CommandLine syntax docs](https://learn.microsoft.com/dotnet/standard/commandline/syntax#option-argument-delimiters). The two-delimiter support (both `:` and `=`) mirrors the Windows cmd and .NET CLI conventions.

Only the FIRST delimiter counts — subsequent occurrences pass through verbatim into the value. This enables values like `--connection=Server=tcp:localhost,1433` (a connection string containing both `=` and `:`).

The `TrimStart('-')` is applied uniformly to the option-name prefix, normalizing both `-opt` and `--opt` forms to `opt`.

---

## Potential Issues / Open Questions

### Issue 1 — Empty option name not rejected
**Observation**: The function does not validate that `currentOption` is non-empty after `TrimStart('-')`. Inputs like `"--:value"` or `"--="` produce `currentOption = ""`, which is then treated as a valid option name by the caller. The caller does not re-validate.

**Consequence**: The parser silently creates a `CommandLineParseOption` with option name `""` instead of reporting an error. Attempting to look up this option in the registered options table will fail to find a match (but may produce an unhelpful error message downstream).

**Severity**: Minor / edge case. In practice, callers pass well-formed command lines. The preconditions exclude `--:` inputs via the `arg[2] != '-'` check only if we consider `:` as a non-dash character — which it is — but the preconditions do NOT exclude `--:` specifically.

**Recommendation**: Add a post-split guard: `if (string.IsNullOrEmpty(currentOption)) { errors.Add(...); currentOption = null; }`.

### Open Question 1 — Interaction with TryUnescape
`currentArg` (when non-null) is passed to `TryUnescape` after `.Trim()`. The `Trim()` strips whitespace. The interaction between the inline value and quoting/unescaping is not directly tested: does `--option='hello world'` work? According to the source, it should, because `currentArg = "'hello world'"` which is then unescaped by `TryUnescape`.

### Open Question 2 — Delimiter within quoted value
`"--option='a:b'"` produces `currentOption = "option"` and `currentArg = "'a:b'"`. The colon is inside quotes, but `ParseOptionAndSeparators` does NOT understand quoting — it splits on the first raw `:`. For `"--option:a"`, split occurs before seeing quotes. For `"--option='a:b'"`, the first `:` is index 9 (before `'a`), so `currentArg = "'a:b'"`. Wait — actually `"--option='a:b'"` has `=` at index 8, not `:` first. So `currentOption = "option"`, `currentArg = "'a:b'"`. This is correct. But `"--option:'a:b'"` would split at the first `:` (index 8), giving `currentOption = "option"` and `currentArg = "'a:b'"`. The second `:` is preserved in the value. This seems correct.

---

## Approximations for Lean Model

1. **Model strings as `List Char`** (or `String` which is `List Char` in Lean). Character comparisons are decidable.
2. **Model `TrimStart('-')`** as `List.dropWhile (· == '-')`. 
3. **Model `IndexOfAny([':', '='])`** as `List.findIdx? (fun c => c == ':' || c == '=')` (returns `Option Nat`).
4. **Model return value** as `String × Option String` (option name × optional inline value).
5. **Do NOT model**: threading, exception behaviour, null reference semantics, caller validation.
6. **Decidable propositions**: all properties in Groups 1–5 are decidable for concrete inputs; `decide` should close concrete test cases. The structural properties (Groups 3, 5) require induction on `List.dropWhile` and `List.findIdx?`.
7. **Key lemma to prove**: `List.findIdx? (fun c => c == ':' || c == '=') s = none ↔ ¬ ∃ c ∈ s, c == ':' || c == '='`.
8. **Key lemma 2**: If `findIdx? p s = some i`, then `s[i]` satisfies `p` and no `s[j]` with `j < i` satisfies `p`.

---

## Examples for Lean

```lean
-- No delimiter
#eval parseOptionAndSeparators "--option1"
-- Expected: ("option1", none)

-- Colon delimiter
#eval parseOptionAndSeparators "--option1:a"
-- Expected: ("option1", some "a")

-- Equals delimiter, value contains colon
#eval parseOptionAndSeparators "--option1=a:a"
-- Expected: ("option1", some "a:a")

-- Double equals, only first counts
#eval parseOptionAndSeparators "--option1=a=a"
-- Expected: ("option1", some "a=a")

-- Trailing delimiter → empty value
#eval parseOptionAndSeparators "--option1="
-- Expected: ("option1", some "")
```
