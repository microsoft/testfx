---
name: code-review
description: Review pull requests for MSTest and Microsoft.Testing.Platform with TestFx-specific checks. Use for every GitHub Copilot code review in this repository, with extra scrutiny for tests, public APIs, analyzers, MSBuild, localization, and agentic workflows.
---

# TestFx Code Review

Review the pull request as a maintainer of MSTest and Microsoft.Testing.Platform.
Read `.github/copilot-instructions.md` first and treat it as authoritative.

Focus on defects introduced by the pull request. Do not report pre-existing
issues, speculative concerns without a concrete failure mode, or style
preferences already enforced by automation. Read enough surrounding code and
tests to understand the changed behavior before commenting.

## Review process

1. Identify the product area from the changed paths:
   - `src/Platform` and related tests: Microsoft.Testing.Platform and extensions.
   - `src/TestFramework`: the public MSTest framework API.
   - `src/Adapter`: MSTest adapters and platform services.
   - `src/Analyzers`: Roslyn analyzers and code fixes.
   - `src/Package/MSTest.Sdk`, `eng`, and MSBuild files: build and packaging.
2. Trace each behavior change through its callers, tests, target frameworks,
   shipped packages, and linked source files.
3. Check changed tests against the test-review guidance below.
4. Report only actionable findings on changed lines. Prefer a small number of
   high-confidence findings over broad summaries or praise.
5. If the change is correct, do not invent a finding merely to leave feedback.

## Core TestFx checks

- Preserve backward compatibility for public APIs, protocols, command-line
  options, package behavior, and analyzer diagnostics.
- Treat overload additions as potentially source-breaking. Check representative
  call shapes and older supported language versions, not only the repository's
  preview language version.
- New public API must be minimal, documented, MUST NOT use `init`, and be
  recorded in the relevant `PublicAPI.Unshipped.txt`. Also check internal API
  baselines in projects that track them.
- Check every target framework affected by the change. Do not assume an API
  available on modern .NET exists on older targets.
- Review concurrency and lifecycle ordering carefully. Test execution is
  parallel, and shared mutable state, cancellation, disposal, and
  `ExecutionContext` flow are common correctness boundaries.
- For shared or linked source, identify every consuming project and ensure API
  baselines and behavior remain consistent in all of them.
- User-facing strings belong in resources. Never accept manually edited XLF
  files; verify resource lock markers do not accidentally lock substrings.
- CLI option changes must update the matching `--help` and `--info` acceptance
  expectations, including punctuation and alphabetical ordering.
- Packable projects need valid package descriptions and `PACKAGE.md`.
- Do not allow untracked `TODO` comments.

## Test changes

For changed test code, apply the relevant rules from:

- `.agents/skills/test-anti-patterns/SKILL.md`
- `.agents/skills/assertion-quality/SKILL.md`
- `.agents/skills/test-gap-analysis/SKILL.md`

Use those files as review checklists, not as a request for a repository-wide
audit. Keep analysis scoped to the changed behavior and its directly related
tests.

In particular, verify:

- Tests would fail if the production behavior were wrong; reject missing,
  tautological, self-comparing, or only-trivial assertions.
- Async assertions and operations are awaited.
- Tests are isolated under parallel execution and restore environment, culture,
  static state, files, and other process-wide state.
- Synchronization is deterministic. Avoid sleeps and fragile duration
  assertions when a marker, event, or rendezvous can prove the behavior.
- Acceptance tests that share a generated mutable asset are marked
  `[DoNotParallelize]`; do not require it when the state is execution-context
  local or otherwise isolated.
- Duration output uses `AcceptanceAssert.DurationPattern` rather than assuming a
  millisecond-only format.
- The test framework and assertion library match the project's
  `BannedSymbols.txt` and repository conventions.
- MSTest framework unit tests use `TestFramework.ForTestingMSTest`; MTP and
  analyzer tests use MSTest; adapter tests use their established assertion
  library.
- Test changes cover negative paths, boundaries, cleanup, cancellation, and
  failure behavior relevant to the production change.

## Specialist review routing

Apply these additional repository resources when their paths are changed:

- MSBuild files (`*.props`, `*.targets`, `*.csproj`, SDK and NuGet build
  extensions): use the rule catalog in
  `.github/agents/msbuild-reviewer.agent.md`. Use its review criteria only; the
  GitHub code-review service owns posting comments.
- Broad TestFx architecture, runtime, API, performance, and compatibility
  changes: use the applicable review dimensions in
  `.github/agents/expert-reviewer.agent.md`. Ignore workflow-specific posting,
  attribution, and safe-output instructions in that file.
- Agentic workflow Markdown: require strict compilation, regenerated lock files,
  unchanged trusted action pins, and the repository action-pin audit.
- Analyzer changes: verify diagnostic IDs, severity, messages, code-fix
  equivalence, generated-code behavior, false-positive risk, and analyzer
  release tracking.

## Finding quality

Every finding must:

- Identify a concrete scenario that triggers the problem.
- Explain the observable consequence.
- Point to a changed line.
- Recommend the smallest safe correction.
- Use severity proportional to impact; do not elevate maintainability or style
  suggestions into correctness findings.

Do not submit an approval on behalf of repository maintainers. A clean review
may recommend approval in its summary while remaining a comment review.
