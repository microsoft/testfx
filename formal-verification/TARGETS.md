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
| 1 | `ArgumentArity` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs` | 2 | Informal spec extracted | [PR #7799](https://github.com/microsoft/testfx/pull/7799) |
| 2 | `CommandLineParser.TryUnescape` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 1 | Identified | — |
| 3 | `CommandLineParser.ParseOptionAndSeparators` | `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs` | 1 | Identified | — |
| 4 | `CommandLineOptionsValidator` arity validation | `src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs` | 1 | Identified | — |
| 5 | `CommandLineParseResult.Equals` | `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs` | 1 | Identified | — |

## Priority Order

1. **`ArgumentArity`** — highest priority. Smallest self-contained target; decidable properties; good warm-up for setting up the Lean environment.
2. **`CommandLineParser.TryUnescape`** — second priority. Pure function with clear specification; security-relevant string processing.
3. **`CommandLineParser.ParseOptionAndSeparators`** — third priority. Small pure function; useful for verifying parser correctness.
4. **`CommandLineOptionsValidator` arity validation** — fourth priority. Validation logic with clear input/output contract.
5. **`CommandLineParseResult.Equals`** — fifth priority. Structural equality; good for verifying equivalence-relation laws.

## Notes

- All five targets are in the command-line infrastructure (`src/Platform/Microsoft.Testing.Platform/CommandLine/`).
- This focus makes sense: command-line parsing is pure and testable, with clear specification from the POSIX/CLI conventions.
- MSTest assertion APIs (e.g., `Assert.AreEqual`, `Assert.IsTrue`) are interesting but harder to model formally due to generic type constraints and exception-based control flow.
- Future targets may include the test-filter grammar and the server-mode protocol state machine.
