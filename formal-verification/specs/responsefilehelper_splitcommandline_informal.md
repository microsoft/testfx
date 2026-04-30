# Informal Specification — `ResponseFileHelper.SplitCommandLine`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Method**: `public static IEnumerable<string> SplitCommandLine(string commandLine)`
- **Class**: `ResponseFileHelper` (internal static class)
- **Namespace**: `Microsoft.Testing.Platform` (internal)
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs`
- **Phase**: 2 — Informal Spec
- **Upstream reference**: [dotnet/command-line-api `StringExtensions.cs`](https://github.com/dotnet/command-line-api/blob/feb61c7f328a2401d74f4317b39d02126cfdfe24/src/System.CommandLine/Parsing/StringExtensions.cs#L349)

---

## Purpose

`SplitCommandLine` tokenises a single command-line string into a sequence of argument strings. It is used when reading response files (`@file` arguments): each line of the response file is first stripped of leading/trailing whitespace and `#`-prefixed comment lines (in the caller `SplitLine`), then passed to `SplitCommandLine` to be tokenised.

The function handles two categories of characters:
- **Whitespace** — token delimiter outside of quoted regions
- **Double-quote** (`"`) — begins and ends quoted regions where whitespace is literal

---

## Data Model

**Input**: a single `string commandLine`

**Output**: an `IEnumerable<string>` — a lazy sequence of token strings

**Internal state machine** (two independent variables):

| Variable | Enum | Values | Meaning |
|---|---|---|---|
| `seeking` | `Boundary` | `TokenStart`, `WordEnd` | whether we are looking for the start of a new token or for the end of the current token |
| `seekingQuote` | `Boundary` | `QuoteStart`, `QuoteEnd` | whether we are outside (`QuoteStart`) or inside (`QuoteEnd`) a double-quoted region |

Initial state: `(TokenStart, QuoteStart)` — not in a word, not in a quote.

**Token accumulation**: `startTokenIndex` is the start of the current token in the input string. The function accumulates characters from `startTokenIndex` to `pos`. On yield, it emits `commandLine[startTokenIndex .. pos].Replace("\"", "")`.

---

## Transition Rules (per character)

### Whitespace character (space, tab, etc.)

| `seeking` | `seekingQuote` | Action |
|---|---|---|
| `WordEnd` | `QuoteStart` | Yield current token; `startTokenIndex = pos`; `seeking = TokenStart` |
| `TokenStart` | `QuoteStart` | `startTokenIndex = pos` (advance past whitespace) |
| any | `QuoteEnd` | Literal character — part of current quoted token; no state change |

### Double-quote character (`"`)

| `seeking` | `seekingQuote` | Action |
|---|---|---|
| `TokenStart` | `QuoteStart` | Enter quoted region: `startTokenIndex = pos + 1`; `seekingQuote = QuoteEnd` |
| `TokenStart` | `QuoteEnd` | Close quoted region: yield current token; `startTokenIndex = pos`; `seekingQuote = QuoteStart` |
| `WordEnd` | `QuoteStart` | Toggle into quote mid-word: `seekingQuote = QuoteEnd` |
| `WordEnd` | `QuoteEnd` | Toggle out of quote mid-word: `seekingQuote = QuoteStart` |

### Any other character (non-whitespace, non-quote)

| `seeking` | `seekingQuote` | Action |
|---|---|---|
| `TokenStart` | `QuoteStart` | Start unquoted token: `seeking = WordEnd`; `startTokenIndex = pos` |
| other combinations | — | No state change (character is part of current token) |

### End of input

| `seeking` | Action |
|---|---|
| `TokenStart` | Nothing (no pending token) |
| `WordEnd` | Yield remaining token (`commandLine[startTokenIndex .. end].Replace("\"", "")`) |

Note: if `seeking == TokenStart` at end-of-input, even if `seekingQuote == QuoteEnd` (unclosed quote), **no token is yielded**. This is a potential bug/edge case — an unclosed quote starting right at end of input yields nothing.

---

## Preconditions

- `commandLine` is a non-null string (enforced by C# type system; no null-guard in method)
- The function is designed to receive a non-empty, non-comment line (callers pre-strip)
- Any string is a valid input (the function is total)

---

## Postconditions / Properties

### Property Group 1 — Trivial inputs

1. **Empty string**: `SplitCommandLine("") = []` (empty sequence)
2. **Whitespace-only**: `SplitCommandLine("   ") = []` (pure whitespace, no tokens)
3. **Single word**: `∀ w, (w contains no whitespace and no `"`) → SplitCommandLine(w) = [w]`
4. **Leading/trailing whitespace**: `SplitCommandLine("  hello  ") = ["hello"]`

### Property Group 2 — Whitespace splitting

5. **Whitespace delimiter**: `SplitCommandLine("a b") = ["a", "b"]`
6. **Multiple spaces treated as one delimiter**: `SplitCommandLine("a   b") = ["a", "b"]`
7. **N words**: a string of N whitespace-separated non-empty words (no quotes) yields exactly N tokens
8. **Tabs split identically to spaces**: `char.IsWhiteSpace` handles tabs, newlines, etc.

### Property Group 3 — Quote handling

9. **Quoted grouping**: `SplitCommandLine("\"a b\"") = ["a b"]` (space inside quotes is literal)
10. **Quote stripping**: output tokens never contain `"` characters
    - `∀ s, ∀ token ∈ SplitCommandLine(s), token.IndexOf('"') == -1`
11. **Empty quoted string**: `SplitCommandLine("\"\"") = [""]` (yields one empty token)
12. **Quoted whitespace preserved**: `SplitCommandLine("\" \"") = [" "]` (space inside quotes is a token)
13. **Adjacent quoted segments merge**: `SplitCommandLine("\"a\"\"b\"") = ["a", "b"]` — NOTE: separate yields because closing `"` triggers a yield when `seeking == TokenStart`
14. **Mid-word quote embedding**: `SplitCommandLine("abc\"def\"ghi") = ["abcdefghi"]` (quote stripped, no split)
15. **Unquoted + quoted merge (open token)**: `SplitCommandLine("abc\"def\"") = ["abcdef"]`
16. **Quoted then unquoted**: `SplitCommandLine("\"abc\"def") = ["abc", "def"]`
    - The closing `"` yields the quoted token "abc", then 'd' starts a new unquoted token

### Property Group 4 — Structural invariants

17. **No quotes in output**: all output tokens have `token.IndexOf('"') == -1`
18. **Token non-emptiness from unquoted text**: a token started by a non-whitespace, non-quote character is non-empty
19. **Empty token only from empty quotes**: the only way to emit an empty string token is via `""`
20. **Determinism**: the function is pure (no side effects, no random or I/O) — same input always yields same output
21. **Output count ≥ 0**: the result is always a non-negative number of tokens

### Property Group 5 — Composition

22. **Single word round-trip (no quotes)**: if `w` has no whitespace and no `"`, then `SplitCommandLine(w) = [w]`
23. **Concatenation with space**: `SplitCommandLine(a + " " + b)` where `a, b` have no whitespace/quotes → `[a, b]`

---

## Edge Cases

| Input | Expected output | Notes |
|-------|----------------|-------|
| `""` | `[]` | Empty string; loop never executes |
| `"   "` | `[]` | Whitespace-only |
| `"a"` | `["a"]` | Single character |
| `"a b c"` | `["a", "b", "c"]` | Multiple words |
| `"\"hello world\""` | `["hello world"]` | Quoted with space |
| `"\"\""` | `[""]` | Empty quoted string |
| `"\" \""` | `[" "]` | Quoted single space |
| `"abc\"def\"ghi"` | `["abcdefghi"]` | Embedded quote in word |
| `"\"abc\"def"` | `["abc", "def"]` | Closing quote mid-string creates two tokens |
| `"\"abc\" \"def\""` | `["abc", "def"]` | Two quoted words |
| `"\"abc\""` (unclosed: `"abc`) | `[]` | Unclosed quote, `seeking==TokenStart` at end → no token emitted! |
| `"abc\""` (unclosed: `abc"`) | `["abc"]` | Word started before quote; toggle to QuoteEnd mid-word; end yields word |
| `"#comment"` | `["#comment"]` | Comments are NOT stripped by this method (handled by caller) |
| `"\t\ta\tb"` | `["a", "b"]` | Tab characters as delimiters |

---

## Confirmed Design Properties

1. The function is a **tokeniser** (lexer), not a parser. It produces flat token sequences.
2. It handles **at most one level** of quoting — there is no escape sequence support (no `\"` inside a quoted string).
3. Quotes are **stripped** from output — the consumer sees unquoted text.
4. The function is **lazy** (`IEnumerable<string>` with `yield return`) — tokens are produced on demand.
5. The function calls `string.Replace("\"", "")` on **each token substring** to strip quotes. This means even edge cases like `"a\"b"` (unmatched quote in word) have quotes stripped.

---

## Potential Issues / Open Questions

### Issue 1 — No escape sequence support

There is no way to include a literal `"` in a token. This is by design (matches the upstream command-line-api implementation), but it means the tokeniser is not a full POSIX shell tokeniser.

### Issue 2 — Unclosed quote at start of input

`SplitCommandLine("\"abc")` (opening `"` but no closing `"`):
- pos=0: `"` → seeking==TokenStart, seekingQuote==QuoteStart → `startTokenIndex=1`, `seekingQuote=QuoteEnd`
- pos=1,2,3: 'a','b','c' → no action (not whitespace, not `"`, and seeking==TokenStart so unquoted-char rule doesn't fire)
- End of input: `seeking == TokenStart` → no yield

**Result**: `[]` — the entire input is silently discarded! This is a potential correctness issue: an unclosed quoted string is treated as if it were empty.

### Issue 3 — Unclosed quote mid-word

`SplitCommandLine("abc\"def")`:
- pos=0: 'a' → seeking=WordEnd, startTokenIndex=0
- pos=1,2: 'b','c'
- pos=3: `"` → seeking==WordEnd → toggles to QuoteEnd
- pos=4,5,6: 'd','e','f' → no action
- End: seeking==WordEnd → yield Substring(0,7).Replace(...)="abcdef"

**Result**: `["abcdef"]` — the unclosed quote is stripped, and content is included. This is probably correct for a response-file tokeniser, but the asymmetry with Issue 2 (opening quote at TokenStart) is notable.

### Issue 4 — `"foo""bar"` emits two tokens

`SplitCommandLine("\"foo\"\"bar\"")` → `["foo", "bar"]`

When the closing `"` of `"foo"` fires while `seeking==TokenStart`, it yields and resets. This means that two adjacent quoted strings are NOT concatenated; they are separate tokens. This contrasts with some shell implementations where `"foo""bar"` → `foobar`.

### Issue 5 — No unit tests in the test suite

The codebase has no tests for `SplitCommandLine` directly. The behaviour described above is derived purely from code analysis.

---

## Invariants for Lean Formalisation

1. **Quote-free output**: `∀ t ∈ result, '\"' ∉ t.toList`
2. **Empty input → empty result**: `splitCommandLine "" = []`
3. **Whitespace-free tokens from unquoted input**: if `commandLine` has no `"`, then each output token has no whitespace characters
4. **Quote grouping**: text between a matched `"..."` pair appears as a contiguous substring of a single token (though that token may have additional characters from surrounding unquoted text — only if `seeking == WordEnd` before the opening `"`)
5. **Token count stability**: repeated calls with the same input yield the same count and token sequence

---

## Approximations for Lean Model

- Model `char.IsWhiteSpace` as a decidable predicate `isWhitespace : Char → Bool` with `isWhitespace ' ' = true`, `isWhitespace '\t' = true`, `isWhitespace '\n' = true`, etc.
- Model strings as `List Char` for easier structural reasoning
- Model `IEnumerable<string>` as `List String` (finite; termination guaranteed since `pos` strictly increases each iteration)
- Exclude `Replace("\"", "")` subtlety from the state-machine model and handle it as a post-processing step
- The state machine can be modelled as a tail-recursive function with accumulator: `splitCL : String → State → String → List String`

---

## Inferred Design Intent

`SplitCommandLine` implements the minimal quoting semantics needed for response files: whitespace-delimited tokens with double-quote grouping, matching the upstream [dotnet/command-line-api](https://github.com/dotnet/command-line-api) implementation. It deliberately avoids backslash escaping to keep the tokeniser simple. The design favours simplicity over POSIX compliance.
