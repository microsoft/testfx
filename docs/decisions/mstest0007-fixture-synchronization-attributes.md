# MSTEST0007 on fixture synchronization attributes: research and decisions

## Problem and contract

`[ResourceLock]` and `[DoNotParallelize]` are silently ignored when applied to fixture methods
(`[AssemblyInitialize]`, `[AssemblyCleanup]`, `[ClassInitialize]`, `[ClassCleanup]`,
`[TestInitialize]`, `[TestCleanup]`, `[GlobalTestInitialize]`, `[GlobalTestCleanup]`). Users may
expect them to serialize or lock the fixture, but discovery never reads them there.

Confirmed requirement (user): extend MSTEST0007 to flag these attributes on fixture methods, and
avoid automatic moves that would change fixture-lock lifetime.

Non-goals: flagging these attributes on arbitrary non-test methods, adding a new diagnostic ID,
changing runtime scheduling.

## Research and experiments

- Discovery (`src/MSTest/MSTestAdapter.PlatformServices/Discovery/TypeEnumerator.cs`, around lines
  202-210) reads `DoNotParallelize` and `ResourceLock` only from the test method and its declaring
  class (and assembly). It does not read them from fixture methods.
- Both attributes are `Inherited = true` and are read with `inherit: true`. An attribute on a
  virtual helper or base method can therefore apply to an overriding `[TestMethod]`. For this
  reason, flagging every non-test method would produce false positives, so the rule is limited to
  fixture methods.
- The existing MSTEST0007 code fix adds `[TestMethod]`, which is wrong for a fixture. Moving the
  attribute to the class would change lock lifetime: a class-level `[ResourceLock]` covers every
  test in the class, and `[DoNotParallelize]` on the class serializes all tests. No automatic fix is
  offered.

## Alternatives and decision

| Option | Outcome |
| --- | --- |
| No change | The silent no-op remains. Rejected. |
| Flag on all non-test methods | False positives through the inheritance path. Rejected. |
| Code fix that moves the attribute to the class | Changes semantics and lock lifetime, against the explicit requirement. Rejected. |
| New diagnostic ID | Extra rule surface for the same "attribute only meaningful on test methods" concept. Rejected in favor of extending MSTEST0007. |
| **Chosen:** two new MSTEST0007 descriptors that fire only on fixture methods, with a dedicated message and a `NoAddTestMethodFix` diagnostic property that suppresses the add-`[TestMethod]` fix | Accurate, and keeps the user in control of where the lock belongs. |

Existing MSTEST0007 behavior for other attributes, including fixtures still being offered the
add-`[TestMethod]` fix for Owner and similar attributes, is unchanged. No new ID means that
`AnalyzerReleases.Unshipped.md` does not need to change.

Reconsider this decision if discovery starts honoring these attributes on fixtures.

## Validation and demonstration

Unit tests in `test/UnitTests/MSTest.Analyzers.UnitTests/UseAttributeOnTestMethodAnalyzerTests.cs`:

- `WhenFixtureMethodIsMarkedWithSchedulingAttributes_DiagnosticWithoutCodeFixAsync` covers all 8
  fixture kinds with both attributes. It also confirms that no code fix is offered: the fixed code
  equals the source.
- `WhenFixtureMethodIsMarkedWithResourceLock_MessageExplainsFixtureHasNoEffectAsync` checks the
  message text.
- `WhenSchedulingAttributesAreOnTestMethodsTestClassOrHelperMethods_NoDiagnosticAsync` checks that
  test methods, the class, and helper methods are not flagged.

Red/green: a behavior-removal mutation in the task-owned worktree, run on net10.0 with the
repository-pinned SDK. The tests were kept unchanged in both runs.

- Without fixture reporting (`context.ReportDiagnostic(...)` for the fixture rules replaced by a
  no-op), `dotnet run --project test\UnitTests\MSTest.Analyzers.UnitTests -f net10.0 --no-build --
  --filter "FullyQualifiedName~UseAttributeOnTestMethodAnalyzerTests"` exited with code 2: 49
  total, 9 failed, 40 passed. All 9 failures were the new diagnostic tests, each with
  `Mismatch between number of diagnostics returned, expected "2"/"1" actual "0"`.
- With the change, the same command exited with code 0: 49 total, 49 passed.
- Full `MSTest.Analyzers.UnitTests` on net10.0: 3080 total, 3079 passed, 1 skipped (pre-existing),
  0 failed.

There is no rendered surface beyond the standard analyzer diagnostic. The IDE presentation was not
inspected.

## Open questions

- Whether a future opt-in fix should offer "move to class" with an explicit lifetime warning.
