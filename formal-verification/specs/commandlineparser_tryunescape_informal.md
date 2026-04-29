# Informal Specification — `CommandLineParser.TryUnescape`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Type**: `static bool TryUnescape(string input, string? option, IEnvironment environment, out string? unescapedArg, out string? error)`
- **Namespace**: `Microsoft.Testing.Platform.CommandLine`
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs`
- **Phase**: 2 — Informal Spec
- **Visibility**: `private static` inner function of `CommandLineParser.Parse`
- **Reference**: POSIX shell quoting rules — https://pubs.opengroup.org/onlinepubs/9699919799/utilities/V3_chap02.html#tag_18_02_03

---

## Purpose

`TryUnescape` converts a raw command-line argument string (which may be enclosed in single or double quotes) into an unescaped string suitable for consumption by the option handler. It implements a subset of POSIX shell quoting:

- **Single-quoted** strings have all characters preserved literally (no backslash escaping). A literal single quote inside single quotes is not permitted.
- **Double-quoted** strings allow a small set of backslash escape sequences (`\\`, `\"`, `\$`, `` \` ``, `\<newline>`). Other backslash sequences are **not** consumed (backslash is passed through unchanged for unrecognised sequences — because the implementation uses independent `Replace` calls for each escape, not a state machine).
- **Unquoted** strings are returned unchanged.

---

## Signature and Parameters

```csharp
static bool TryUnescape(
    string input,           // raw argument token (after optional leading/trailing whitespace trim by caller)
    string? option,         // option name for diagnostic messages (null if argument is free-standing)
    IEnvironment environment, // provides Environment.NewLine
    [NotNullWhen(true)]  out string? unescapedArg,  // set on success
    [NotNullWhen(false)] out string? error           // set on failure
)
```

The function is called by the outer `Parse` loop on an already-trimmed token, after `ParseOptionAndSeparators` has split `--opt=val` into `(opt, val)`.

---

## Preconditions

1. `input` is not null.
2. `environment` is not null; `environment.NewLine` is `"\n"` (Unix) or `"\r\n"` (Windows).
3. `option`, when non-null, is a trimmed option name without leading dashes (e.g., `"option1"`).

---

## Postconditions

### Success case (returns `true`)

- `unescapedArg` is not null.
- `error` is null.

#### Mode 1 — No surrounding quotes

- Applies when `input` does **not** start with `'` or `"`.
- `unescapedArg == input` (identity — no transformation applied).

#### Mode 2 — Single-quoted (`'...'`)

- Applies when `input.StartsWith("'") && input.EndsWith("'")`.
- Succeeds when no `'` character appears at any position `i` with `1 <= i <= input.Length - 2` (i.e., no single quote inside the outer quotes).
- `unescapedArg == input[1..^1]` (outer quotes stripped, inner characters unchanged).

#### Mode 3 — Double-quoted (`"..."`)

- Applies when `input.StartsWith("\"") && input.EndsWith("\"")`.
- Always succeeds (no error path in this mode).
- `unescapedArg` is obtained from `input[1..^1]` by applying the following substitutions **in order**, each applied globally:
  1. `"\\\\"` → `"\\"`  (two backslashes → one backslash)
  2. `"\\\""` → `"\""` (backslash-quote → double-quote)
  3. `"\\$"` → `"$"`    (backslash-dollar → dollar)
  4. `` "\\`" `` → `` "`" `` (backslash-backtick → backtick)
  5. `"\\" + environment.NewLine` → `environment.NewLine` (backslash-newline → newline)

### Failure case (returns `false`)

- `error` is not null.
- `unescapedArg` is null (caller must not read it — `[NotNullWhen(true)]`).
- Only occurs in **Mode 2** (single-quoted), when an inner single quote is detected.
- Error message format:
  - `option == null`: `"Unexpected single quote in argument: {input}"`
  - `option != null`: `"Unexpected single quote in argument: {input} for option '--{option}'"`

---

## Properties to Verify

### Group 1 — Unquoted passthrough

1. **Identity**: If `input` does not start with `'` or `"`, then `TryUnescape(input, ...) == (true, input, null)`.

### Group 2 — Single-quote mode

2. **Strip-outer**: If `input = '\'' + body + '\''` and `body` contains no `'`, then `TryUnescape(input, ...) == (true, body, null)`.
3. **Reject-inner-quote**: If `input = '\'' + body + '\''` and `body` contains `'`, then `TryUnescape(input, ...) == (false, null, error)` for some non-null `error`.
4. **Error-message-no-option**: If `option == null` and the function fails in Mode 2, then `error == "Unexpected single quote in argument: " + input`.
5. **Error-message-with-option**: If `option == "opt"` and the function fails in Mode 2, then `error == "Unexpected single quote in argument: " + input + " for option '--opt'"`.
6. **Empty single-quoted**: `TryUnescape("''", ...) == (true, "", null)`.

### Group 3 — Double-quote mode

7. **Strip-outer**: If `input = '"' + body + '"'` (with no unescaping needed in body), then `unescapedArg == body`.
8. **Backslash-backslash**: `input = "\"\\\\\"" ` → `unescapedArg = "\\"`.
9. **Backslash-doublequote**: `input = "\"\\\"\""` → `unescapedArg = "\""`.
10. **Backslash-dollar**: `input = "\"\\$\""` → `unescapedArg = "$"`.
11. **Backslash-backtick**: Input `"\"`\\`\`\"" ` → `unescapedArg = "`"`.
12. **Backslash-newline**: `input = "\"\\<NL>\""` → `unescapedArg = "<NL>"` where `<NL>` is `environment.NewLine`.
13. **Unrecognised escape passthrough**: A backslash followed by a character not in `{\, ", $, `, newline}` is passed through unchanged (because no Replace handles it).
14. **Always succeeds**: Mode 3 never returns `false`.
15. **Empty double-quoted**: `TryUnescape("\"\"", ...) == (true, "", null)`.

### Group 4 — Overlap / edge cases

16. **Single char `'`**: A string equal to exactly `'\''` (length 1) — does this match single-quote mode? The `StartsWith("'")` and `EndsWith("'")` checks both succeed on a single-char string, but `IndexOf('\'', 1, input.Length - 2)` would be searching with `length = -1`, which throws. **Potential edge case — see Open Questions.**
17. **Single char `"`**: Similarly, `"\""` (length 1) would match double-quote mode and strip both ends to yield empty string — possibly fine (it produces `input[1..^1]` which for length-1 string is `""`).
18. **Quote mismatch**: A string starting with `'` but ending with `"` (or vice versa) is **not** treated as a quoted string; it falls through to the unquoted passthrough path.

---

## Invariants

1. The return value is always consistent with the out parameters: `true ↔ unescapedArg != null`, `false ↔ error != null`.
2. Modes are mutually exclusive and cover all inputs (unquoted is the fallback).
3. Only Mode 2 can return `false`.
4. The double-quote escape substitutions are applied in a fixed order using `String.Replace`, not a state machine. This means:
   - `\\"` (backslash-backslash-quote) is first reduced to `\"` by rule (1), then `\"` is reduced to `"` by rule (2) — net result: `"`. This is correct.
   - Sequences are not context-sensitive; each pass is a global replace.

---

## Edge Cases

| Input | Expected outcome |
|-------|-----------------|
| `""` (empty string) | Mode: unquoted passthrough → `unescapedArg = ""`, success |
| `"'a'"` | Mode: single-quoted → `unescapedArg = "a"`, success |
| `"'a'b'"` | Mode: single-quoted, inner quote at position 2 → fail |
| `"''"` | Mode: single-quoted, empty body → `unescapedArg = ""`, success |
| `"\"\\\\\""` | Mode: double-quoted, body `"\\\\"` → `"\\"` after first replace → success |
| `"hello"` (unquoted, no wrapping quotes) | Passthrough → `unescapedArg = "hello"`, success |
| `"'a"` (single quote at start only) | Unquoted passthrough (doesn't end with `'`) → identity |
| `"a'"` (single quote at end only) | Unquoted passthrough (doesn't start with `'`) → identity |
| `"'"` (exactly one `'`) | **Potential bug** — see Open Questions |

---

## Open Questions for Lean Formalisation

1. **Single-character `'` (length 1)**: `input = "'"` matches both `StartsWith("'")` and `EndsWith("'")`, then calls `input.IndexOf('\'', 1, input.Length - 2)` = `input.IndexOf('\'', 1, -1)`. In .NET, `IndexOf(char, int, int)` with `count < 0` throws `ArgumentOutOfRangeException`. This is a **potential bug**: no test covers this case. The Lean model should flag this.

2. **Order-dependency of double-quote replacements**: Should the spec model the sequential `Replace` strategy explicitly, or describe the intended (denotational) meaning? The sequential strategy produces `\\"` → `"` (correct), but also means `"\\\\$"` → `"\\$"` → `"\$"` (two backslashes followed by dollar becomes just dollar, losing the leading backslash). Is this the intended behaviour?

3. **`environment.NewLine` abstraction**: Should the Lean model be parameterised over a `newline : String` variable, or instantiated with `"\n"`? Parameterisation is cleaner.

4. **`\<newline>` vs `\n`**: The implementation uses `environment.NewLine` which may be `"\r\n"` on Windows. The spec should be clear about this. The Lean formalisation should abstract over it.

5. **Unrecognised escapes in double-quote mode**: The implementation passes them through unchanged (because it only runs specific replaces). Is this deliberate or an oversight? The POSIX spec says only `$`, `` ` ``, `"`, `\`, and `<newline>` have special meaning after backslash inside double-quotes; other backslash sequences are "implementation-defined". The current implementation effectively keeps them (which is reasonable).

6. **`input.Trim()` is done by the caller**: The function receives a pre-trimmed argument. The formal spec should note this precondition.

---

## Approximations for Lean Model

- Model `string` as Lean `String` (UTF-16 character list or `List Char`).
- Model `String.Replace` as a global substitution function.
- Abstract `IEnvironment` to a parameter `newline : String`.
- Do NOT model exception paths (e.g., potential bug with `'"'` of length 1).
- The three modes can be formalised as a three-case conditional:
  ```
  TryUnescape(input, option, newline) :=
    if isSingleQuoted(input) then
      if hasInnerSingleQuote(input) then
        Fail(errorMsg(option, input))
      else
        Ok(stripOuter(input))
    else if isDoubleQuoted(input) then
      Ok(doubleUnquote(stripOuter(input), newline))
    else
      Ok(input)
  ```
- `isSingleQuoted(s)` ≡ `s.head = '\''  ∧  s.last = '\''`
- `isDoubleQuoted(s)` ≡ `s.head = '"'   ∧  s.last = '"'`
- `hasInnerSingleQuote(s)` ≡ `∃ i ∈ [1, s.length-2), s[i] = '\''`
- `stripOuter(s)` ≡ `s[1..s.length-1]` (i.e., `s[1..^1]` in C#)
- `doubleUnquote(body, newline)` ≡ sequential global replacements as listed in Property Group 3.

---

## Inferred Design Intent

`TryUnescape` implements a minimal subset of POSIX shell quoting, sufficient for command-line option argument processing. It deliberately does not support:
- Backslash escaping outside double-quoted mode.
- `$`-expansion or backtick command substitution.
- Mixed quoting (e.g., `'can'"'"'t`).

The double-quote escape list (`\\`, `\"`, `\$`, `` \` ``, `\<newline>`) matches the POSIX standard exactly. The single-quote behaviour (no escaping at all) also matches POSIX. This is a well-understood and standard specification, making it a strong FV target.
