## Repository-owned test guidance

The curated `dotnet-test` installation uses the upstream grading rubric,
including its result states, concrete improvement suggestions, and read-only
per-test pseudo-mutation assessment. TestFx-specific review conventions live
in `.github/workflows/shared/test-reviewer-shared.md`, not a fork of the skill.

The installation deliberately excludes the repository-owned review corrections
below so a refresh cannot silently overwrite them:

- [Code testing reference helper](.agents/skills/code-testing-extensions/SKILL.md),
  [test analysis reference helper](.agents/skills/test-analysis-extensions/SKILL.md),
  and [filter syntax reference](.agents/skills/filter-syntax/SKILL.md)
  keep internal language guidance callable by the model while hidden from the
  user menu.
- [Python generation guidance](.agents/skills/code-testing-extensions/extensions/python.md)
  preserves requested regression tests and reports production or tooling blockers.
- [.NET generation guidance](.agents/skills/code-testing-extensions/extensions/dotnet.md)
  distinguishes VSTest command mode from native MTP command mode.
- [Coverage setup discovery](.agents/skills/coverage-analysis/references/setup-discovery.md)
  recognizes `.slnx`, preserves the selected repository/user entry point, and
  bounds test-project discovery to that root and its containing Git repository.
- [Test project scaffolding](.agents/skills/scaffold-dotnet-test-project/SKILL.md)
  adds bridge properties only for VSTest command mode.
- [PowerShell pipeline example](.agents/skills/code-testing-extensions/extensions/powershell-examples.md)
  loads module-defined types at parse time, uses valid Pester identity checks,
  and covers the planned missing-invoice path.
- [C++ pipeline example](.agents/skills/code-testing-extensions/extensions/cpp-examples.md)
  includes the planned missing-invoice case and its required Catch2 header.
- [Kotlin](.agents/skills/code-testing-extensions/extensions/kotlin-examples.md),
  [Ruby](.agents/skills/code-testing-extensions/extensions/ruby-examples.md), and
  [Rust](.agents/skills/code-testing-extensions/extensions/rust-examples.md)
  examples include the planned missing-invoice case and align their reports
  with the actual test counts.

These corrections are relative to upstream commit
`e468462d8a0900f5278331c1ae12f45e015bb656`. Compare each excluded file explicitly
on refresh and remove its exception once upstream carries the correction.
These excluded files are not checked by `gh copilot-curate verify`; that check
still verifies the remaining imported files. They do not fork the grading rubric.

The installation also excludes
[Find-UntestedSources.cs](.agents/skills/find-untested-sources/scripts/Find-UntestedSources.cs).
It matches upstream commit `0608d8924cd3173411e36650a7a01b4063e1f55a` except
for the UTF-8 BOM required by `.editorconfig`.

This helper is repository-owned, not checked by `gh copilot-curate verify`.
Compare it explicitly when refreshing upstream and preserve its C# encoding.
The manifest's include list keeps updates from overwriting it.

[Test generation guidance](.github/skills/code-testing/unit-test-generation.prompt.md)
adapts upstream agents' relative links to the `.agents/skills` installation
layout. The legacy [code-testing-generator](.github/agents/code-testing-generator.agent.md)
agent remains available for existing workflows; new upstream guidance uses
[test-engineer](.github/agents/test-engineer.agent.md). The retained generator
resolves one absolute non-stageable state directory for phased work and passes
its canonical artifact paths through each worker, retry, and iteration.

## Repository-maintained testing skills

These skills retain their testing functionality without referrals to removed
framework/platform migration skills. They are excluded from upstream curation
updates to preserve that routing.

The [test-engineer agent](.github/agents/test-engineer.agent.md) is also
repository-maintained so its routing cannot restore the removed migration
dispatcher. Framework/platform migration plugins remain intentionally absent.

- [MTP hot reload](.agents/skills/mtp-hot-reload/SKILL.md) - Iterate on test fixes without rebuilding.
- [Running .NET tests](.agents/skills/run-tests/SKILL.md) - Detect the current runner and select compatible commands.
- [Test filter syntax](.agents/skills/filter-syntax/SKILL.md) - Reference filter syntax by platform and framework.
- [Test platform detection](.agents/skills/platform-detection/SKILL.md) - Reference runner and framework detection.
- [Writing MSTest tests](.agents/skills/writing-mstest-tests/SKILL.md) - Write and modernize MSTest tests without major-version migrations.

<!-- BEGIN gh-copilot-curate managed -->
## Available skills (managed by gh-copilot-curate — do not edit by hand)

Run `gh copilot-curate list` to see installed plugins; run `gh copilot-curate update` to refresh.

### dotnet-msbuild

- [Analyzing MSBuild Failures with Binary Logs](.agents/skills/binlog-failure-analysis/SKILL.md) — _skill_ — This skill diagnoses MSBuild build failures from a `.binlog` file. The preferred path uses the **binlog MCP server** (`Microsoft.AITools.BinlogMcp`, exposed under the `binlog` MCP namespace) which is bundled with this plugin. If the MCP server is not available, fall back to the **binlog replay** workflow at the bottom.
- [Build Performance Baseline & Optimization](.agents/skills/build-perf-baseline/SKILL.md) — _skill_ — Before optimizing a build, you need a **baseline**. Without measurements, optimization is guesswork. This skill covers how to establish baselines and apply systematic optimization techniques.
- [Custom Target Authoring Patterns](.agents/skills/target-authoring/SKILL.md) — _skill_ — Canonical patterns from `Microsoft.Common.CurrentVersion.targets` in the MSBuild repository.
- [Detecting OutputPath and IntermediateOutputPath Clashes](.agents/skills/check-bin-obj-clash/SKILL.md) — _skill_ — This skill helps identify when multiple MSBuild project evaluations share the same `OutputPath` or `IntermediateOutputPath`. This is a common source of build failures including:
- [Generate Binary Logs](.agents/skills/binlog-generation/SKILL.md) — _skill_ — **Pass the `/bl` switch when running any MSBuild-based command.** This is a non-negotiable requirement for all .NET builds.
- [Including Generated Files Into Your Build](.agents/skills/including-generated-files/SKILL.md) — _skill_ — Files generated during the build are generally ignored by the build process. This leads to confusing results such as: - Generated files not being included in the output directory - Generated source files not being compiled - Globs not capturing files created during the build
- [MSBuild Anti-Pattern Catalog](.agents/skills/msbuild-antipatterns/SKILL.md) — _skill_ — A numbered catalog of common MSBuild anti-patterns. Each entry follows the format:
- [MSBuild Extension Points](.agents/skills/extension-points/SKILL.md) — _skill_ — How the MSBuild pipeline provides hooks for SDKs, NuGet packages, repos, and users to inject custom logic.
- [MSBuild Item Management Patterns](.agents/skills/item-management/SKILL.md) — _skill_ — Canonical patterns for working with item groups, from `Microsoft.Common.CurrentVersion.targets`.
- [MSBuild Modernization: Legacy to SDK-style Migration](.agents/skills/msbuild-modernization/SKILL.md) — _skill_ — **Legacy indicators:**
- [MSBuild Property Patterns](.agents/skills/property-patterns/SKILL.md) — _skill_ — Canonical property definition and manipulation patterns from the MSBuild repository.
- [MSBuild Server for CLI Caching](.agents/skills/msbuild-server/SKILL.md) — _skill_ — Use the MSBuild Server to cache evaluation results across CLI builds, matching the performance advantage Visual Studio gets from its long-lived MSBuild process.
- [Misleading ResolveProjectReferences Time](.agents/skills/resolve-project-references/SKILL.md) — _skill_ — Prevent misguided optimization of `ResolveProjectReferences` by explaining that its reported time is wall-clock wait time, not CPU work.
- [Organizing Build Infrastructure with Directory.Build Files](.agents/skills/directory-build-organization/SKILL.md) — _skill_ — Understanding which file to use is critical. They differ in **when** they are imported during evaluation:
- [SKILL](.agents/skills/build-parallelism/SKILL.md) — _skill_ — - `/maxcpucount` (or `-m`): number of worker nodes (processes) - Default: 1 node (sequential!). Always use `-m` for parallel builds - Recommended: `-m` without a number = use all logical processors - Each node builds one project at a time - Projects are scheduled based on dependency graph
- [SKILL](.agents/skills/build-perf-diagnostics/SKILL.md) — _skill_ — 1. **Generate a binlog**: `dotnet build /bl:{} -m` 2. Use the **binlog MCP server** (`Microsoft.AITools.BinlogMcp`, exposed under the `binlog` MCP namespace) which is bundled with this plugin
- [SKILL](.agents/skills/eval-performance/SKILL.md) — _skill_ — For a comprehensive overview of MSBuild's evaluation and execution model, see [Build process overview](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview).
- [SKILL](.agents/skills/incremental-build/SKILL.md) — _skill_ — MSBuild's incremental build mechanism allows targets to be skipped when their outputs are already up to date, dramatically reducing build times on subsequent runs.
- [Build Performance Agent](.github/agents/build-perf.agent.md) — _agent_ — You are a specialized agent for diagnosing and optimizing MSBuild build performance. You actively run builds, analyze binlogs, and provide data-driven optimization recommendations.
- [MSBuild Code Review Agent](.github/agents/msbuild-code-review.agent.md) — _agent_ — You are a specialized agent that reviews MSBuild project files for quality, correctness, and adherence to modern best practices. You actively scan files and produce actionable recommendations.
- [MSBuild Expert Agent](.github/agents/msbuild.agent.md) — _agent_ — You are an expert in MSBuild, the Microsoft Build Engine used by .NET and Visual Studio. You help developers run builds, diagnose build failures, optimize build performance, and resolve common MSBuild issues.

### dotnet-test

- [Assertion Diversity Analysis](.agents/skills/assertion-quality/SKILL.md) — _skill_ — Analyze test code in any supported language to measure how varied and meaningful the assertions are. Produce a metrics report that reveals whether tests verify different facets of correctness — not just "output equals X" but also structure, exceptions, state transitions, side effects, and invariants.
- [CRAP Score Analysis](.agents/skills/crap-score/SKILL.md) — _skill_ — Calculate CRAP (Change Risk Anti-Patterns) scores for .NET methods to identify code that is both complex and undertested.
- [Code Testing Skill](.agents/skills/code-testing/SKILL.md) — _skill_ — The reliable implicit entry point for generating, repairing, and strengthening tests. It handles focused work directly and invokes the public `test-engineer` agent for broad or multi-stage requests.
- [Coverage Analysis](.agents/skills/coverage-analysis/SKILL.md) — _skill_ — Explain what .NET coverage evidence proves, reconcile target arithmetic, and identify the code blocking progress. Add complexity/CRAP ranking only when the user explicitly asks for risk hotspots, CRAP, priorities by risk, or refactoring safety.
- [Detect Static Dependencies](.agents/skills/detect-static-dependencies/SKILL.md) — _skill_ — Scan a C# codebase for calls to hard-to-test static APIs and produce a ranked report showing which statics appear most frequently, which files are most affected, and which abstractions already exist in the .NET ecosystem to replace them.
- [Find Untested Sources](.agents/skills/find-untested-sources/SKILL.md) — _skill_ — Coverage tools answer "which lines were executed?" — they require a green build and a passing test run, which is minutes-to-tens-of-minutes on a real repo. The question this skill answers is different and much cheaper:
- [Generate Testability Wrappers](.agents/skills/generate-testability-wrappers/SKILL.md) — _skill_ — Generate wrapper interfaces, default implementations, and DI service registration code for untestable static dependencies. For statics that already have .NET built-in abstractions (`TimeProvider`, `IHttpClientFactory`), guide adoption of the built-in. For statics without built-in alternatives, generate custom minimal wrappers.
- [Grade Tests](.agents/skills/grade-tests/SKILL.md) — _skill_ — Assess a curated list of test methods and produce a compact, PR-comment-friendly report. The primary result is one of **Pass**, **Failed**, **Uncertain**, or **Not applicable**; an A-F quality grade remains secondary diagnostic information. The skill **does not discover tests on its own** — the caller (typically a PR automation workflow or a human reviewer holding a specific list) provides the tests or a bounded diff to assess.
- [Legacy Code Testing Alias](.agents/skills/code-testing-agent/SKILL.md) — _skill_ — This skill preserves explicit calls that still use the former `code-testing-agent` name. The public custom agent is `test-engineer`; the model-facing implicit skill is `code-testing`.
- [Migrate Static to Wrapper](.agents/skills/migrate-static-to-wrapper/SKILL.md) — _skill_ — Perform mechanical, codemod-style replacement of static dependency call sites with calls to injected wrapper interfaces or built-in abstractions. Operates on a bounded scope (single file, project, or namespace) so migrations can be done incrementally.
- [Resolve a Testability Obstacle](.agents/skills/testability-obstacle/SKILL.md) — _skill_ — Introduce the smallest behavior-preserving seam needed to test a specific C# behavior, then add deterministic tests that prove both the behavior and the seam. The production edit is a means to the requested test, not an invitation to redesign adjacent code.
- [Test Anti-Pattern Detection](.agents/skills/test-anti-patterns/SKILL.md) — _skill_ — Quick, pragmatic analysis of test code in any supported language for anti-patterns and quality issues that undermine test reliability, maintainability, and diagnostic value.
- [Test Gap Analysis](.agents/skills/test-gap-analysis/SKILL.md) — _skill_ — Answer one question: **which caller-visible production behaviors could change without an existing test failing?** Mutation reasoning is a probe, not the goal. Inventory public outcomes first, then verify only credible gaps.
- [Test Smell Detection](.agents/skills/test-smell-detection/SKILL.md) — _skill_ — Audit test code with the academic taxonomy, code evidence, calibrated framework idioms, and fixes native to the codebase.
- [Test Trait Tagging](.agents/skills/test-tagging/SKILL.md) — _skill_ — Analyze an existing test suite in any supported language and apply a standardized set of trait tags to each test method, giving teams visibility into their test distribution (positive vs. negative, critical-path coverage, smoke tests, etc.).
- [Builder Agent](.github/agents/code-testing-builder.agent.md) — _agent_ — You build/compile projects and report the results. You are polyglot — you work with any programming language.
- [Fixer Agent](.github/agents/code-testing-fixer.agent.md) — _agent_ — You fix compilation errors in code files. You are polyglot — you work with any programming language.
- [Linter Agent](.github/agents/code-testing-linter.agent.md) — _agent_ — You format code and fix style issues. You are polyglot — you work with any programming language.
- [Test Implementer](.github/agents/code-testing-implementer.agent.md) — _agent_ — You implement a single phase from the test plan. You are polyglot — you work with any programming language.
- [Test Planner](.github/agents/code-testing-planner.agent.md) — _agent_ — You create detailed test implementation plans based on research findings. You are polyglot — you work with any programming language.
- [Test Quality Auditor Agent](.github/agents/test-quality-auditor.agent.md) — _agent_ — Produce a bounded, evidence-based health assessment of an existing test suite. This agent is diagnostic: do not edit production or test files unless the user explicitly requests a separate fixing workflow. Never recommend testability migration when repository guidance prohibits production seams or wrappers.
- [Test Researcher](.github/agents/code-testing-researcher.agent.md) — _agent_ — You research codebases to understand what needs testing and how to test it. You are polyglot — you work with any programming language.
- [Testability Migration Agent](.github/agents/testability-migration.agent.md) — _agent_ — You are a testability migration agent for .NET codebases. Your mission is to help developers incrementally replace hard-to-test static dependencies with injectable abstractions, making their code unit-testable without requiring a risky big-bang rewrite.
- [Tester Agent](.github/agents/code-testing-tester.agent.md) — _agent_ — You run tests and report the results. You are polyglot — you work with any programming language.

<!-- END gh-copilot-curate managed -->
