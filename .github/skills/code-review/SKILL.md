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
3. Compare the PR title and description with the actual diff. Verify that the
   stated motivation, behavior change, compatibility impact, and validation
   match what the code does.
4. Check changed tests against the test-review guidance below.
5. Report only actionable findings caused by the PR. Prefer a small number of
   high-confidence findings over broad summaries or praise.
6. If the change is correct, do not invent a finding merely to leave feedback.

## PR scope and review depth

Be constructively critical. Challenge the change's assumptions, failure modes,
and claimed validation rather than accepting the implementation or description
at face value. Criticism must still identify concrete evidence and impact; do
not manufacture objections for the sake of sounding rigorous. Every criticism
must meet the concrete-scenario and observable-consequence bar in
`Finding quality`.

- Check whether the PR combines independent features, refactors, fixes, or
  formatting changes that have different motivations or could be reviewed,
  reverted, and shipped separately. When this materially obscures behavior or
  increases review risk, recommend splitting the PR and identify the distinct
  change groups.
- Check that every material diff is explained by the PR description and that
  every claimed behavior is implemented. Flag hidden scope, stale claims,
  understated compatibility or operational impact, and validation claims not
  supported by the changed tests or available evidence. A missing or brief
  description is not itself a defect; raise it only when it hides material
  behavior, compatibility impact, or an unsupported claim.
- Distinguish necessary supporting changes from unrelated cleanup. Do not
  request a split merely because a coherent change spans many files or product
  layers.
- Put scope, description-alignment, split, and additional-review feedback only
  in the top-level review summary, never on an arbitrary changed line.
- Request an independent multi-model review sparingly, only for high-blast-
  radius changes such as:
  - Wire-level IPC contracts or serialization protocols shared with external
    clients.
  - Structural redesigns of core test-execution scheduling, synchronization,
    cancellation, or `ExecutionContext` propagation.
  - New process-launch, arbitrary-code-execution, privilege, or other security
    boundaries.
  - Cross-product changes with independent compatibility or rollback risks.
- Do not request a multi-model review for routine public API additions, bug
  fixes, analyzer changes, adapter features, or test-only changes. Put the
  request in the review summary and name the exact failure mode, compatibility
  concern, race, or attack surface the additional models should examine.

## Core TestFx checks

- Preserve backward compatibility for public APIs, protocols, command-line
  options, package behavior, and analyzer diagnostics.
- Treat overload additions as potentially source-breaking. Check representative
  call shapes and older supported language versions, not only the repository's
  preview language version.
- New public API must be minimal, documented, MUST NOT use `init`, and be
  recorded in the relevant `PublicAPI.Unshipped.txt`. Also check internal API
  baselines in projects that track them.
- Every API marked `[Experimental]` must include this sentence in its XML
  documentation `<remarks>`: `This API is experimental. It may change, break,
  or be removed at any time without notice.`
- Check every target framework affected by the change. Do not assume an API
  available on modern .NET exists on older targets.
- Review concurrency and lifecycle ordering carefully. Test execution is
  parallel, and shared mutable state, cancellation, disposal, and
  `ExecutionContext` flow are common correctness boundaries.
- For shared or linked source, identify every consuming project and ensure API
  baselines and behavior remain consistent in all of them.
- User-facing strings belong in resources. Never accept manually edited XLF
  files. Every `{Locked="..."}` token must occur verbatim in the corresponding
  value; verify it does not accidentally lock a substring of a translatable
  word.
- CLI option changes must update the matching `--help` and `--info` acceptance
  expectations, including punctuation and alphabetical ordering.
- Packable projects need valid package descriptions and `PACKAGE.md`.
- Never accept manual changes under `eng/common/`; those files are mirrored
  from `dotnet/arcade` and overwritten by automation.
- Do not weaken `ApplicationStateGuard.Unreachable()`, `Debug.Assert`, or
  explicit invariant throws into silent fallbacks, warnings, or empty returns
  without a concrete external trigger or failing test that disproves the
  invariant.
- Workflow, script, issue-template, and policy changes must use native GitHub
  Issue Types and must not introduce the deprecated `type/bug`, `type/feature`,
  or `type/task` labels.
- Do not allow untracked `TODO` comments.

## Security checks

Review changed trust boundaries for concrete security regressions. Pay particular
attention when the change handles paths, process arguments, environment
variables, protocol messages, serialized data, logs, artifacts, credentials,
reflection, extensions, or arbitrary user test code.

- Ensure untrusted file and directory names cannot escape the intended root
  through traversal, rooted paths, alternate separators, or symlink behavior.
- Ensure process launches preserve argument boundaries and do not concatenate
  untrusted values into a command line, shell command, or executable path.
  Check executable resolution, working-directory selection, inherited
  environment variables, and assembly or plugin search paths.
- Validate untrusted protocol and serialized input before using it, including
  message type, required fields, payload and collection bounds, and unsupported
  values. Reject unsafe polymorphic or binary deserialization of untrusted data.
- Treat reflection, user callbacks, test methods, data sources, extensions, and
  assembly discovery as hostile-code boundaries. Ensure exceptions are
  contained appropriately, cancellation and timeouts remain enforceable, and
  user-controlled input cannot cause unbounded memory or resource growth.
- Do not expose secrets, tokens, environment values, private paths, or sensitive
  test data through logs, exceptions, reports, artifacts, telemetry, or
  temporary files.
- Check that temporary files and artifacts use safe locations, appropriate
  access, collision-resistant names, and reliable cleanup.
- Treat workflow permission, network access, action pin, and secret-handling
  changes as security boundaries; reject unnecessary privilege expansion or
  mutable third-party references.
- For dependency changes, check whether the new package or version introduces a
  known vulnerability, unexpected runtime asset, or broader transitive surface.
  Use authoritative evidence such as official advisories, NuGet metadata, and
  upstream release notes; preserve uncertainty rather than guessing.

Report a security finding only when the changed code creates a plausible attack
or disclosure scenario with an observable consequence. Defensive coding belongs
at actual external boundaries; do not request blanket hardening of trusted
internal invariants without a concrete external trigger.

For a suspected vulnerability or security-driven dependency update, do not put
proofs of concept, attacker payloads, exploit steps, CVE details, affected
version ranges, or other actionable vulnerability details in public review
comments or PR metadata. Leave at most a minimal public note and follow
`SECURITY.md` and the repository's private reporting process.

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
  `.github/agents/msbuild-reviewer.agent.md`. Ignore its operating modes,
  finding cap, orchestration instructions, and output contract; the GitHub
  code-review service owns review formatting and posting.
- Broad TestFx architecture, runtime, API, performance, compatibility,
  security-boundary, IPC, process-launch, serialization, and artifact-handling
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
- Point to a changed line for code findings.
- Put PR-level scope, description, split, and multi-model-review feedback in the
  overall review summary and identify the specific unsupported claim,
  unexplained change group, or high-risk boundary.
- Recommend the smallest safe correction.
- Use severity proportional to impact; do not elevate maintainability or style
  suggestions into correctness findings.

Do not submit an approval on behalf of repository maintainers. A clean review
may recommend approval in its summary while remaining a comment review.
