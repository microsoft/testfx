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
4. `ZeroOrMore` has Min=0, Max=`Int.max`.
5. `OneOrMore` has Min=1, Max=`Int.max`.
6. `ExactlyOne` has Min=1, Max=1.
7. `Equals` is an equivalence relation (reflexive, symmetric, transitive).
8. `==` and `!=` agree with `Equals`.

**Approximations**: Model `int.MaxValue` as a sentinel constant (e.g., `Int32.max`).

---

### Target 2 — `CommandLineParser.TryUnescape` ★★★★☆

**File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` (inner function)
**Type**: `string × option × IEnvironment → Result(string, string)`

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

## Approach Notes

- We use Lean 4 with Mathlib for all proofs.
- We translate C# business logic into **pure functional Lean models**, explicitly noting what is abstracted away.
- `sorry` is used liberally early on; the goal is to get theorems stated correctly, then fill proofs.
- For simple finite types (like `ArgumentArity` constants), we rely on `decide` for closed proofs.
- We track which theorems are `sorry`-guarded vs. fully proved in `TARGETS.md`.

## Open Questions

- Should we model `int.MaxValue` as Lean's `Int.max` (i.e., `2^31 - 1`) or leave it as an opaque constant?
- The `TryUnescape` function handles environment `NewLine` — should we abstract over this or assume `"\n"`?
- Is the lack of a `Min ≤ Max` invariant in `ArgumentArity` a real bug or an accepted API choice? Worth filing an issue if a "bad" arity causes unexpected validator behaviour.
