# FV Project Report

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Status

**Phase**: Early — Research expanded to 7 targets; informal specs extracted for `ArgumentArity` (merged) and `CommandLineParser.TryUnescape` (PR open). Lean toolchain unavailable in runner (network firewall), blocking Task 3+.

## Summary

The Lean Squad has surveyed the `microsoft/testfx` codebase and identified **seven** high-quality FV targets. Two informal specifications have been extracted. The Lean toolchain cannot currently be installed in the runner environment due to network restrictions.

Key findings:
- **`ArgumentArity`**: informal spec extracted (14 properties, 4 groups). Constructor does not enforce `Min ≤ Max`; `CommandLineOption` enforces this on construction, acting as the real guard.
- **`CommandLineParser.TryUnescape`**: informal spec extracted (24 properties, 5 groups, 2 confirmed bugs for single-char quote inputs). PR open.
- **New targets identified**: `ResponseFileHelper.SplitCommandLine` (pure tokeniser) and `TreeNodeFilter.MatchFilterPattern` (Boolean algebra — ideal for structural induction proofs of De Morgan, double negation, idempotence).

Next steps (once Lean toolchain available):
1. Write Lean 4 formal spec for `ArgumentArity` (Task 3).
2. Write Lean 4 formal spec for `TreeNodeFilter.MatchFilterPattern` — Boolean algebra model with abstract match predicate.

## Targets Identified

| Target | Phase | Notes |
|--------|-------|-------|
| `ArgumentArity` | 2 | Informal spec extracted — 14 properties, open question on ill-formed arity (merged PR) |
| `CommandLineParser.TryUnescape` | 2 | Informal spec extracted — 24 properties, 2 confirmed bugs (PR open) |
| `TreeNodeFilter.MatchFilterPattern` | 1 | Boolean algebra; De Morgan, double negation, idempotence provable by `simp` |
| `ResponseFileHelper.SplitCommandLine` | 1 | Pure tokeniser; state machine; grammar-based properties |
| `CommandLineParser.ParseOptionAndSeparators` | 1 | Pure option-splitting function |
| `CommandLineOptionsValidator` arity validation | 1 | Arity bounds checking |
| `CommandLineParseResult.Equals` | 1 | Structural equality laws |

## Run History

| Date | Tasks | Outcome |
|------|-------|---------|
| 2026-04-26 | Task 1 (Research expansion), Task 3 (blocked — Lean unavailable) | Added 2 new targets: SplitCommandLine and TreeNodeFilter.MatchFilterPattern; updated priority order; Lean toolchain still blocked by runner network |
| 2026-04-25 | Task 2 (Informal Spec — TryUnescape) | Extracted 24-property informal spec; discovered 2 confirmed bugs for single-char quote inputs |
| 2026-04-24 | Task 1 (Research), Task 2 (Informal Spec), Task 9 (CI Automation) | Identified 5 targets; extracted informal spec for ArgumentArity (14 properties, open question on ill-formed arity); set up CI workflow |
| 2026-04-24 | Task 1 (Research), Task 9 (CI Automation) | Identified targets, created FV directory, set up CI workflow |
