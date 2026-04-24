# Lean 4 Formal Verification — FVSquad

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

This directory contains the Lean 4 formal verification project for `microsoft/testfx`.

## Structure

```
lean/
  lakefile.toml          # Lake build configuration
  FVSquad/
    <Name>.lean          # One file per FV target (spec + implementation model + proofs)
```

## Building

Requires [elan](https://github.com/leanprover/elan) (the Lean version manager).

```bash
# Install elan (if not already installed)
curl -sSfL https://raw.githubusercontent.com/leanprover/elan/master/elan-init.sh | sh

# Build the project
cd formal-verification/lean
lake build
```

## Status

No Lean source files yet — targets are currently in Phase 1 (Research) or Phase 2 (Informal Spec).
Lean source files will be added when targets advance to Phase 3 (Formal Spec Writing).

## CI

A GitHub Actions workflow (`.github/workflows/lean-proofs.yml`) runs `lake build`
on every PR that modifies files in this directory.
