---
name: testfx-parallel-safety-preflight
description: Check concurrency and isolation before implementing or refactoring TestFx tests, lifecycle code, helpers, shared-state declarations, or production paths those tests exercise. Use before narrowing DoNotParallelize or ResourceLock, changing parallelization settings, or introducing asynchronous workers; repeat against the final diff.
---

# TestFx parallel-safety preflight

Run this during implementation, not only during review. Keep it bounded to the
requested change and its reachable conflicts; do not turn it into a repository
audit or speculative hardening.

## Reuse the canonical analysis

Read `.github/copilot-instructions.md`, the owning project's configuration and
`BannedSymbols.txt` when present, and
`.github/workflows/shared/parallel-safety-audit-shared.md`.
Apply the shared rubric from `# Parallel-safety audit` through Step 3, including
its assembly-scoping rules. It owns scheduling semantics, the A-D taxonomy,
severity, confidence, culture/ExecutionContext distinctions, and declaration
reconciliation; do not duplicate or replace it.

Read `.github/agents/parallel-safety-reviewer.agent.md` for the specialist's
evidence and partial-analysis contract. No specialist invocation or GitHub
publication is required. Ignore review-only read-only instructions, event
expressions, extraction-tool assumptions, output formatting, and safe-output
calls: use the actual implementation scope and local diff instead. For existing
branch changes, compare against their merge base, not a moving base-branch tip.

## Before editing: establish the isolation contract

1. Resolve the effective test engine, target frameworks, assembly scheduling
   scope, workers, and configuration sources using Step 0. Include constructors,
   initialization, cleanup, disposal, fixtures, linked sources, and production
   helpers reached by the affected tests. Do not assume that every project uses
   MSTest or that an attribute is the effective runtime configuration.
2. Record the existing resource coverage matrix in working notes, not a new
   repository planning file:

   | Resource and boundary | Lifetime | Direct test readers/writers | Production readers/writers | Other tests reaching those paths | Declaration: key, mode, placement |
   | --- | --- | --- | --- | --- | --- |
   | Exact state, path, source, or registry; assembly/process/external | Per-test, class, assembly, or worker | Named tests and lifecycle members | Concrete helpers and call paths | Named observers or mutators in the owning assembly | Existing coverage or a confirmed gap |

   Trace through production helpers and back to every reachable caller in the
   owning test assembly. A literal-key search is an inventory aid, not proof.
   Before claiming a race, name a concrete conflicting observer, the scheduling
   interleaving, and the observable wrong result. If no conflict is established,
   do not add a lock merely because a mutation exists; mark uncertainty or
   readiness-only concerns as such.
3. Choose elimination or the narrowest correct shared read/write declaration
   using categories C and D. Reuse the existing key and shared constants; include
   indirect readers and compatible producers, not only direct mutations.
   Justify method versus class placement against the whole lifecycle. Preserve
   assembly-level safety; a narrower method/class lock must not silently opt an
   assembly in or replace assembly-lifetime exclusion.

For a requested scheduling change, expand to the whole affected assembly as the
shared rubric requires. Otherwise leave assembly scheduling unchanged. If
coverage is incomplete, retain the existing exclusion and report the gap rather
than serializing unrelated tests or weakening safety to finish a patch.

## While implementing: close the assertion and lifetime boundaries

- **Global observers:** trace activity, metric, logging, and event listeners back
  to every producer they can observe. A lock around a captured list only makes
  the collection thread-safe; it does not prove an event belongs to this test.
  Prefer a per-test identity filter and assert the intended producer. If an
  unfiltered observer is necessary, reconcile all interfering producers with
  the same coordination key. Compatible filtered tests/producers can share
  `Mode = ResourceAccessMode.Read` when the established protocol permits it;
  use exclusive access only where required. Do not turn an entire class into
  `ReadWrite` merely because one method needs exclusion.
- **Inherited state:** enumerate the environment values the exercised production
  paths actually consult, including inherited CI gates such as `TF_BUILD`.
  Explicitly arrange required present and absent values using existing fakes or
  child-process environment overrides where possible. A lock does not neutralize
  CI inheritance. When real process state must change, capture exact previous
  values (including absence), restore them in `finally` inside the protected
  lifetime, and reconcile readers. Restore changed culture and static state too;
  use the rubric's target-framework/ExecutionContext distinctions rather than
  treating all current-culture changes as process-global races.
- **Rendezvous and cleanup:** use deterministic started/ready/release signals,
  not sleeps or elapsed-time guesses. Keep handles to the original worker tasks,
  not only cancellation/timeout wrappers. In `finally`, release blocked workers
  and request cancellation as appropriate, then await their termination before
  disposing gates/listeners, restoring shared state, or releasing test ownership.
  A release signal, cancellation request, or "reached finally" marker is not
  completion. Bound rendezvous and cleanup separately with named limits and
  target-framework-compatible existing helpers; cleanup must not immediately
  abandon the join because the test's cancellation token is already canceled.
  Surface worker faults and cleanup timeouts without hiding the original test
  failure. If in-process workers cannot terminate within the bound, use an
  existing isolated-process harness or keep the attempted refactor out; do not
  leave background mutation running and declare the test isolated.

## Before finishing: reconcile the final diff

Rebuild the matrix from the final implementation, including removals of locks,
opt-outs, restoration, or cleanup. Verify that each named conflicting observer
is covered and that compatible readers remain concurrent. Missing evidence is
partial, never clean. Do not remove an exclusion when any row is unknown.

Use the smallest existing build and focused tests for the affected behavior,
following the repository-pinned toolchain and packing rules. Include the named
conflicting tests and the relevant normal, assertion-failure, cancellation, and
timeout cleanup paths, not just the changed method. Preserve the actual assembly
scheduling in validation; a serial filtered run or passing stress run is not
proof that a race is impossible. When environment-dependent behavior is in scope,
cover the required present/absent CI values without changing unrelated host state.

Keep a concise handoff: assembly scope/workers, concrete conflict and declaration
choice (or why none is needed), rendezvous/termination evidence, exact validation
commands and results, and unresolved evidence. This skill does not authorize
publication, unrelated fixes, or changing assembly scheduling.

## Repository-backed examples

These are analysis examples, not requests to patch the referenced tests. Recheck
their implementation at HEAD before reusing a pattern.

| Scenario | Preflight decision and reference |
| --- | --- |
| A test writes `ProcessKilledByHangDump`; another calls the crash handler without mentioning that slot. | Trace `CrashDumpProcessLifetimeHandler.OnTestHostProcessExitedAsync` to `AppDomain.CurrentDomain.GetData`. For example, the writer in `CrashDumpTests` could make `CrashDumpArtifactPublisherMutationTests.OnTestHostProcessExitedAsync_MissingDumpPattern_ThrowsInvariantViolation` skip its expected exception if exclusion were removed. Preserve the matching readers, writer's exclusive declaration, and exact restoration. |
| A raw activity listener could accept another test's builder event. | In `OpenTelemetryProviderExtensionsTests`, `AddTestingPlatformDiagnostics_RawListenerObservesBuilderActivityWithoutProvider` needs exclusion from the producer `DiagnosticsAndProviderRegistration_IsIdempotentAndOrdered`, not just a lock on its captured list. They use matching exclusive/read source-key declarations. `OpenTelemetryPlatformServiceTests` also uses a unique activity prefix and a compatible class-level read declaration; do not serialize all readers. |
| A test expects no CI resource attributes while running on an Azure Pipelines agent. | Inspect the environment read set, not the developer machine. `OpenTelemetryProviderExtensionsTests.WithEnvironment` / `WithEnvironmentAsync` explicitly clear observed values including `TF_BUILD`, then restore the snapshot. Prefer the existing fake when testing configuration alone. |
| A test's timeout/cancellation wrapper completes while its worker is still active. | `src/Platform/Microsoft.Testing.Platform/Helpers/TaskExtensions.cs` separates cancellation of the wait from completion of the original task. Retain and join that original worker after releasing its rendezvous, with bounded failure cleanup; observing the wrapper or a pre-exit marker is insufficient. |
