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
   shipped packages, and linked source files. Apply the context checks below
   before treating a checklist match as a finding.
3. Compare the PR title and description with the actual diff. Verify that the
   stated motivation, behavior change, compatibility impact, and validation
   match what the code does. Apply **Feature decisions and delivery evidence**
   below, including when the PR provides no design rationale.
4. Check changed tests against the test-review guidance below.
5. Report only actionable findings caused by the PR. Prefer a small number of
   high-confidence findings over broad summaries or praise.
6. If the change is correct, do not invent a finding merely to leave feedback.

## Context checks before a finding

Keep comprehensive analysis separate from publication. Check every applicable
dimension, but treat checklist matches and test grades as investigation inputs,
not automatic instructions to comment.

- Read the whole type, including other partial declarations, inherited
  behavior, helpers, and callers. Before calling a member or P/Invoke unused,
  search references across that context and every linked-source consumer;
  absence of a call in the changed file is not evidence of dead code.
- Compare merge-base and HEAD behavior, not just added lines. A moved or
  extracted block that preserves behavior does not introduce its old defects.
  Report it only if the move changes a concrete contract, caller, ordering,
  or compilation context.
- Derive supported TFMs, conditional compilation, polyfills, and language
  versions from the owning projects and linked consumers. A proposed edit
  must work on the oldest relevant target, not just the reviewer's runtime.
- Verify asserted BCL semantics against official documentation, versioned
  implementation source, or a focused repro on the relevant target. Do not
  infer a concrete type's disposal, cancellation, buffering, or thread-safety
  behavior from a base type or a familiar API name. If verification is
  unavailable, record the uncertainty rather than asserting a defect.
- For races and lifetime defects, trace ownership, all reads/writes, and
  happens-before relationships (locks, task completion, channels, publication,
  and serial lifecycle phases). A mutable field, ordinary collection, or
  missing `using` is not itself proof of a race or leak.
- Preserve trusted internal invariants unless a concrete external trigger or
  failing test disproves them. Put validation at the actual trust boundary;
  do not suggest silent recovery just because an invariant could hypothetically
  be violated.

## Review publication

- Publish review feedback as one pull-request review per run. Stage actionable
  line findings as inline review comments, then bundle them with the final
  review submission.
- Put PR-level findings, scope and description feedback, dependency assessments,
  specialist-review summaries, and overflow findings in the final review body.
  Do not post separate top-level PR comments for review content.
- Include a compact `Confidence at a glance` section with one color-coded,
  collapsed block per applicable review scope. Keep the detailed dimensions as
  an internal coverage checklist; do not publish one status row per dimension.
- Lead with measurable reviewer actions when action is needed. Keep detailed
  line-level evidence in inline comments rather than repeating it in the final
  review body.
- Do not duplicate a finding already covered by another live review thread.
  Reference the existing thread from the review body when it remains the only
  actionable item.
- Group findings by root cause, observable consequence, and correction, not by
  file, locale, test, or review dimension. Use one representative changed-line
  anchor and list other affected locations compactly. Separate findings only
  when they require different corrections or have materially different impact.
- Before publishing, read existing review threads and replies, including
  human and other-bot findings. Match the root cause even if the line moved,
  the wording differs, or no workflow marker exists. A new HEAD or outdated
  anchor alone does not justify repeating an unresolved, still-applicable
  finding. Reopen a resolved or declined concern only with new evidence that
  addresses the earlier disposition.
- Keep per-test grades and rubric-only improvements in the combined review
  summary. A below-A grade is not a defect or an inline-publication threshold;
  do not add assertions, churn, or unsupported modernizations just to raise it.
- For automatic specialist checks that are fully clean, prefer `noop` when the
  main review already covers that dimension. Explicit slash-command reviews may
  still publish an informational `COMMENT` review.

These instructions govern reviewers that load this repository guidance.
Separate code-quality-service findings may not consume it; identify the
producer before attributing its output to this skill or claiming a policy
change will affect that service.

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

## Feature decisions and delivery evidence

Use [feature-delivery](../feature-delivery/SKILL.md) as the evidence contract,
not permission to implement, run untrusted PR code, upload media, or publish
additional comments. Read linked decision records and available run artifacts;
author claims and a checked template box are not execution proof.

- For new features, check that the versioned design/RFC/decision record captures
  sources, approaches actually tried and results, alternatives, assumptions,
  and why the chosen direction won. Compare it with the diff and revision history.
  Do not assume an absent rationale means the design is wrong.
- Challenge material design choices even when no explanation was provided:
  trace consumers and constraints, compare the existing/no-change design and
  a simpler alternative, and examine ownership/lifecycle, failure/cancellation,
  compatibility, observability, and unnecessary public APIs/abstractions.
  Ask for the specific missing contract or evidence when a consequential
  assumption cannot be established. Do not demand an arbitrary redesign.
- Check demonstrations for user-visible features, including terminal/report
  changes: actual screenshots or short GIF/video where practical, captions,
  reproduction steps, source/package provenance, and accessible text.
  Accept a clearer transcript/artifact or an explained unavailable/inapplicable
  capture. A diagram alone is not a running-feature demonstration; media is not
  test execution or shipping-product proof. Never request public sensitive data.
- For behavioral regression coverage, verify the same tests/assertions/selection
  were retained without the production change, executed, and failed at the
  intended assertion, then executed and passed with it. Check source states,
  commands, exits, identities/counts, and artifacts; repack/provenance matters
  for package consumers. Build/restore/launch failures, skips, and zero tests
  are not red-phase proof. A compiling behavior-removal/mutation check is valid
  when accurately labeled; a new API's baseline compilation failure alone is not.
- Accept green/green equivalence for behavior-preserving changes. For a test-only
  safety net protecting correct behavior, look for a relevant compiling mutation
  demonstrating sensitivity. Documentation-only changes need no product red/green
  or media. Mark infeasible behavioral comparisons as sensitivity unproven.
- Judge titles by descriptive accuracy, not `[feat]` or other category prefixes.
  Preserve existing automation prefixes; do not infer compatibility from a tag.

Put material missing decision, demonstration, or regression evidence in the
review summary with the exact gap and smallest useful follow-up. Missing evidence
is not a proven code defect, does not justify an apply-ready inline suggestion,
and must not trigger `REQUEST_CHANGES` solely because a record or media is absent.
Avoid ceremonial documentation demands when evidence is already available.

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
- Workflow, script, issue-template, and policy changes that create or triage
  issues must use native GitHub Issue Types and must not introduce the
  deprecated `type/bug`, `type/feature`, or `type/task` labels.
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
- Check actual red/green or deliberate-mutation evidence under **Feature decisions
  and delivery evidence**, not only a hypothetical claim that the test would fail.
- Async assertions and operations are awaited.
- Deterministic multiline output is checked with one exact raw-string
  (`"""..."""`) expectation, not multiple substring assertions or escaped
  line breaks. Where surrounding output is genuinely variable, match a
  complete deterministic raw-string block rather than individual lines.
  Preserve ordering, indentation, blank lines, and trailing newlines.
  Normalize both operands when platform-native output line endings can differ
  from the source checkout's raw-string line endings.
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
- Analyzer changes:
  - Verify diagnostic IDs, severity, generated-code behavior, false-positive
    risk, analyzer release tracking, and code-fix registration and properties.
  - For every mapping from a source API or annotation domain to a target API or
    runtime domain, classify the mapping as **exact**, **compatible/coarser**, or
    **unrepresentable**. Check all source-value polarities and supported target
    versions; do not infer equivalence from one successful case.
  - Separate compile-time annotation semantics from runtime enforcement. Verify
    what the analyzer can prove from symbols and metadata independently from
    what the target framework or platform actually enforces at execution time.
  - Check descriptor wording against the classification. Use "equivalent" only
    for exact mappings; describe lossy but behaviorally acceptable mappings as
    compatible and make any semantic loss explicit.
  - Every diagnostic without a code fix must have a safe, concrete manual edit
    that clears the diagnostic while preserving the relevant behavior. If no
    such edit exists for a valid triggering program, question whether the
    diagnostic is actionable rather than treating the expected diagnostic as
    proof of correctness.

## Finding quality

Every finding must:

- Identify a concrete scenario that triggers the problem.
- Explain the observable consequence.
- Point to a changed line for code findings.
- Put PR-level scope, description, split, and multi-model-review feedback in the
  overall review summary and identify the specific unsupported claim,
  unexplained change group, or high-risk boundary.
- Recommend the smallest safe correction.
- Establish that the correction improves observable correctness, reliability,
  diagnostics, compatibility, or maintainability in this context, rather than
  merely satisfying a rubric or replacing valid code with a preferred idiom.
- Distinguish a verified defect from a material evidence gap. Put unresolved
  validation questions in the summary with the exact missing evidence; do not
  present them as proven bugs or apply-ready inline fixes.
- Apply **Feature decisions and delivery evidence** to relevant candidates:
  missing rationale/media/red-phase proof is an evidence gap, not an automatic
  correctness finding. Do not claim a demonstration, baseline failure, or passing
  execution that the available artifacts do not establish.
- Use severity proportional to impact; do not elevate maintainability or style
  suggestions into correctness findings.

Do not submit an approval on behalf of repository maintainers. A clean review
may recommend approval in its summary while remaining a comment review.

## Calibration regression examples

Use these bounded cases when changing reviewer guidance. Evaluate the expected
publication decision as well as coverage; these are examples, not findings to
post on their originating PRs.

| Context | Expected decision |
|---------|-------------------|
| A P/Invoke in one partial has callers in another partial or linked consumer. | No unused-member finding; retain the reference trace. |
| A new private P/Invoke has no references in the whole type or any consumer, and removing it safely eliminates a redundant declaration. | One maintainability finding with the verified search scope and safe deletion. |
| A proposed modern API is unavailable on the oldest compiled TFM. | Reject the suggestion; retain the compatibility check and use supported prior art if a real defect remains. |
| Identical code moves to a helper with unchanged callers, ordering, and target context. | No newly introduced behavior finding. |
| A move causes cleanup to precede the last read from a stream whose read API rejects disposal. | Publish the lifetime defect with the call sequence and verified API contract. |
| `MemoryStream.ToArray()` is called after disposal. | No disposal finding: the [official contract](https://learn.microsoft.com/dotnet/api/system.io.memorystream.toarray#remarks) explicitly permits a closed stream. |
| A `Dictionary` is built before task publication, then only read by workers; a mutable field is handed off through awaited task completion. | No race finding without an overlapping unsafe access; verify the ordering. |
| Two concurrent callbacks mutate the same `Dictionary` without coordination. | Publish one race finding with the reachable interleaving and a correction that preserves compound operations. |
| An internal guard enforces a trusted synchronous protocol; no external trigger can violate it. | Preserve the guard; do not invent tolerant recovery. |
| Ten locale files or tests share one defective generator/helper. A live thread already explains that defect. | One root-cause finding if novel; otherwise reference the live thread, not ten new comments. |
| A B-grade test has one exact assertion that completely protects its narrow contract. | Keep the grade in the scorecard; no inline demand for redundant assertions. |
| A changed test only asserts non-null; returning the wrong contents survives and contradicts its stated contract. | Keep the grade and publish one actionable assertion improvement with the specific surviving mutation and expected contents. |
| A feature has a maintained decision record, a captioned terminal recording/transcript with provenance, and the same behavioral tests fail without the production change and pass with it. | Evidence contract satisfied; no ceremonial request for a different title prefix, another document, or more media. |
| A feature has no design rationale and introduces a lifetime assumption the consumers do not establish. | Investigate the contract and simpler alternatives; put the exact unresolved assumption in the summary. Publish a defect only if a concrete failing lifetime is established. |
| A PR claims regression proof, but the baseline cannot compile a newly added API. | Baseline red-phase proof is unproven; request a compiling behavior-removal/mutation check in the summary, not an inline fix or a claimed test failure. |
| Red-phase artifacts show the intended assertion failure, but the package consumer resolved the modified package in both runs. | Comparison provenance is insufficient; request distinct rebuilt/repacked source-state evidence. |
| A baseline command exits nonzero because restore failed, or reports only skipped/zero selected tests. | No behavioral red-phase proof; identify the execution gap in the summary without inventing a code defect. |
| A behavior-preserving refactor has focused green-before/green-after runs and no useful visual surface. | Accept equivalence and explained inapplicable media; do not require a fabricated red run or screenshot. |
| A test-only safety net passes before and after, and a relevant compiling mutation fails its intended assertion. | Accept green/green plus labeled mutation sensitivity; do not claim the old production state was defective. |
| A new user-visible feature lacks media or a decision record, but correctness is otherwise established. | Identify useful missing evidence proportionately in the summary; do not block solely for absent media/record or create an inline code finding. |
