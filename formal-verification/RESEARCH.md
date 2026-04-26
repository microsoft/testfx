# Formal Verification Research

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Repository Overview

**Repository**: `microsoft/testfx`
**Primary Language**: C# (.NET)
**Codebase Components**:
- **MSTest** (`src/TestFramework/`): The MSTest v3 testing framework for .NET — assertion APIs, test attributes, data-driven tests.
- **Microsoft.Testing.Platform** (`src/Platform/Microsoft.Testing.Platform/`): A modern, extensible test runner platform — command-line parsing, server mode, extensions.
- **Adapters and Extensions** (`src/Adapter/`, `src/Platform/Microsoft.Testing.Extensions.*/`): VSTest bridge, telemetry, coverage, retry, crash dump, etc.
- **Analyzers** (`src/Analyzers/`): Roslyn-based diagnostic analyzers for MSTest usage.

## FV Tool Choice

**Lean 4** with **Mathlib** (standard Lean 4 library for mathematics and formal proofs).

**Rationale**:
- Lean 4 has excellent decidable-proposition support (`decide` tactic).
- Mathlib provides rich libraries for lists, strings, and algebraic structures.
- Lean 4's dependent type system allows encoding invariants as types.
- Active ecosystem with CI integration via `lake build`.

## FV Strategy

This project applies formal verification **incrementally**, one target at a time:

1. Identify **pure or near-pure functions** with clear algebraic properties.
2. Write **informal specs** capturing intent from code, tests, and documentation.
3. Translate to **Lean 4 type definitions and theorem statements** (with `sorry`).
4. **Attempt proofs** using `decide`, `omega`, `simp`, `induction`, etc.
5. Report **bugs** when a proposition cannot be proved and the spec is correct.

We focus on **structural properties** and **invariants** rather than full functional equivalence (which would require modelling all of .NET's runtime semantics).

## Identified FV-Amenable Targets

### Target 1 — `ArgumentArity` ★★★★★

**File**: `src/Platform/Microsoft.Testing.Extensions.CommandLine/ArgumentArity.cs`
**Type**: `readonly struct ArgumentArity(int min, int max)`

A simple value struct for describing how many arguments a command-line option accepts.

**Why ideal for FV**:
- Tiny, self-contained type with only two integer fields.
- Five predefined constants with documented semantics.
- `IEquatable<T>` implementation to verify.
- Invariant: for all well-formed arities, `Min ≤ Max` — **not enforced by the constructor**.
- Equality properties (reflexivity, symmetry, transitivity) are easily decidable.

**Properties to verify**:
1. All five predefined constants satisfy `Min ≤ Max`.
2. `Zero` has Min=0, Max=0.
3. `ZeroOrOne` has Min=0, Max=1.
4. `ZeroOrMore` has Min=0, Max=`maxInt32`.
5. `OneOrMore` has Min=1, Max=`maxInt32`.
6. `ExactlyOne` has Min=1, Max=1.
7. `Equals` is an equivalence relation (reflexive, symmetric, transitive).
8. `==` and `!=` agree with `Equals`.

**Approximations**: Model `int.MaxValue` with an explicit Lean sentinel constant, for example `def maxInt32 : Int := 2^31 - 1`.

---

### Target 2 — `CommandLineParser.TryUnescape` ★★★★☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` (inner function)
**Informal signature**: command-line text × optional quote context × environment → either unescaped text or an error message

A pure function that unescapes command-line argument strings — handling single-quote and double-quote conventions.

**Why good for FV**:
- Pure (no side effects, no I/O).
- Well-documented convention via comments in source.
- Clear case analysis: plain string / single-quoted / double-quoted.

**Properties to verify**:
1. Single-quoted strings without interior quotes → strip outer quotes.
2. Single-quoted strings with interior quotes → error.
3. Double-quoted strings → strip quotes and apply backslash-escape rules.
4. Unquoted strings → returned unchanged.
5. Result of successful unescaping never starts or ends with the outer quote character.

**Approximations**: Lean model abstracts `IEnvironment.NewLine` as a parameter; does not model environment variable expansion.

---

### Target 3 — `CommandLineParser.ParseOptionAndSeparators` ★★★★☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` (inner function)
**Type**: `string → string × option(string)`

Splits a raw argument like `--option=value` or `--option:value` or `--option` into option name and argument.

**Properties to verify**:
1. If the input contains no `:` or `=`, the result argument is `none`.
2. If the input contains `:` or `=`, the option name is `input[..delimiterIndex]` and argument is the rest.
3. The returned option name has all leading `-` characters stripped.
4. If the delimiter is the first character, the option name is empty string.

**Approximations**: Models `IndexOfAny` over two characters; string indexing.

---

### Target 4 — `CommandLineOptionsValidator` arity validation ★★★☆☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs`
**Function**: `ValidateOptionsArgumentArity`

Checks that each parsed option has an argument count within its registered arity bounds.

**Properties to verify**:
1. If an option has `Max=0` and `argumentCount > 0` → error.
2. If `argumentCount < Min` → error.
3. If `argumentCount > Max` (and `Max > 0`) → error.
4. If `Min ≤ argumentCount ≤ Max` → no error for that option.
5. An option with `Arity = Zero` and zero arguments produces no error.

**Approximations**: Must model a simplified option registry; abstracts over provider identity.

---

### Target 5 — `CommandLineParseResult` structural equality ★★★☆☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs`
**Function**: `Equals(CommandLineParseResult?)`

Structural equality over a parse result: tool name, list of options (name + argument list), and list of errors.

**Properties to verify**:
1. Reflexivity: `r.Equals(r)` is always true.
2. Symmetry: `r1.Equals(r2) ↔ r2.Equals(r1)`.
3. Transitivity: `r1.Equals(r2) ∧ r2.Equals(r3) → r1.Equals(r3)`.
4. Empty parse result equals itself.
5. Two results differing only in tool name are not equal.

**Approximations**: Model strings as `String` (Lean), argument lists as `List String`.

---

### Target 6 — `ResponseFileHelper.SplitCommandLine` ★★★★☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs`
**Type**: `string → IEnumerable<string>`

A pure function that tokenises a command-line string into whitespace-separated tokens, treating double-quoted substrings as single tokens (with their surrounding quotes stripped).

**Why good for FV**:
- Pure function; all state is local variables.
- State machine structure with two orthogonal state dimensions (`seeking` ∈ {TokenStart, WordEnd} × `seekingQuote` ∈ {QuoteStart, QuoteEnd}).
- Well-defined contract: whitespace → token separator outside quotes; double-quote toggles quoting mode.
- Termination is obvious (index advances on every iteration).

**Properties to verify**:
1. Empty input → empty output.
2. Whitespace-only input → empty output.
3. Non-whitespace input with no quotes → splits into whitespace-delimited tokens.
4. Double-quoted string → single token with quotes stripped.
5. Double-quoted string containing spaces → single token, spaces preserved.
6. All output tokens have their `"` characters removed.
7. Two adjacent quoted strings (`"a""b"`) → may produce one or two tokens (document the chosen semantics).
8. A trailing non-terminated quote (e.g., `"foo`) → yields what remains as a token.

**Approximations**: Lean model uses `List Char`; whitespace is modelled as `Char.isWhitespace`; does not model the `SplitLine` comment-filter layer.

---

### Target 7 — `TreeNodeFilter.MatchFilterPattern` — Boolean Filter Algebra ★★★★★

**File**: `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs`
**Type**: `FilterExpression × String × PropertyBag → Bool`

A pure recursive function that evaluates whether a test-node path and property bag match a structured Boolean filter expression built from `And`, `Or`, `Not`, and `Nop` operators.

**Why excellent for FV**:
- Pure recursive function; ideal for Lean structural induction.
- The semantics exactly model a propositional Boolean algebra.
- Classic algebraic laws (De Morgan, double negation, idempotence, absorption) are provable purely by structural induction on the `FilterExpression` type.
- Abstracting away the `Regex` pattern matcher gives a clean propositional model.

**Properties to verify**:
1. `NopExpression` always returns `true`.
2. `Not(Not(A))` is semantically equivalent to `A`.
3. De Morgan (AND): `Not(And(A, B)) ↔ Or(Not(A), Not(B))`.
4. De Morgan (OR): `Not(Or(A, B)) ↔ And(Not(A), Not(B))`.
5. Idempotence: `And([A, A]) ↔ A`, `Or([A, A]) ↔ A`.
6. Identity: `And([Nop, A]) ↔ A`, `Or([Nop, ...]) ↔ true`.
7. Commutativity of binary `And`/`Or` (over two-element lists): `And([A, B]) ↔ And([B, A])`.

**Approximations**: The Lean model abstracts `Regex.IsMatch` as an opaque predicate parameter `match : String → Bool`. This captures the Boolean-algebra structure independently of the regex engine semantics. `PropertyBag` matching is similarly abstracted.

---

## Approach Notes

- We use Lean 4 with Mathlib for all proofs.
- We translate C# business logic into **pure functional Lean models**, explicitly noting what is abstracted away.
- `sorry` is used liberally early on; the goal is to get theorems stated correctly, then fill proofs.
- For simple finite types (like `ArgumentArity` constants), we rely on `decide` for closed proofs.
- We track which theorems are `sorry`-guarded vs. fully proved in `TARGETS.md`.

## Open Questions

- Should we model `int.MaxValue` as an explicit Lean constant (e.g., `def maxInt32 : Int := 2^31 - 1`) or leave it as an opaque constant?
- The `TryUnescape` function handles environment `NewLine` — should we abstract over this or assume `"\n"`?
- Is the lack of a `Min ≤ Max` invariant in `ArgumentArity` a real bug or an accepted API choice? Worth filing an issue if a "bad" arity causes unexpected validator behaviour.
- For `SplitCommandLine`, is the two-adjacent-quoted-strings case (`"a""b"`) intended to produce one token `ab` or two tokens? The current state-machine code yields `a` then starts a new token at the opening quote, so it produces two tokens. This is worth documenting in the informal spec.
- For `TreeNodeFilter.MatchFilterPattern`, should commutativity be stated over _ordered_ lists (which it isn't in general, since `All` and `Any` are order-independent) or only over two-element binary forms? The current implementation uses `List.All` / `List.Any`, which are order-independent, so commutativity holds trivially.
