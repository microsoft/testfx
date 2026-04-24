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
# Install a pinned elan release (example for Linux x86_64)
ELAN_VERSION=v4.2.1
curl -sSfL -o elan.tar.gz "https://github.com/leanprover/elan/releases/download/${ELAN_VERSION}/elan-x86_64-unknown-linux-gnu.tar.gz"
curl -sSfL -o elan.tar.gz.sha256 "https://github.com/leanprover/elan/releases/download/${ELAN_VERSION}/elan-x86_64-unknown-linux-gnu.tar.gz.sha256"
sha256sum -c elan.tar.gz.sha256
tar -xzf elan.tar.gz
./elan-init -y

# Build the project
cd formal-verification/lean
lake build
```

The `lean-toolchain` file pins the exact Lean version to match the Mathlib dependency.

`lake build` creates a local `.lake/` directory with build artifacts.
The repository root `.gitignore` ignores this directory to avoid accidentally committing
generated files.

## Status

No Lean source files yet — targets are currently in Phase 1 (Research) or Phase 2 (Informal Spec).
Lean source files will be added when targets advance to Phase 3 (Formal Spec Writing).

## CI

A GitHub Actions workflow (`.github/workflows/lean-proofs.yml`) runs `lake build`
on every PR that modifies files in this directory.
