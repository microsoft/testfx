<!-- BEGIN gh-copilot-curate managed -->
## Available skills (managed by gh-copilot-curate — do not edit by hand)

Run `gh copilot-curate list` to see installed plugins; run `gh copilot-curate update` to refresh.

### dotnet

- [C# Expert](.agents/skills/csharp-expert/SKILL.md) — _skill_ — Act as the front door for C# and .NET work. Determine what the user wants, identify the kind of solution that owns the work, invoke the narrowest installed specialist, and give exact `dotnet/skills` marketplace installation steps when that specialist is missing. Keep direct C# language guidance as the fallback, not the default.
- [C# Refactoring (behavior-preserving)](.agents/skills/csharp-refactoring/SKILL.md) — _skill_ — A refactor changes **structure**, never observable **behavior**. Do the edit with binding-aware tools, then confirm behavior held with a build + the relevant tests. Keep the effort proportional to the change: a one-line local rename does not need the ceremony a public multi-targeted change does.
- [MSBuild troubleshooting](.agents/skills/msbuild/SKILL.md) — _skill_ — This is the entry skill. Choose **troubleshooting**, **performance**, or **authoring review**, then read only the relevant local references. The references retain the content and identities of the original MSBuild skills, with correctness fixes; they are not separate skills to activate.
- [setup-local-sdk](.agents/skills/setup-local-sdk/SKILL.md) — _skill_ — Guide the user through installing a .NET SDK into a project-local `.dotnet/` directory and wiring it up via the `global.json` `paths` feature (.NET 10+). The examples use .NET 11, but this works with any version — prerelease or stable.

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

- [.NET Test Framework Reference](.agents/skills/dotnet-test-frameworks/SKILL.md) — _skill_ — Language-specific detection patterns for .NET test frameworks (MSTest, xUnit, NUnit, TUnit).
- [Assertion Diversity Analysis](.agents/skills/assertion-quality/SKILL.md) — _skill_ — Analyze test code in any supported language to measure how varied and meaningful the assertions are. Produce a metrics report that reveals whether tests verify different facets of correctness — not just "output equals X" but also structure, exceptions, state transitions, side effects, and invariants.
- [CRAP Score Analysis](.agents/skills/crap-score/SKILL.md) — _skill_ — Calculate CRAP (Change Risk Anti-Patterns) scores for .NET methods to identify code that is both complex and undertested.
- [Code Testing Extensions](.agents/skills/code-testing-extensions/SKILL.md) — _skill_ — This skill provides access to language-specific guidance files used by the code-testing pipeline. Call this skill to get the file paths, then read the relevant file for your target language.
- [Code Testing Generation Skill](.agents/skills/code-testing-agent/SKILL.md) — _skill_ — An AI-powered skill that generates comprehensive, workable unit tests for any programming language using a coordinated multi-agent pipeline.
- [Coverage Analysis](.agents/skills/coverage-analysis/SKILL.md) — _skill_ — Raw coverage percentages answer "what code was executed?" — they don't answer what you actually need to know:
- [Detect Static Dependencies](.agents/skills/detect-static-dependencies/SKILL.md) — _skill_ — Scan a C# codebase for calls to hard-to-test static APIs and produce a ranked report showing which statics appear most frequently, which files are most affected, and which abstractions already exist in the .NET ecosystem to replace them.
- [Generate Testability Wrappers](.agents/skills/generate-testability-wrappers/SKILL.md) — _skill_ — Generate wrapper interfaces, default implementations, and DI service registration code for untestable static dependencies. For statics that already have .NET built-in abstractions (`TimeProvider`, `IHttpClientFactory`), guide adoption of the built-in. For statics without built-in alternatives, generate custom minimal wrappers.
- [Migrate Static to Wrapper](.agents/skills/migrate-static-to-wrapper/SKILL.md) — _skill_ — Perform mechanical, codemod-style replacement of static dependency call sites with calls to injected wrapper interfaces or built-in abstractions. Operates on a bounded scope (single file, project, or namespace) so migrations can be done incrementally.
- [Test Analysis Extensions](.agents/skills/test-analysis-extensions/SKILL.md) — _skill_ — This skill provides access to per-language reference files used by the polyglot test analysis skills. Call this skill to get the list of available extension files, then read the one matching the target codebase's language and test framework.
- [Test Anti-Pattern Detection](.agents/skills/test-anti-patterns/SKILL.md) — _skill_ — Quick, pragmatic analysis of test code in any supported language for anti-patterns and quality issues that undermine test reliability, maintainability, and diagnostic value.
- [Test Gap Analysis via Pseudo-Mutation](.agents/skills/test-gap-analysis/SKILL.md) — _skill_ — Analyze production code in any supported language by reasoning about hypothetical mutations and checking whether existing tests would catch them. This reveals blind spots where tests pass but would continue to pass even if the code were broken.
- [Test Smell Detection](.agents/skills/test-smell-detection/SKILL.md) — _skill_ — Deep formal audit of test code in any supported language using an academic test smell taxonomy. Detects symptoms of bad design or implementation decisions that make tests harder to understand, more fragile, less effective at catching bugs, or more expensive to maintain. Produces a severity-ranked report with specific locations and actionable fixes.
- [Test Trait Tagging](.agents/skills/test-tagging/SKILL.md) — _skill_ — Analyze an existing test suite in any supported language and apply a standardized set of trait tags to each test method, giving teams visibility into their test distribution (positive vs. negative, critical-path coverage, smoke tests, etc.).
- [Builder Agent](.github/agents/code-testing-builder.agent.md) — _agent_ — You build/compile projects and report the results. You are polyglot — you work with any programming language.
- [Fixer Agent](.github/agents/code-testing-fixer.agent.md) — _agent_ — You fix compilation errors in code files. You are polyglot — you work with any programming language.
- [Linter Agent](.github/agents/code-testing-linter.agent.md) — _agent_ — You format code and fix style issues. You are polyglot — you work with any programming language.
- [Test Generator Agent](.github/agents/code-testing-generator.agent.md) — _agent_ — You coordinate test generation using the Research-Plan-Implement (RPI) pipeline. You are polyglot — you work with any programming language.
- [Test Implementer](.github/agents/code-testing-implementer.agent.md) — _agent_ — You implement a single phase from the test plan. You are polyglot — you work with any programming language.
- [Test Planner](.github/agents/code-testing-planner.agent.md) — _agent_ — You create detailed test implementation plans based on research findings. You are polyglot — you work with any programming language.
- [Test Quality Auditor Agent](.github/agents/test-quality-auditor.agent.md) — _agent_ — You are a polyglot test quality auditor. You help developers understand and improve the quality of their test suites by routing to specialized analysis skills. Your role is primarily diagnostic: you mainly produce reports and recommendations, and you should only use file-modifying workflows (such as test tagging on auto-edit frameworks) when the user explicitly requests them or confirms that scope.
- [Test Researcher](.github/agents/code-testing-researcher.agent.md) — _agent_ — You research codebases to understand what needs testing and how to test it. You are polyglot — you work with any programming language.
- [Testability Migration Agent](.github/agents/testability-migration.agent.md) — _agent_ — You are a testability migration agent for .NET codebases. Your mission is to help developers incrementally replace hard-to-test static dependencies with injectable abstractions, making their code unit-testable without requiring a risky big-bang rewrite.
- [Tester Agent](.github/agents/code-testing-tester.agent.md) — _agent_ — You run tests and report the results. You are polyglot — you work with any programming language.

<!-- END gh-copilot-curate managed -->


## Repository-maintained testing skills

These skills retain their testing functionality without referrals to removed migration skills.
They are excluded from upstream curation updates to preserve that routing.

- [MTP hot reload](.agents/skills/mtp-hot-reload/SKILL.md) - Iterate on test fixes without rebuilding.
- [Running .NET tests](.agents/skills/run-tests/SKILL.md) - Detect the current runner and select compatible commands.
- [Test filter syntax](.agents/skills/filter-syntax/SKILL.md) - Reference filter syntax by platform and framework.
- [Test platform detection](.agents/skills/platform-detection/SKILL.md) - Reference runner and framework detection.
- [Writing MSTest tests](.agents/skills/writing-mstest-tests/SKILL.md) - Write and modernize MSTest tests without major-version migrations.
- [Grade Tests](.agents/skills/grade-tests/SKILL.md) - Retain this repository's
  per-test grading rubric, pseudo-mutation analysis, and concrete improvements
  outside the curator's `dotnet-test` selection.
