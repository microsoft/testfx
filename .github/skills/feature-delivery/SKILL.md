---
name: feature-delivery
description: Keep TestFx feature research and decision records, challenge material design choices, demonstrate user-visible behavior, and prove regression-test sensitivity with red/green comparisons. Use before new features or material design decisions, when adding regression coverage, and when preparing feature PR evidence.
---

# Feature delivery evidence

Read [repository instructions](../../copilot-instructions.md) and apply
[session-delivery-hygiene](../session-delivery-hygiene/SKILL.md). This skill
does not authorize publication, new dependencies, or unrelated changes.
Use proportionate evidence; do not turn a small fix into a repository-wide audit.

## 1. Start and maintain the decision record

For every new feature, extend its existing design document/RFC or create
`docs/decisions/<feature-name>.md`. Keep it versioned with the implementation
and link it from the PR so the reasoning survives the session. Do not create
duplicate records; non-feature fixes can use existing issue/PR rationale.
Keep temporary logs and captures in task-owned artifact storage, not in source
control; preserve concise findings and accessible evidence references in the
record. Do not include secrets, personal data, or public vulnerability details.

Start before coding. Update after meaningful research, experiments, decisions,
and direction changes, not just at handoff. Distinguish research, proposed
experiments, experiments actually run, and results; never invent attempts.
Record unsuccessful approaches too, including why they were abandoned.

Use this compact structure, adapting depth to risk:

```markdown
# <Feature>: research and decisions

## Problem and contract
User outcome, confirmed requirements, assumptions, constraints, non-goals,
affected consumers, and compatibility/lifecycle/failure behavior.

## Research and experiments
Dated entries: question, source/link or exact experiment and source state,
observed result, and how it changed the decision. Mark untried options as such.

## Alternatives and decision
Existing/no-change approach, simpler alternative, chosen approach, tradeoffs,
rejected options and reasons, and evidence that would trigger reconsideration.

## Validation and demonstration
Source states, commands, exits, selected/executed identities and counts,
red/green or green/green evidence, assertions/artifact links, media/provenance,
and explicit unavailable or inapplicable checks.

## Open questions
Unresolved assumptions, risks, owners, and rollout/servicing decisions as needed.
```

## 2. Challenge the design before implementing it

Do not treat the first proposed implementation, an AI answer, or missing rationale
as a specification. Search for repository prior art and authoritative contracts.
For each material choice, compare the current/no-change design, the simplest
viable alternative, and the proposed approach. Explain concrete tradeoffs, not
generic claims that an architecture is "cleaner" or "more scalable".

Identify the consumer, ownership/lifetime, failure and cancellation behavior,
compatibility, observability, and cost of new abstractions or public APIs.
For cross-product/package/process changes, define the producer-consumer contract
and use [testfx-acceptance-validation](../testfx-acceptance-validation/SKILL.md).
Apply [testfx-parallel-safety-preflight](../testfx-parallel-safety-preflight/SKILL.md)
before changing tests or the lifecycle/shared-state paths they exercise.

When no design information was provided, explicitly record assumptions and test
them against callers, supported targets, and failure paths. Resolve consequential
unknowns before committing to an incompatible or irreversible design; seek
clarification when required. In unattended work, proceed only with a safe,
reversible assumption and label unresolved decisions rather than claiming them
confirmed. Do not require a separate agent or multi-model review for routine work.

## 3. Prove the tests are sensitive to the change

Establish the focused existing-test baseline before implementation when possible.
For a regression fix or new feature's behavioral tests, run the **same new/changed
tests with the same assertions and selection** in two production states:

| State | Required observation |
| --- | --- |
| Without the relevant production change, with the tests retained | Tests execute the targeted scenario and fail at the intended behavioral assertion. |
| With the production change | The same tests execute and pass; relevant existing tests remain green. |

Use an isolated, task-owned comparison worktree/output area, never the main
checkout or someone else's worktree. Record the exact base/head commits and any
test/support-code patch retained in the comparison. Do not remove the tests with
the production change, weaken assertions between runs, alter shared caches, or
destructively revert user work. Clean up only owned comparison resources.

Rebuild each state with the repository-pinned toolchain, capture binlogs for
MSBuild commands, and keep outputs/results separate. For package-consuming tests,
repack each state and verify the consumer's resolved/loaded bits; identical
package versions and `--no-build` against stale outputs are not evidence.
Use the smallest relevant selector, and reconcile discovered/selected identities
with **executed** results in both states. A nonzero command exit alone is not a
failing assertion. Compilation, restore, launch, timeout/infrastructure failures,
missing tests, zero tests, and skips do not establish regression sensitivity.

If a new API cannot compile against the old production state, a compiler error
is not behavioral red-phase proof. Prefer a comparison retaining the API/test
harness while removing the feature behavior, or a deliberate compiling mutation
that violates the asserted contract. Record exactly what was removed/mutated;
label this as a behavior-removal/mutation check, not a pristine-baseline run.
If no safe executable comparison is feasible, explain why and report
**regression sensitivity unproven**, not an invented red result.

For behavior-preserving refactors or test-only safety-net additions protecting
already-correct behavior, focused tests should pass before and after. Record
green/green equivalence; use a deliberate relevant mutation to demonstrate any
new safety net's sensitivity, clearly labeled. Documentation-only changes do
not require product tests or fabricated red/green runs.

For each run preserve source state and retained patches, working directory,
configuration/TFM/SDK/packages, exact command/filter, process exit, test identities
and planned/selected/executed/pass/fail/skip counts, intended assertion failure,
and logs/results. Identify any outer acceptance host versus child product exits.
Link retained evidence from the decision record and PR; a local absolute path
alone is not accessible reviewer evidence. Publish evidence only when authorized,
using approved artifact storage or concise text excerpts if logs cannot be shared.

## 4. Demonstrate the feature and prepare the PR

Whenever practical, show new or changed user-visible behavior in screenshots or
a short GIF/video, including terminal/CLI output, analyzer UX, and reports.
Capture the actual changed product, preferably before/after, with enough context
to identify the behavior. A diagram can explain a design but is not proof the
feature ran. Do not manufacture or stage output as a successful demonstration.

Provide a caption, reproduction command/steps, and source/package provenance.
Pair visual output with a readable text explanation/transcript for accessibility.
If text or an artifact communicates the result better, use it and explain the
choice. For internal, documentation-only, inaccessible, or nonvisual changes,
state why media is inapplicable/unavailable and provide the relevant evidence.
Redact sensitive data; avoid unrelated windows, credentials, and private paths.
Media supplements executable assertions and composed-product validation.

Use a descriptive, user-facing PR title; category prefixes are not required.
See the [PR-title research](../../../docs/decisions/feature-delivery-guidance.md#pr-title-research).
Preserve prefixes required by existing automation. Follow the
[PR template](../../PULL_REQUEST_TEMPLATE.md), linking the decision record,
demonstration, and regression evidence, and state remaining gaps.
For authorized media publication, use `github-pr-media` when available.
Preserve existing body content and verify published text and attachment links
by reading them back. Do not upload, open a PR, or post a review just because
this skill asks for evidence.
