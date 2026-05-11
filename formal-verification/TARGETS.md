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

| # | Name | File | Phase | Status | Notes |
|---|------|------|-------|--------|-------|
| 1 | `ArgumentArity` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs` | 2 | Informal spec extracted | [PR #7799](https://github.com/microsoft/testfx/pull/7799). Ready for Task 3. |
| 2 | `CommandLineParser.TryUnescape` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 2 | Informal spec extracted | BUG-1/BUG-2 documented. Ready for Task 3. |
| 3 | `CommandLineParser.ParseOptionAndSeparators` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 2 | Informal spec extracted | [PR #7919](https://github.com/microsoft/testfx/pull/7919). Ready for Task 3. |
| 4 | `CommandLineOptionsValidator` arity validation | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs` | 1 | Identified | 4 open questions (OQ-1–OQ-4). |
| 5 | `CommandLineParseResult.Equals` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs` | 1 | Identified | [PR #7918](https://github.com/microsoft/testfx/pull/7918). |
| 6 | `ResponseFileHelper.SplitCommandLine` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs` | 2 | Informal spec extracted | [PR #7899](https://github.com/microsoft/testfx/pull/7899). Unclosed-quote discard bug documented. |
| 7 | `TreeNodeFilter.MatchFilterPattern` | `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs` | 2 | Informal spec extracted | [PR #7934](https://github.com/microsoft/testfx/pull/7934). Ready for Task 3. |
| 8 | `UnitTestOutcomeHelper.ToTestOutcome` | `src/Adapter/MSTestAdapter.PlatformServices/Helpers/UnitTestOutcomeHelper.cs` | 2 | Informal spec extracted | Pure 2-param switch, 14 decidable cases. 5 untested paths found. Highest-priority next Lean spec target. |
| 9 | `PasteArguments.AppendArgument` | `src/Platform/Microsoft.Testing.Platform/Helpers/PasteArguments.cs` | 1 | Identified | Windows CL quoting 2N/2N+1 backslash rules. |
| 10 | `ValidationResult` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ValidationResult.cs` | 1 | Identified | Discriminated union; two cases (Success/Failure). |
| 11 | `TreeNodeFilter.TokenizeFilter` | `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs` | 1 | Identified | Lexer for filter grammar. |
| 12 | `TimeSpanParser.TryParse` | `src/Platform/Microsoft.Testing.Platform/Helpers/TimeSpanParser.cs` | 1 | Identified | Parsing with format fallback. |
| 13 | `CommandLineOption` name validation | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOption.cs` | 1 | Identified | Character-class predicate: `IsLetterOrDigit \|\| hyphen \|\| ?`. |
| 14 | `EnvironmentVariableParser.ParseBool` | `src/Platform/Microsoft.Testing.Platform/Helpers/LLMEnvironmentDetector.cs` | 2 | Informal spec extracted | Pure `string? → bool` with explicit truthy/falsy sets. Trivially decidable. |
| 15 | `PasteArguments.ContainsNoWhitespaceOrQuotes` | `src/Platform/Microsoft.Testing.Platform/Helpers/PasteArguments.cs` | 1 | Identified | Pure string predicate: no whitespace and no `"`. Simple inductive proof. |

## Priority Order (next-up targets)

1. **`UnitTestOutcomeHelper.ToTestOutcome`** (Target 8) — **highest priority for Task 3**. Pure 2-parameter switch over a 11-value enum with two Boolean flags. All 14 cases are decidable. Informal spec extracted this run. Smallest possible Lean spec once toolchain available.
2. **`ArgumentArity`** (Target 1) — second priority. Smallest self-contained struct; decidable constant-value properties; good Lean warm-up once toolchain unblocked.
3. **`EnvironmentVariableParser.ParseBool`** (Target 14) — third priority. Pure function over a few string literals; `decide` can verify all named cases exhaustively.
4. **`CommandLineParseResult.Equals`** (Target 5) — fourth priority. Structural equality; equivalence-relation laws straightforward to state and prove.
5. **`TreeNodeFilter.MatchFilterPattern`** (Target 7) — fifth priority. Richest mathematical content; De Morgan and double-negation laws; structural induction proofs.
6. **`CommandLineParser.TryUnescape`** (Target 2) — sixth priority. Security-relevant; bugs BUG-1 and BUG-2 documented.
7. **`ResponseFileHelper.SplitCommandLine`** (Target 6) — seventh priority. Grammar-based tokeniser; unclosed-quote discard bug confirmed.
8. **`PasteArguments.ContainsNoWhitespaceOrQuotes`** (Target 15) — eighth priority. Tiny pure predicate; ideal `simp`/`induction` proof target.

## Findings to Date

| ID | Target | Finding |
|----|--------|---------|
| BUG-1 | TryUnescape | Single-char single-quoted string → `IndexOf(char, 1, -1)` throws `ArgumentOutOfRangeException` |
| BUG-2 | TryUnescape | Single-char double-quoted string → `input[1..^1]` range exception |
| BUG-3 | SplitCommandLine | Unclosed quote → all accumulated input is discarded |
| BUG-4 | SplitCommandLine | Adjacent quoted strings emit 2 tokens, not 1 |
| BUG-5 | ParseOptionAndSeparators | Empty option name is not rejected |
| OQ-1 | ValidateOptionsArgumentArity | Absent required options not caught |
| OQ-2 | ValidateOptionsArgumentArity | `KeyNotFoundException` if called before `ValidateNoUnknownOptions` |
| OQ-3 | ValidateOptionsArgumentArity | `Max==0` message asymmetry |
| OQ-4 | ValidateOptionsArgumentArity | Grammar defect: "at least 1 arguments" |
| GAP-1 | UnitTestOutcomeHelper | `Aborted` and `Unknown` have no unit tests |
| GAP-2 | UnitTestOutcomeHelper | `NotRunnable` with `MapNotRunnableToFailed=false` has no unit test |
| GAP-3 | UnitTestOutcomeHelper | `Inconclusive` with `MapInconclusiveToFailed=true` has no unit test |
| GAP-4 | UnitTestOutcomeHelper | Out-of-range `UnitTestOutcome` values have no unit test |

## Notes

- **Lean toolchain**: `elan` installation has been blocked by the network firewall in every run so far. All Task 3+ work is deferred until the toolchain becomes available. The `formal-verification/lean/` directory has a valid `lakefile.toml` and `lean-toolchain` file ready to use.
- **Targets 1–8** all have merged informal specs (see `formal-verification/specs/`). Target 8 was extracted this run.
- **Targets 9–15** are identified but have no informal spec yet. Targets 14–15 are new this run.
- `ResponseFileHelper.SplitCommandLine` (Target 6) is derived from `dotnet/command-line-api`; the upstream source is noted in comments.
- `TreeNodeFilter.MatchFilterPattern` (Target 7) is the highest-complexity target but also the most mathematically rich: proofs of Boolean-algebra laws give immediate, meaningful results.
- MSTest assertion APIs remain interesting but harder to model formally due to generic type constraints and exception-based control flow.
