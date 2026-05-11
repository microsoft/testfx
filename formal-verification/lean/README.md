# Lean 4 Formal Verification — FVSquad

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

This directory contains the Lean 4 formal verification project for `microsoft/testfx`.

## Structure

```
lean/
  lakefile.toml          # Lake build configuration (no Mathlib; standalone)
  lean-toolchain         # Pins exact Lean version (leanprover/lean4:v4.29.1)
  lake-manifest.json     # Resolved Lake dependency manifest
  FVSquad.lean           # Root module (imports all sub-modules)
  FVSquad/
    <Name>.lean          # One file per FV target (spec + proofs)
```

## Building

Requires [elan](https://github.com/leanprover/elan) (the Lean version manager).

```bash
# Install elan v3.1.0 (Linux x86_64)
ELAN_VERSION=v3.1.0
curl -sSfL -o elan.tar.gz \
  "https://github.com/leanprover/elan/releases/download/${ELAN_VERSION}/elan-x86_64-unknown-linux-gnu.tar.gz"
tar -xzf elan.tar.gz
./elan-init -y --default-toolchain leanprover/lean4:v4.29.1

# Build the project
cd formal-verification/lean
lake build
```

The `lean-toolchain` file pins the exact Lean version. `lake build` creates a
local `.lake/` directory with build artifacts (gitignored).

## Targets

| File | Target | Phase | Theorems | sorry |
|------|--------|-------|----------|-------|
| `FVSquad/TreeNodeFilter.lean` | `TreeNodeFilter.MatchFilterPattern` | 3 | 21 | 0 |

## CI

`.github/workflows/lean-proofs.yml` runs `lake build` on every PR that touches
files in `formal-verification/lean/`. It also reports theorem and `sorry` counts
in the job summary.

## Notes

- **No Mathlib**: The lakefile does not depend on Mathlib (CI firewalls block
  the cache download). All proofs use Lean 4 core tactics only.
- **Opaque axioms**: `matchesGlob` is an opaque `Bool`-valued function abstracting
  the C# regex matching.
