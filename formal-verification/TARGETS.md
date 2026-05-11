# FV Targets

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Legend

| Phase | Description |
|-------|-------------|
| 1 | Research — identified, rationale documented |
| 2 | Informal spec extracted |
| 3 | Lean 4 formal spec written (type signatures + theorem stubs) |
| 4 | Lean 4 implementation model extracted |
| 5 | Proofs attempted / completed |

## Target List

| # | Name | File | Phase | Status | PR/Issue | Notes |
|---|------|------|-------|--------|----------|-------|
| 1 | `ArgumentArity` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs` | 2 | Informal spec extracted | [PR #7799](https://github.com/microsoft/testfx/pull/7799) | Top priority for Task 3 |
| 2 | `CommandLineParser.TryUnescape` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 2 | Informal spec extracted | — | BUG-1, BUG-2 documented |
| 3 | `CommandLineParser.ParseOptionAndSeparators` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 2 | Informal spec extracted | — | BUG-5 documented |
| 4 | `CommandLineOptionsValidator.ValidateOptionsArgumentArity` | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs` | 2 | Informal spec extracted | — | OQ-1..OQ-4 documented |
| 5 | `CommandLineParseResult.Equals` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs` | 2 | Informal spec extracted | — | Equivalence-relation laws |
| 6 | `ResponseFileHelper.SplitCommandLine` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs` | 2 | Informal spec extracted | [PR #7899](https://github.com/microsoft/testfx/pull/7899) | BUG-3, BUG-4 documented |
| 7 | `TreeNodeFilter.MatchFilterPattern` | `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs` | 2 | Informal spec extracted | — | Boolean algebra laws |
| 8 | `EnvironmentVariableParser.ParseBool` | `src/Platform/Microsoft.Testing.Platform/Helpers/EnvironmentVariableParser.cs` | 2 | Informal spec extracted | — | All 8 theorems decidable |
| 9 | `PasteArguments.ContainsNoWhitespaceOrQuotes` | `src/Platform/Microsoft.Testing.Platform/Helpers/PasteArguments.cs` | 2 | Informal spec extracted | — | 14 theorems; char whitespace approx |
| 10 | `CommandLineOptionsValidator.ValidateOptionsAreNotDuplicated` | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs` | 1 | Identified | — | Next: Task 2 |
| 11 | `ValidationResult` | `src/Platform/Microsoft.Testing.Platform/Extensions/ValidationResult.cs` | 1 | Identified | — | Discriminated union invariant |
| 12 | `PasteArguments.AppendArgument` | `src/Platform/Microsoft.Testing.Platform/Helpers/PasteArguments.cs` | 1 | Identified | — | Deferred until target 9 proved |
| 13 | `TreeNodeFilter.TokenizeFilter` | `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs` | 1 | Identified | — | Lexer; deferred |
| 14 | `TimeSpanParser.TryParse` | `src/Platform/Microsoft.Testing.Platform/Helpers/TimeSpanParser.cs` | 1 | Identified | — | Regex-driven; harder to model |
| 15 | `CommandLineOption` name validation | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOption.cs` | 1 | Identified | — | Character predicate |
| 16 | `DotnetTestConnection.IsVersionCompatible` | `src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs` | 1 | Identified | — | One-liner; ideal smallest target |
| 17 | `LLMEnvironmentDetector` rule composition | `src/Platform/Microsoft.Testing.Platform/Helpers/LLMEnvironmentDetector.cs` | 1 | **NEW** — Identified this run | — | Rule-based DSL; composition laws |

## Priority Order

1. **`ArgumentArity`** (id=1) — highest priority. Smallest self-contained target; decidable properties; warm-up for Lean setup. Informal spec done. **Next: Task 3 (blocked by Lean toolchain).**
2. **`CommandLineParser.TryUnescape`** (id=2) — second priority. Pure function; security-relevant; 2 confirmed bugs. **Next: Task 3 (blocked by Lean toolchain).**
3. **`TreeNodeFilter.MatchFilterPattern`** (id=7) — third priority. Boolean algebra; De Morgan and double negation provable by `simp`. **Next: Task 3 (blocked by Lean toolchain).**
4. **`ResponseFileHelper.SplitCommandLine`** (id=6) — fourth priority. Pure tokeniser; state machine; 2 confirmed bugs. **Next: Task 3 (blocked by Lean toolchain).**
5. **`CommandLineParser.ParseOptionAndSeparators`** (id=3) — fifth priority. Small pure function; BUG-5 documented. **Next: Task 3.**
6. **`DotnetTestConnection.IsVersionCompatible`** (id=16) — sixth priority for Task 2. One-liner pure function; trivially provable. **Next: Task 2.**
7. **`EnvironmentVariableParser.ParseBool`** (id=8) — seventh. All theorems decidable by `decide`. **Next: Task 3.**
8. **`PasteArguments.ContainsNoWhitespaceOrQuotes`** (id=9) — eighth. Concatenation split via `List.all_append`. **Next: Task 3.**

## Findings

| ID | Description | Target |
|----|-------------|--------|
| BUG-1 | `TryUnescape`: single-char quote → `IndexOf(char,1,-1)` throws | id=2 |
| BUG-2 | `TryUnescape`: single-char double-quote → `input[1..^1]` range exception | id=2 |
| BUG-3 | `SplitCommandLine`: unclosed quote discards all input | id=6 |
| BUG-4 | `SplitCommandLine`: adjacent quoted strings emit 2 tokens not 1 | id=6 |
| BUG-5 | `ParseOptionAndSeparators`: empty option name not rejected | id=3 |
| OQ-1 | `ValidateOptionsArgumentArity`: absent required options not caught | id=4 |
| OQ-2 | `ValidateOptionsArgumentArity`: `KeyNotFoundException` if called before ValidateNoUnknownOptions | id=4 |
| OQ-3 | `ValidateOptionsArgumentArity`: Max==0 message asymmetry | id=4 |
| OQ-4 | `ValidateOptionsArgumentArity`: grammar defect ("at least 1 arguments") | id=4 |
| GAP-1 | `UnitTestOutcomeHelper`: Aborted and Unknown have no unit tests | — |
| GAP-2 | `UnitTestOutcomeHelper`: NotRunnable with MapNotRunnableToFailed=false has no unit test | — |

## New Target Research — Run 14 (2026-05-11)

### Target 17 — `LLMEnvironmentDetector` rule composition ★★★☆☆

**File**: `src/Platform/Microsoft.Testing.Platform/Helpers/LLMEnvironmentDetector.cs`
**Type**: Internal rule-based DSL for AI-tool environment detection

This recently-added helper detects whether the current process is running inside an LLM-powered coding agent (Claude Code, Copilot, Cursor, Gemini, etc.). It models detection as a composition of three primitive rule types:

- `BooleanEnvironmentRule`: any env var ∈ list parses as `true` (via `ParseBool`)
- `AnyPresentEnvironmentRule`: any env var ∈ list is non-null/non-empty
- `AnyPresentEnvironmentRule`: env var has a specific value (case-insensitive)
- `AnyMatchEnvironmentRule`: logical OR over a list of sub-rules

**Why good for FV**:
- The rule types form a small, pure algebraic structure: `Rule → Env → Bool`
- `AnyMatchEnvironmentRule` with an empty list always returns `false` → **provable by `decide`**
- `AnyMatchEnvironmentRule [r]` is extensionally equal to `r.IsMatch()` → provable
- Monotonicity: adding a rule to `AnyMatchEnvironmentRule` can only increase detection → provable by structural induction
- The private `EnvironmentVariableParser.ParseBool` is the same logic as target id=8 — reuse the same Lean model

**Properties to verify**:
1. Empty rule list → `AnyMatchEnvironmentRule` never matches (trivially decidable)
2. Singleton rule list → same as that rule (extensional equality)
3. `AnyMatchEnvironmentRule` is monotone in rule count (inductive)
4. `ParseBool(null, default)` = `default` (decidable)
5. `ParseBool("1", _)` = `true`, `ParseBool("0", _)` = `false` (decidable)
6. `ParseBool` result ∈ {`true`, `false`} for any input (trivially true)

**Approximations**: Model environment as a `HashMap String (Option String)`. The `AnyMatchEnvironmentRule` list is modelled as a `List Rule`.

**Priority**: Medium. Rich composition properties, but requires modelling env as a function parameter.

## Notes

- Lean 4 toolchain installation has been blocked in the agent sandbox for 14 consecutive runs (elan binary execution denied by security policy). All Tasks 3–5 are deferred until this is resolved.
- Fourteen targets are in Microsoft.Testing.Platform (command-line infrastructure, server mode, helpers). One target (`UnitTestOutcomeHelper` gaps) is in the MSTest adapter.
- `TreeNodeFilter.MatchFilterPattern` (id=7) is the mathematically richest target: Boolean algebra proofs.
- `DotnetTestConnection.IsVersionCompatible` (id=16) is the smallest remaining Task-2 target: a single expression.
- `LLMEnvironmentDetector` (id=17) is newly identified this run from recently-added code; interesting for rule-composition properties.
