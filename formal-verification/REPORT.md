# FV Project Report

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Status

**Phase**: Early — Research complete, informal spec for `ArgumentArity` extracted.

## Summary

The Lean Squad has surveyed the `microsoft/testfx` codebase and identified five high-quality FV targets in the command-line infrastructure of Microsoft.Testing.Platform. All five targets are pure or near-pure functions with clear algebraic properties, making them suitable for Lean 4 formal verification.

An informal specification has been extracted for `ArgumentArity` (highest-priority target): 14 properties across four groups (predefined constants, well-formedness, equality, distinctness). One open question identified: the constructor does not enforce `Min ≤ Max`, so ill-formed arities are silently accepted. This is a potential source of undefined validator behavior.

The next steps are:
1. Write the Lean 4 formal spec for `ArgumentArity` (types + theorem stubs).
2. Attempt decidable proofs with `decide`.
3. Advance to `CommandLineParser.TryUnescape`.

## Targets Identified

| Target | Phase | Notes |
|--------|-------|-------|
| `ArgumentArity` | 2 | Informal spec extracted — 14 properties, open question on ill-formed arity |
| `CommandLineParser.TryUnescape` | 1 | Security-relevant string unescaping |
| `CommandLineParser.ParseOptionAndSeparators` | 1 | Pure option-splitting function |
| `CommandLineOptionsValidator` arity validation | 1 | Arity bounds checking |
| `CommandLineParseResult.Equals` | 1 | Structural equality laws |

## Run History

| Date | Tasks | Outcome |
|------|-------|---------|
| 2026-04-24 | Task 1 (Research), Task 2 (Informal Spec), Task 9 (CI Automation) | Identified 5 targets; extracted informal spec for ArgumentArity (14 properties, open question on ill-formed arity); set up CI workflow |
| 2026-04-24 | Task 1 (Research), Task 9 (CI Automation) | Identified targets, created FV directory, set up CI workflow |
