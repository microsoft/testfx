---
name: testfx-test-value-evidence
description: Apply the vendored test-value-evidence skill with TestFx toolchain, assertion, parallel-safety, packed-consumer, and review conventions. Use when implementing or reviewing behavior-changing TestFx PRs or commits, including source-only changes.
---

# TestFx test-value evidence

Read and apply the canonical
[test-value-evidence skill](../../../.agents/skills/test-value-evidence/SKILL.md)
first, then these repository-specific requirements. Keep the canonical vendored
file unmodified; its source revision and blob are recorded in
[eng/vendored-files.json](../../../eng/vendored-files.json).

- Read [repository instructions](../../copilot-instructions.md). Use the
  repository-pinned toolchain through `build.cmd` or `build.sh`, capture binary
  logs for MSBuild commands, and use the owning project's assertion conventions
  and `BannedSymbols.txt`. MSTest framework tests use `TestContainer`; MTP and
  analyzer tests use MSTest; adapter tests use their established assertion APIs.
- Apply [parallel-safety preflight](../testfx-parallel-safety-preflight/SKILL.md)
  before test/lifecycle changes and against the final diff. Counterfactual
  experiments must use isolated outputs and preserve the actual test scheduling.
- For acceptance, package, launcher, protocol, or cross-component changes, apply
  [acceptance validation](../testfx-acceptance-validation/SKILL.md). Repack each
  product variant before testing, prove the effective package/cache and loaded
  binary paths, and retain real shipping-layout evidence. Assert/API overload
  changes also need the repository's packed source-compatibility coverage.
- Reuse the installed
  [test-gap-analysis](../../../.agents/skills/test-gap-analysis/SKILL.md) for
  bounded static reasoning and the existing
  [mutation workflow](../../workflows/mutation-testing.yml) for available
  executed Stryker evidence. Do not upgrade unrelated curated plugins to run
  this skill.
- Keep publication under the [code-review gate](../code-review/SKILL.md).
  The automatic and `/review-tests` reviewers are read-only: report static
  predictions or unavailable proof unless exact matching execution records can
  be inspected. Do not execute PR code or infer red-green proof from green CI.
  Put material evidence gaps in the summary, not automatic inline findings.

The existing review workflow reports the canonical evidence table for each
eligible PR head, including unchanged tests protecting source-only changes.
Individual commit validation is separate; neither the skill nor its vendored
presence enforces a Git hook or proves live workflow publication.
