---
name: curation-exceptions
description: >-
  Use when running gh copilot-curate update/verify, comparing excluded files
  with upstream dotnet/skills, or editing repository-maintained testing/MSBuild
  skills. Preserves exclusion reasons, upstream baselines, and compare-on-refresh
  requirements.
---

# Curation exceptions

## Repository-owned test guidance

The curated `dotnet-test` installation uses the upstream grading rubric,
including its result states, concrete improvement suggestions, and read-only
per-test pseudo-mutation assessment. TestFx-specific review conventions live
in `.github/workflows/shared/test-reviewer-shared.md`, not a fork of the skill.

The installation deliberately excludes the repository-owned review corrections
below so a refresh cannot silently overwrite them:

- [Code testing reference helper](../code-testing-extensions/SKILL.md),
  [test analysis reference helper](../test-analysis-extensions/SKILL.md),
  and [filter syntax reference](../filter-syntax/SKILL.md)
  keep internal language guidance callable by the model while hidden from the
  user menu.
- [Python generation guidance](../code-testing-extensions/extensions/python.md)
  preserves requested regression tests and reports production or tooling blockers.
- [Ruby generation guidance](../code-testing-extensions/extensions/ruby.md)
  preserves the selected runner's exit status and uses real test counts rather
  than counting Rake tasks or falling back after a failure.
- [.NET generation guidance](../code-testing-extensions/extensions/dotnet.md)
  distinguishes VSTest command mode from native MTP command mode.
- [Coverage setup discovery](../coverage-analysis/references/setup-discovery.md)
  recognizes `.slnx`, preserves the selected repository/user entry point, and
  bounds test-project discovery to that root and its containing Git repository.
- [Test project scaffolding](../scaffold-dotnet-test-project/SKILL.md)
  adds bridge properties only for VSTest command mode.
- [PowerShell pipeline example](../code-testing-extensions/extensions/powershell-examples.md)
  loads module-defined types at parse time, uses valid Pester identity checks,
  and covers the planned missing-invoice path.
- [C++ pipeline example](../code-testing-extensions/extensions/cpp-examples.md)
  includes the planned missing-invoice case and its required Catch2 header.
- [Kotlin](../code-testing-extensions/extensions/kotlin-examples.md),
  [Ruby](../code-testing-extensions/extensions/ruby-examples.md), and
  [Rust](../code-testing-extensions/extensions/rust-examples.md)
  examples include the planned missing-invoice case and align their reports
  with the actual test counts.

These corrections are relative to upstream commit
`e468462d8a0900f5278331c1ae12f45e015bb656`. Compare each excluded file explicitly
on refresh and remove its exception once upstream carries the correction.
These excluded files are not checked by `gh copilot-curate verify`; that check
still verifies the remaining imported files. They do not fork the grading rubric.

The installation also excludes
[Find-UntestedSources.cs](../find-untested-sources/scripts/Find-UntestedSources.cs).
It matches upstream commit `0608d8924cd3173411e36650a7a01b4063e1f55a` except
for the UTF-8 BOM required by `.editorconfig`.

This helper is repository-owned, not checked by `gh copilot-curate verify`.
Compare it explicitly when refreshing upstream and preserve its C# encoding.
The manifest's include list keeps updates from overwriting it.

[Test generation guidance](../../../.github/skills/code-testing/unit-test-generation.prompt.md)
adapts upstream agents' relative links to the `.agents/skills` installation
layout. The legacy [code-testing-generator](../../../.github/agents/code-testing-generator.agent.md)
agent remains available for existing workflows; new upstream guidance uses
[test-engineer](../../../.github/agents/test-engineer.agent.md). The retained generator
resolves one absolute non-stageable state directory for phased work and passes
its canonical artifact paths through each worker, retry, and iteration.

## Repository-maintained testing skills

These skills retain their testing functionality without referrals to removed
framework/platform migration skills. They are excluded from upstream curation
updates to preserve that routing.

The [test-engineer agent](../../../.github/agents/test-engineer.agent.md) is also
repository-maintained so its routing cannot restore the removed migration
dispatcher. Framework/platform migration plugins remain intentionally absent.

- [MTP hot reload](../mtp-hot-reload/SKILL.md) - Iterate on test fixes without rebuilding.
- [Running .NET tests](../run-tests/SKILL.md) - Detect the current runner and select compatible commands.
- [Test filter syntax](../filter-syntax/SKILL.md) - Reference filter syntax by platform and framework.
- [Test platform detection](../platform-detection/SKILL.md) - Reference runner and framework detection.
- [Writing MSTest tests](../writing-mstest-tests/SKILL.md) - Write and modernize MSTest tests without major-version migrations.
- [Code testing](../code-testing/SKILL.md) - Generate and repair tests without referrals to removed framework/platform migration workflows.
- [Test anti-patterns](../test-anti-patterns/SKILL.md) - Diagnose test quality without offering an unavailable framework/version migration workflow.
- [Test tagging](../test-tagging/SKILL.md) - Classify/tag tests while keeping framework/platform migrations out of scope.
- [Static dependency migration](../migrate-static-to-wrapper/SKILL.md) - Replace static dependencies with testable abstractions, not test frameworks.

## Repository-maintained MSBuild guidance

Use [msbuild](../msbuild/SKILL.md) as the general entry point.
The legacy `binlog-failure-analysis`, `binlog-generation`, `build-perf-baseline`,
`build-perf-diagnostics`, `eval-performance`, `extension-points`,
`incremental-build`, and `msbuild-antipatterns` skills have compact activation
descriptions; their original scope, routing rules, and full procedures remain in
their bodies.

- [copy-to-output-directory](../copy-to-output-directory/SKILL.md): compatibility redirect to the umbrella's copy-mode reference.

These nine `SKILL.md` files are excluded from upstream curation; supporting
references remain managed. Compare the excluded files explicitly against
`plugins/dotnet-msbuild/skills/` in `dotnet/skills` during refreshes, preserving
their scope and correctness rules.
