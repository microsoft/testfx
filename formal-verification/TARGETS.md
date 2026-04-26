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

| # | Name | File | Phase | Status | PR/Issue |
|---|------|------|-------|--------|----------|
| 1 | `ArgumentArity` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs` | 2 | Informal spec extracted | [PR #7799](https://github.com/microsoft/testfx/pull/7799) (merged) |
| 2 | `CommandLineParser.TryUnescape` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 2 | Informal spec extracted (PR open) | [PR #7833](https://github.com/microsoft/testfx/pull/7833) |
| 3 | `CommandLineParser.ParseOptionAndSeparators` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 1 | Identified | — |
| 4 | `CommandLineOptionsValidator` arity validation | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs` | 1 | Identified | — |
| 5 | `CommandLineParseResult.Equals` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs` | 1 | Identified | — |
| 6 | `ResponseFileHelper.SplitCommandLine` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs` | 1 | Identified | — |
| 7 | `TreeNodeFilter.MatchFilterPattern` | `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs` | 1 | Identified | — |

## Priority Order

1. **`ArgumentArity`** — highest priority. Smallest self-contained target; decidable properties; good warm-up for setting up the Lean environment. Informal spec done.
2. **`CommandLineParser.TryUnescape`** — second priority. Pure function with clear specification; security-relevant string processing. Informal spec extracted (PR open).
3. **`TreeNodeFilter.MatchFilterPattern`** — **elevated third priority**. Pure recursive Boolean algebra; structural induction proofs; De Morgan and double negation provable by `simp`. Excellent Lean target.
4. **`ResponseFileHelper.SplitCommandLine`** — fourth priority. Pure tokeniser with state machine; clear grammar-based properties.
5. **`CommandLineParser.ParseOptionAndSeparators`** — fifth priority. Small pure function; useful for verifying parser correctness.
6. **`CommandLineOptionsValidator` arity validation** — sixth priority. Validation logic with clear input/output contract.
7. **`CommandLineParseResult.Equals`** — seventh priority. Structural equality; good for verifying equivalence-relation laws.

## Notes

- Six of the seven targets are in the command-line and filter infrastructure of Microsoft.Testing.Platform.
- `TreeNodeFilter.MatchFilterPattern` (Target 7) is the highest-complexity target but also the most mathematically rich: proofs of Boolean-algebra laws give immediate, meaningful results.
- `ResponseFileHelper.SplitCommandLine` (Target 6) is derived from `dotnet/command-line-api` — the upstream source is noted in comments; cross-referencing it may reveal documented invariants.
- MSTest assertion APIs (e.g., `Assert.AreEqual`, `Assert.IsTrue`) remain interesting but harder to model formally due to generic type constraints and exception-based control flow.
- Future targets may include the server-mode protocol state machine and the test-filter tokeniser (`TreeNodeFilter.TokenizeFilter`).
