---
name: behavior-preserving-refactor
description: Preserve observable behavior when splitting files, moving members, consolidating helpers, or simplifying TestFx code. Use for structural refactors and file-diet implementation; inventory baseline semantics and consumers, then run focused equivalence checks.
---

# Behavior-Preserving Refactors

Use this recipe for the specific code being refactored, not a repository-wide
audit. Read `.github/copilot-instructions.md` and applicable directory rules.
Preserve existing behavior, including surprising reachable cases; a bug fix or
modernization needs separate authorization and validation.

An issue-generation workflow's bounded inspection is not implementation
preflight. For example, Daily File Diet inspects only the first 100 lines and up
to 50 declaration lines of one file. Keep that budget when proposing work, label
unverified assumptions, and include this recipe for the later implementer.
Do not claim equivalence, tested behavior, or complete consumer coverage from
that sample.

## 1. Establish the baseline before editing

Identify the exact methods or declarations to move or extract, their callers,
existing focused tests, and owning projects. Read the complete affected bodies
and relevant context, not just signatures or similar-looking helpers. For each
existing implementation that might share a helper, record:

| Boundary | What must remain unchanged |
| --- | --- |
| Selection | Iteration order, first versus last match, first matching name versus first matching typed value, exact type tests, casts, defaults, and missing-value handling. |
| Evaluation | Short-circuit operand order, which callbacks run and how often, enumeration count, laziness, mutation, and side effects before return or failure. |
| Results and failures | Return values, diagnostics and locations, output ordering, exception type and timing, cancellation, and cleanup/disposal order where affected. |
| Compilation | Namespaces, accessibility, attributes, overload binding, target-framework guards, and native interop signatures and marshalling. |
| Cost | Allocations, materialization, boxing, repeated scans, and algorithmic complexity on affected hot paths. |

Inventory reachable empty, duplicate, malformed, null, and wrong-type inputs.
Do not add support for impossible states, but do not assume compiler or user
input is always valid: analyzer inputs can be incomplete or contain errors.
Name the existing guard that makes a case unreachable if excluding it.
Use existing behavior as the oracle, not the intended behavior of a new helper.

## 2. Trace the actual consumers and constraints

- Find all partial declarations and call sites of the affected type or member.
  Check initialization order if moving fields between partial files.
- Inspect owning project files and imports for linked `Compile` items, shared
  source, globs, explicit includes, and packaging rules. Find every consuming
  project, including source-only packages; a build of the original owner does
  not prove that a new file is included by another consumer.
- Keep target frameworks, conditional compilation, supported platforms, and
  package layout intact. A native interop file move is not permission to replace
  `DllImport`, change marshalling, or adopt APIs unavailable on older targets.
- Check applicable `PublicAPI` and `InternalAPI` shipped/unshipped baselines.
  Prefer unchanged signatures and accessibility; do not expose a helper just
  to share it. Record required newly tracked declarations without rewriting
  shipped API history.

## 3. Make the structural change, not adjacent cleanup

Move existing bodies unchanged first, preserving attributes, guards, resource
access, and file encoding. Keep unrelated renaming, formatting, LINQ rewrites,
interop modernization, new validation, and behavior fixes out of the change.

Before consolidating helpers, compare every caller's baseline. Similar names
or happy-path outputs do not establish equivalence. If callers differ in
selection, evaluation, or failure behavior, keep separate implementations or
use the smallest explicit helper that preserves each contract; do not invent
a configurable abstraction merely to eliminate duplication.

Do not replace a loop with a dictionary, eager projection, sort, or single-match
operator without proving duplicate handling, enumeration, and failure behavior.
On hot paths, preserve allocation and complexity characteristics. Use existing
allocation/performance checks when the extraction could change them; do not
introduce a benchmark project for a pure move.

If the size target requires behavior changes or unsafe fragmentation, stop that
part of the refactor and report the remaining line count and concrete constraint.
Do not alter semantics just to meet a file-size goal.

## 4. Use distinguishing equivalence cases

These are examples to adapt to the actual baseline, not new product semantics:

| Baseline | Focused case | Required observation |
| --- | --- | --- |
| A loop overwrites its result for every matching name. | Matching values `1`, then `2`. | Result remains `2`; returning the first match is not equivalent. |
| A loop returns the first matching name whose value is an `int`. | Same-name string `"bad"`, integer `7`, then integer `9`. | Result remains `7`; taking the first name then casting, or taking the last integer, is not equivalent. |
| Exact `int` type matching. | Same-name `long` value `7L`, followed by `int` value `8`. | Result remains `8`; numeric conversion changes type selection. |
| `ShouldRun() && Execute()`. | `ShouldRun` returns false; `Execute` records a call or throws. | Only `ShouldRun` runs; reordering operands or evaluating both eagerly is not equivalent. |
| A guarded interop declaration moves to another file. | Build every source-linked consumer and affected supported TFM/platform path. | Declaration, guards, accessibility, and marshalling remain unchanged; no new runtime API requirement. |

Also cover the actual missing/empty and malformed-only outcomes, not just
malformed input followed by a valid value. Assert observable values, diagnostic
locations, exception behavior, and ordered side-effect traces where applicable.
Pin expectations to the baseline; comparing two paths that both call the new
helper is not an equivalence test.

## 5. Validate the smallest relevant surface

Before editing, run the existing focused checks when feasible and add any
necessary distinguishing cases against the original implementation. After the
change, rerun the same checks. Explain pre-existing failures or unavailable
checks; do not report unrun checks as passed.

Use the repo-local toolchain and documented filtered build/test commands from
`.github/copilot-instructions.md`. Build the owning and linked consuming
projects for affected target frameworks. Run directly related tests, escalating
only when dependencies or failures justify it. A pure file move needs inclusion
and compilation checks plus relevant existing tests, not the full solution.
If shipping package/source inclusion changes, validate the packed consumer
layout using existing acceptance infrastructure.

Review the final diff for changed expressions, ordering, signatures, attributes,
guards, includes, tracked APIs, and unintended adjacent edits. Measure resulting
file sizes rather than assuming the split meets its target.

## Completion evidence

Report the moved/extracted responsibility, baseline invariants and distinguishing
cases, affected consumers/TFMs and API surfaces, exact checks and outcomes, and
resulting line counts when size is the goal. Identify any remaining uncertainty
or blocked target explicitly. Compilation alone does not prove runtime
equivalence, and passing happy-path tests does not settle first/last selection
or short-circuit behavior.
