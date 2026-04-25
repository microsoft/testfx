# Informal Specification — `CommandLineParser.TryUnescape`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Function**: `static bool TryUnescape(string input, string? option, IEnvironment environment, out string? unescapedArg, out string? error)`
- **Container**: `CommandLineParser` (internal static class)
- **Namespace**: `Microsoft.Testing.Platform.CommandLine`
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs`
- **Phase**: 2 — Informal Spec
- **References**:
  - [POSIX quoting rules](https://pubs.opengroup.org/onlinepubs/9699919799/utilities/V3_chap02.html#tag_18_02_03)
  - [System.CommandLine syntax](https://learn.microsoft.com/dotnet/standard/commandline/syntax)
  - [MSVC C argument parsing](https://learn.microsoft.com/cpp/c-language/parsing-c-command-line-arguments)

---

## Purpose

`TryUnescape` is a pure function that takes a single command-line argument token (already trimmed) and produces its "logical value" by stripping surrounding quotes and applying backslash-escape rules. It returns `true` on success (`unescapedArg` is set, `error` is `null`) or `false` on failure (`error` is set, `unescapedArg` is `null`).

This function implements three distinct parsing modes:

| Mode | Trigger | Description |
|------|---------|-------------|
| **Single-quoted** | Input starts and ends with `'` | Literal: no escape sequences inside; inner single-quotes forbidden |
| **Double-quoted** | Input starts and ends with `"` | Backslash-escape sequences for `\\`, `\"`, `\$`, `` \` ``, `\<newline>` |
| **Unquoted** | Neither of the above | Pass-through: returned verbatim |

---

## Signature

```csharp
static bool TryUnescape(
    string input,
    string? option,           // for error messages only; no effect on parsing
    IEnvironment environment, // provides NewLine string for double-quote escape
    [NotNullWhen(true)]  out string? unescapedArg,
    [NotNullWhen(false)] out string? error)
```

---

## Preconditions

- `input` is a non-null `string` (guaranteed by callers, which pass `currentArg.Trim()`).
- `option` is the name of the current option, used only in error message formatting; `null` if called without an option context.
- `environment.NewLine` is the system's newline string (typically `"\r\n"` on Windows or `"\n"` on Linux).
- There are **no other preconditions** documented — any non-null input is accepted.

---

## Postconditions / Properties

### Property Group 1 — Return convention

1. **Success implies populated result**: `TryUnescape(...) == true` ⟹ `unescapedArg ≠ null ∧ error == null`
2. **Failure implies error set**: `TryUnescape(...) == false` ⟹ `error ≠ null ∧ unescapedArg == null`
3. **Total**: `TryUnescape` always returns either `true` or `false`; it never throws (see **Bug: length-1 edge cases** below).

---

### Property Group 2 — Unquoted mode (pass-through)

An input that does **not** start and end with `'` or `"` is returned unchanged.

4. **Unquoted pass-through**: If `input` does not start with `'` and does not start with `"`, then `TryUnescape` returns `true` with `unescapedArg == input`.
5. **Unquoted pass-through (double-quote terminal only)**: If `input` starts with `"` but does not end with `"`, then `TryUnescape` returns `true` with `unescapedArg == input`.
6. **Unquoted pass-through (double-quote leading only)**: If `input` starts with `'` but does not end with `'`, then `TryUnescape` returns `true` with `unescapedArg == input`.

---

### Property Group 3 — Single-quoted mode

Single-quoted mode: both first and last characters are `'`.

7. **Happy path**: If `input` starts and ends with `'`, and `input[1..(len-2)]` contains no `'`, then `TryUnescape` returns `true` with `unescapedArg == input[1..^1]` (outer quotes stripped, interior returned literally).
8. **Empty single-quoted string**: `TryUnescape("''", ...)` returns `true` with `unescapedArg == ""`.
9. **Interior quote rejected**: If `input` starts and ends with `'`, and `input[1..(len-2)]` contains at least one `'`, then `TryUnescape` returns `false` with a non-null `error` message.
10. **Error message includes input**: When failing due to interior `'`, the `error` message contains `input`.
11. **Error message includes option name when option is non-null**: When failing and `option ≠ null`, the `error` message contains `option`.
12. **No backslash expansion in single-quoted mode**: After stripping outer quotes, `\` characters are returned literally. E.g., `TryUnescape(@"'\\'", ...)` returns `"\\"` (two chars), not `"\"` (one char).

---

### Property Group 4 — Double-quoted mode

Double-quoted mode: both first and last characters are `"`.

13. **Outer quotes stripped**: If `input` starts and ends with `"`, then `unescapedArg` is derived from `input[1..^1]` with escape sequences applied.
14. **Empty double-quoted string**: `TryUnescape(@"""""", ...)` returns `true` with `unescapedArg == ""`.
15. **Backslash-backslash → backslash**: `\\` in the interior is replaced by `\` (applied first, left-to-right).
16. **Backslash-quote → quote**: `\"` in the interior is replaced by `"`.
17. **Backslash-dollar → dollar**: `\$` in the interior is replaced by `$`.
18. **Backslash-backtick → backtick**: `` \` `` in the interior is replaced by `` ` ``.
19. **Backslash-newline → newline**: `\<NewLine>` in the interior is replaced by the platform newline string. The newline value comes from `environment.NewLine`.
20. **All other backslash sequences are literal**: E.g., `\n`, `\t`, `\x` are returned unchanged (the `\` is retained).
21. **Double-quoted mode never fails**: `TryUnescape` always returns `true` when in double-quoted mode. There is no input that triggers an error in this mode.
22. **Escape replacement order matters**: Replacements are applied as `Replace(@"\\", "\\").Replace(@"\""", "\"").Replace(@"\$", "$").Replace(@"\`", "`").Replace($@"\{NewLine}", NewLine)` — sequential string replacement (not regex). Characters matched by an earlier replacement cannot be re-matched by a later one.

---

### Property Group 5 — Idempotency & non-interference

23. **No option-name effect on result**: The `option` parameter affects only the error message, not the return value or `unescapedArg`.
24. **Trim is caller responsibility**: The caller passes `currentArg.Trim()`; `TryUnescape` does not trim internally. Leading/trailing whitespace in `input` is significant.

---

## Edge Cases

### EC-1 — Length-1 single-quote: `input == "'"`

**Observed behaviour**: **Crash** — `ArgumentOutOfRangeException`.

**Root cause**: The implementation enters the single-quote branch when `input.StartsWith("'") && input.EndsWith("'")`, which is true for any length-1 string starting with `'`. It then calls `input.IndexOf('\'', 1, input.Length - 2)`, which becomes `input.IndexOf('\'', 1, -1)`. Passing a negative count to `IndexOf(char, int, int)` throws `ArgumentOutOfRangeException`.

**Expected behaviour** (by spec intent): A length-1 `'` would mean the closing quote is the same character as the opening quote. This should arguably be treated as an empty-body single-quoted string `""` (returning `true` with `""`) or rejected as a malformed quote with an error. Currently it crashes.

**This is a confirmed bug** — there is no existing test covering this case.

### EC-2 — Length-1 double-quote: `input == "\""`

**Observed behaviour**: **Crash** — `ArgumentOutOfRangeException` or `IndexOutOfRangeException`.

**Root cause**: The implementation enters the double-quote branch when `input.StartsWith("\"") && input.EndsWith("\"")`, which is true for length-1 `"`. It then evaluates `input[1..^1]` = `input[1..0]` (since `^1` = `length-1` = 0). A range where start=1 > end=0 is invalid and throws.

**Expected behaviour** (by spec intent): Should return `true` with `unescapedArg == ""` (empty body), matching the `""` case.

**This is a confirmed bug** — no existing test covers this case.

### EC-3 — Unmatched leading `'` or `"` (e.g., `'hello`)

**Observed behaviour**: Pass-through — returned verbatim.

**Specification note**: The function does not detect unmatched quotes. `'hello` (starts with `'`, does not end with `'`) is treated as an unquoted argument and returned as-is. This is intentional per the implementation.

### EC-4 — Adjacent escape sequences (e.g., `"\\\"`)

**Observed behaviour**: `\\"` → `Replace(@"\\", "\\")` first converts `\\` to `\`, yielding `\"`, then `Replace(@"\""", "\"")` converts `\"` to `"`. Result: `"`.

**Specification note**: The order of replacements matters. `"\\\""` (raw: `\\"`) yields `"` (single double-quote char).

### EC-5 — Non-ASCII characters

**Observed behaviour**: Pass-through in unquoted mode; literal preservation in single-quoted mode; subject to replacement rules in double-quoted mode only for the specific escape sequences.

### EC-6 — Empty string `""`

**Observed behaviour**: Unquoted pass-through — returned as `""` (empty string). Does not enter single-quote or double-quote branch.

---

## Inferred Intent

The function is designed to implement a **subset of POSIX shell quoting semantics** for command-line argument processing:

1. **Single-quotes**: Strictest mode — no special characters at all inside. The only restriction is that single-quotes cannot appear within single-quoted strings, matching POSIX exactly.
2. **Double-quotes**: Moderate mode — most characters literal, but backslash-escape sequences for `\`, `"`, `$`, `` ` ``, and newline. Notably, `$` and `` ` `` escape sequences are handled syntactically even though the runtime does **not** perform parameter expansion or command substitution.
3. **Unquoted**: The argument is passed directly — the tokenizer (shell or OS) has already split it.

The `environment.NewLine` injection point allows the double-quote backslash-newline escape to be platform-aware, but in practice `SystemEnvironment.NewLine` delegates to `Environment.NewLine`.

---

## Open Questions

1. **Bug fix for EC-1 and EC-2**: Should a length-1 `'` or `"` return `true` with empty body, or return `false` with an error? The least-surprise fix would be to add a length guard: `if (input.Length < 2)` before the `IndexOf` / slice.
2. **Replacement order and overlap**: Is it intentional that `\\"` becomes `"` (via two replacements)? This may conflict with user intent (`\\` followed by `"`). A regex-based approach or a state-machine scanner would be more correct.
3. **Unmatched quotes**: Should `'hello` or `hello"` produce an error rather than pass-through? Leaving them as unquoted may silently swallow user mistakes.
4. **`$` and `` ` `` handling**: Escaping `\$` and `` \` `` is done syntactically, but the runtime never expands `$var` or `` `cmd` ``. Is the escape handling dead code, or is it there for future extensibility?

---

## Examples

| Input | `option` | `unescapedArg` | `error` | Notes |
|-------|----------|----------------|---------|-------|
| `hello` | `option1` | `hello` | `null` | Unquoted pass-through |
| `'hello'` | `option1` | `hello` | `null` | Single-quoted, strip quotes |
| `''` | `option1` | `` | `null` | Single-quoted empty |
| `'a'b'` | `option1` | `null` | (error msg) | Interior `'` rejected |
| `"hello"` | `option1` | `hello` | `null` | Double-quoted, strip quotes |
| `""` | `option1` | `` | `null` | Double-quoted empty |
| `"\\"` | `option1` | `\` | `null` | Backslash-backslash |
| `"\""` | `option1` | `"` | `null` | Backslash-doublequote |
| `"\$"` | `option1` | `$` | `null` | Backslash-dollar |
| `'` | `option1` | *(crash)* | *(crash)* | **BUG: ArgumentOutOfRangeException** |
| `"` | `option1` | *(crash)* | *(crash)* | **BUG: Range out of bounds** |
| `hello"` | `option1` | `hello"` | `null` | Unmatched quote → pass-through |
| `'hello` | `option1` | `'hello` | `null` | Unmatched quote → pass-through |
