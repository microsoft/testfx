# Legacy assertion removal: research and decisions

## Problem and contract

[Tracking issue #12354](https://github.com/microsoft/testfx/issues/12354) covers
removing `StringAssert` and `CollectionAssert` in MSTest 5. All assertion
operations remain available through `Assert`; this is not a request to add a
second set of subset or collection-equivalence APIs.

Removing the types, their methods, and their `.That` extension hooks intentionally
breaks source and binary compatibility. Consumers must migrate and recompile,
including custom assertion extensions. Keep shipped API history and record
removals in the unshipped baselines. Do not backport the removal to MSTest 4.x.
Older framework consumers must still be able to use the migration analyzers.

### Complete collection migration

`message` arguments remain supported by every replacement.

| `CollectionAssert` API | Replacement |
| --- | --- |
| `AreEqual(expected, actual)` | `Assert.AreSequenceEqual(expected, actual)` |
| `AreNotEqual(notExpected, actual)` | `Assert.AreNotSequenceEqual(notExpected, actual)` |
| `AreEqual(expected, actual, comparer)` | `Assert.AreSequenceEqual(expected, actual, equalityComparer)`; adapt `IComparer` as described below |
| `AreNotEqual(notExpected, actual, comparer)` | `Assert.AreNotSequenceEqual(notExpected, actual, equalityComparer)`; adapt `IComparer` as described below |
| `AreEquivalent(expected, actual)` | `Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder)` |
| `AreNotEquivalent(notExpected, actual)` | `Assert.AreNotSequenceEqual(notExpected, actual, SequenceOrder.InAnyOrder)` |
| `AreEquivalent<T>(expected, actual, comparer)` | `Assert.AreSequenceEqual(expected, actual, comparer, SequenceOrder.InAnyOrder)` |
| `AreNotEquivalent<T>(notExpected, actual, comparer)` | `Assert.AreNotSequenceEqual(notExpected, actual, comparer, SequenceOrder.InAnyOrder)` |
| `Contains(collection, element)` | `Assert.Contains(element, collection)` |
| `DoesNotContain(collection, element)` | `Assert.DoesNotContain(element, collection)` |
| `AllItemsAreNotNull(collection)` | `Assert.AreAllNotNull(collection)` |
| `AllItemsAreUnique(collection)` | `Assert.AreAllDistinct(collection)` |
| `AllItemsAreInstancesOfType(collection, typeof(T))` | `Assert.ContainsOnly(item => item is null \|\| item is T, collection)` |
| `AllItemsAreInstancesOfType(collection, expectedType)` | `Assert.AreAllOfType(expectedType, collection?.Cast<object?>().Where(item => item is not null))` |
| `IsSubsetOf(subset, superset)` | `Assert.ContainsAll((IEnumerable)subset, superset)` |
| `IsNotSubsetOf(subset, superset)` | `Assert.DoesNotContainAll((IEnumerable)subset, superset)` |
| `That` | Change the extension receiver to `Assert` and invoke `Assert.That` |
| Obsolete `Equals` / `ReferenceEquals` traps | Use `Assert.AreEqual` / `Assert.AreSame`; these traps were not working equality assertions |

Subset assertions preserve multiplicity: `[1, 1]` is not a subset of `[1]`.
An empty subset is contained in every non-null collection. The negative
assertion means **not all** elements are present, not that no elements overlap.
Non-generic collections remain supported without converting them to arrays.
For typed collections, select the non-generic overload to retain legacy object
equality instead of switching to `IEquatable<T>` equality. The fixer remaps named
arguments in their source evaluation order and names the message argument, so
side effects and null/default messages keep their original meaning.

The legacy type assertion ignored null elements. `Assert.AreAllOfType` rejects
them, so an unfiltered rename is **not equivalent** and must not be offered as
an automatic migration. The null-tolerant predicate above preserves the
`typeof(T)` behavior. For a runtime `Type`, the null-filtered sequence preserves
null elements' treatment while `AreAllOfType` still validates the type, including
for an empty collection. The conditional access passes a missing collection
through to the assertion instead of letting LINQ throw first; add `using
System.Linq`. Use unfiltered `AreAllOfType` only when intentionally adopting its
stricter null policy.

`ContainsOnly` is available in MSTest 5. When preparing a 4.x project before
upgrading, use the filtered `AreAllOfType` form with either a runtime type or
`typeof(T)` instead.

Ordered equality overloads previously accepting `IComparer` require an
`IEqualityComparer` or `IEqualityComparer<T>` instead. A comparer such as
`StringComparer.OrdinalIgnoreCase` already implements both contracts. For a
custom ordering comparer, implement equality as `Compare(x, y) == 0`, with
consistent hash codes if using unordered comparison. Do not automatically cast
an arbitrary `IComparer` to `IEqualityComparer`.

The legacy comparer overloads reject a null comparer; modern sequence assertions
interpret it as the default comparer. Preserve an explicit non-null precondition
when migrating that contract. Comparer-bearing automatic migrations remain
unsupported rather than silently accepting previously invalid input.

Legacy ordered equality also traversed nested enumerables. `AreSequenceEqual`
uses the supplied element equality instead: a mechanical rename is not
equivalent for independently allocated nested lists. For deep object-graph
comparison, use `Assert.AreEquivalent` / `Assert.AreNotEquivalent` after checking
their documented structural-comparison rules. To retain custom or Equals-first
nested semantics, supply a matching element equality comparer. For nested arrays,
`StructuralComparisons.StructuralEqualityComparer` is an available non-generic
option. This distinction must not be hidden by an automatic migration.

`Assert.AreEquivalent` is **not** the replacement for collection multiset
equivalence: its enumerable comparison is order-sensitive and recursive.
Failure diagnostics intentionally use the replacement assertion's structured
messages rather than retaining the removed class names.

### Complete string migration

| `StringAssert` API | Replacement |
| --- | --- |
| `Contains(value, substring)` | `Assert.Contains(substring, value)` |
| `StartsWith(value, substring)` | `Assert.StartsWith(substring, value)` |
| `EndsWith(value, substring)` | `Assert.EndsWith(substring, value)` |
| `Matches(value, pattern)` | `Assert.MatchesRegex(pattern, value)` |
| `DoesNotMatch(value, pattern)` | `Assert.DoesNotMatchRegex(pattern, value)` |
| `That` | Change the extension receiver to `Assert` and invoke `Assert.That` |
| Obsolete `Equals` / `ReferenceEquals` traps | Use `Assert.AreEqual` / `Assert.AreSame` |

Keep explicit `StringComparison`, `Regex` options/timeouts, and user messages.
The default substring, prefix, and suffix comparisons remain ordinal.

## Research and experiments

- 2026-11-11: Inspected all legacy partials, the base and conditional public API
  baselines, and the replacement implementations. All eleven collection assertion
  families already exist on `Assert`, including non-generic and comparer paths.
  `ContainsAll` / `DoesNotContainAll` already implement the multiplicity-sensitive
  subset contract; adding `IsSubsetOf` aliases would duplicate functionality.
- Confirmed `eng/Versions.props` targets MSTest 5.0.0 on `main`. Existing major
  removals use `*REMOVED*` entries without rewriting shipped baselines.
- Found that analyzer fixtures normally reference the executing source framework.
  Legacy-source fixtures therefore need an explicit older contract after removal;
  retaining the migration analyzers is preferable to deleting useful diagnostics.
- Baseline framework test build succeeded for net48, net10.0, net11.0, and the
  WinUI test target. The modern `AssertTests` selection executed 1,239 cases on
  net10.0: all passed, none skipped.
- The environment's child command processor could not resolve Windows PowerShell.
  Setting its `PATH` inside the build's own `cmd.exe` process allowed the normal
  repository bootstrap to install and use the pinned SDK. No global setting or
  repository build script was changed.
- A full-solution build was blocked by 120 existing MSTEST0037 diagnostics in
  unrelated platform tests, before the framework removal. A framework-only pack
  also failed the repository-wide package inventory check. Use the existing
  `MSTest.slnf` filter: its baseline build, pack, and package validation succeeded.
  Recursive PowerShell project globs and a semicolon-separated project argument
  did not select the intended projects reliably, so they were abandoned.
- Acceptance fixture initialization additionally requires the platform packages.
  Packing the already-built full layout produced these prerequisites, but its
  `NoBuild` attempt encountered the packaged-app controller's explicit build
  target. This is not a successful full-solution validation or red-phase proof.
- With the complete fixture prerequisites present, both C# 12 consumers
  (`net10.0` and `net11.0`) executed all replacement assertions successfully,
  then failed specifically at the reflection assertion that `StringAssert`
  must be absent. Outer test exit: 2; executed: 2; failed: 2; skipped: 0.
  Both failures include `All replacement assertions passed.` and
  `Expected value to be null.` with the exported `StringAssert` type.
  The earlier fixture-initialization failure is excluded from this evidence.
- A later static parity check found the legacy type assertion's intentional
  null tolerance in `CollectionAssert.Type.cs` and
  `AllItemsAreInstancesOfType_AssignableImplementationsAndNulls_AreAccepted`.
  The modern strict-null contract stays unchanged. Documented existing
  predicate/filter equivalents, added packed-consumer cases for them, and
  stopped offering the lossy automatic type-assertion rewrite.
- Generic comparer auto-migration was explored but rejected: the old assertion
  rejects a null comparer even when both sequences are null, while the modern
  sequence assertion uses the default comparer. Retained the original refusal
  to auto-migrate comparer overloads rather than adding nullable-flow machinery
  or changing the modern public API. The package consumer covers an ordering-only
  comparer's adapter and an explicit null-comparer precondition.

## Alternatives and decision

Keeping obsolete forwarding types would preserve binary compatibility, but would
not implement the requested removal. Moving the old methods wholesale to `Assert`
would duplicate existing APIs and risk overload ambiguity. Remove the types in
the major release, retain their shipped history, migrate callers, and document
the existing replacements and the cases requiring manual comparer migration.

All 54 public and five internal removed declarations have matching explicit
removal entries. The shipped public and internal baselines are unchanged.
No new `Assert` public overloads are introduced.

Keep historical stack-frame recognition in independently shipped report
extensions: those extensions can still receive failures from older frameworks.
Likewise, analyzer fixtures representing older frameworks are not production
dependencies on the removed types.

## Validation and demonstration

| Requirement / risk | Automated evidence | Surface |
| --- | --- | --- |
| Every collection operation remains usable | Existing `AssertTests` plus `LegacyAssertionReplacements_ExecuteFromPackedFrameworkWithCSharp12` using non-generic collections, duplicates, nulls, empty inputs, comparers, and negative assertions | Nonvisual API contract |
| String and custom-extension migrations work | The same packed consumer exercises all five string families and `Assert.That` | Nonvisual API contract |
| Removed types do not ship | The consumer checks both type names through reflection, so keeping a legacy type produces an executed assertion failure, not a compilation failure | Package/runtime contract |
| Existing source call shapes remain valid | `PublicAssertCallShapes_CompileWithCSharp12` | C# 12 compilation against packed assets |
| Older consumers can still migrate | Existing and focused migration-analyzer tests using legacy references | Compiler diagnostics and fixed source |

The producer is the current checkout's packed `MSTest.TestFramework`; consumers
are generated C# 12 programs on .NET 10 and .NET 11 with isolated package caches.
The acceptance test compares selected compile/runtime assets and payload hashes
with the current shipping package, asserts child exit zero, and verifies the
loaded assembly location and completion marker. Each generated asset owns its
directory; no shared mutable test asset or scheduling change is introduced.
Launch/handshake, cancellation, reporting layouts, and multi-module aggregation
are not changed by this synchronous assertion API removal.

The before-removal framework source is commit
`ecee016ececdffa3d8b8fbdc835055e3cc6cbe21`; the new consumer tests and analyzer
migration changes were retained. The baseline framework package SHA-256 is
`4BA3D9E5EE65FE8C034EA84A24AFC83472CC9236F173027A9DBED48A2DAB5AFF`.
Its loaded `net10.0` DLL hash is
`B03B347A32FEA567BEADBD1A200D1A97A8E53535CB196BCAD85B09431BF803F5`;
the `net11.0` DLL hash is
`55363E0E81028B4DADDB79435724163BB684E68F50FEB015A4A479D55B653881`.
Each generated consumer asserted the package asset/cache/runtime-copy hashes
before execution. Both outer tests ran concurrently with the normal method-level
scheduling and separate owned asset directories.

### Final results

The final uncommitted source/test patch, relative to the base commit above, has
SHA-256 `1D13C1BC5AEA264F3184073F46A0F0703795A3F3AB4C26BD4E9C44A981E6CDA7`.
Its scope is `git diff --binary -- src test eng/bazel`; documentation is excluded
so recording the evidence does not change that identity.

| Selection | Executed result |
| --- | --- |
| `MSTest.slnf` build, pack, and package validation | Exit 0; zero warnings/errors; pinned SDK `12.0.100-alpha.1.26556.115`, Debug |
| Framework unit suite, net10.0 / net11.0 / net48 | 1,678 / 1,678 / 1,541 passed; no failures/skips |
| Windows-qualified `AssertTests` selection | 1,239 passed; no failures/skips |
| Full analyzer suite, net10.0 / net11.0 / net472 | 3,092 / 3,092 / 3,027 passed; one existing Unix-only skip on each Windows run |
| Collection/string migration selection within each analyzer run | All 34 planned cases passed on each target; no skips |
| `AssertSourceCompatibilityTests`, C# 12 consumers on net10.0 / net11.0 | All eight planned cases executed and passed; outer and successful child exits 0 |
| Native Bazel build-tools / SDK-package helper suites | 13 / 12 passed on the warm verification run; no skips |
| Physical framework package inspection | Both removed types absent from all five assets: net10.0, net11.0, net462, netstandard2.0, and uap10.0 |

The unchanged `AssertTests` identities were reconciled rather than inferred from
counts: 1,238 common cases passed before and after. The remaining case is the
explicitly migrated `Scope_AreSequenceEqual_ExpectedNull_CollectsFollowingFailure`.
The original scope case passed before removal; the replacement preserves
collecting the following failure and asserts its new structured diagnostic.

The existing skipped analyzer case is
`MissingOutputPathWithInheritedCrashDumpSettingsDoesNotProduceDump`; its
environment-driven crash-dump scenario is Unix-only. No new migration case was
skipped, no ignore was added, and scheduling was unchanged. The native cold run
had one existing 30-second child-process startup timeout in
`Xlf_RejectsInvalidCatalog`; the same unmodified suite passed on its warm rerun.
Neither infrastructure failure is counted as regression-sensitivity evidence.

### Test-value evidence

#### Review corrections (2026-11-11)

Fixed four demonstrated migration defects from review: typed subset operands
must retain object equality, named subset arguments must retain source evaluation
order, null/default messages must remain unambiguous, and conditional null
membership operands need parentheses inside the object cast. The subset fixer
now selects the non-generic overload and remaps named arguments without sorting
their evaluation order. No public assertion contract changed.

Added two typed-equality, two null-message, and two side-effecting-order data rows,
and extended all four existing null-membership rows with conditional expressions.
The focused net10.0 build passes, all 31 collection migration cases pass without
skips, and the complete analyzer suite passes 3,098 cases with the same existing
Unix-only skip. Markdown lint also passes. Logs, reports, and binlogs are retained
as `review-fixer-*` session artifacts.

The initial pre-fix run executed 31 cases: 19 passed and 12 failed at the intended
fixed-source assertions. Expected redundant-cast formatting was subsequently
refined for statically non-generic collections, so this is not claimed as an
identical-assertion 31-case red/green pair. The two typed-equality and four
conditional-membership cases retained the same assertions across the failed
pre-fix and passing final runs. The final message and evaluation-order cases
also pass, but their final formatting expectations did not receive another
isolated counterfactual run.

#### Base integration validation (2026-11-11)

Integrated `main` at `f94fa2bbaa1153a3525e4711f13342863c6ea4d0` into the
published removal branch. The Azure Pipelines check had been skipped because
of merge conflicts, not because a build or test had failed. Resolved the two
additive conflicts by retaining both changelog entries and the exact union of
the public API removal records, including the upstream `TestResult` removals.
No assertion implementation or migration behavior was changed by the resolution.

The incoming dependency updates, including Roslyn testing 1.1.4, were retained.
The same repository-pinned restore/build/pack command for `MSTest.slnf` passed
with zero errors and eight upstream dependency-audit warnings. Post-integration
net10.0 framework tests passed 1,678/1,678; the full net10.0 analyzer suite passed
3,092 with the same existing Unix-only skip; all eight packed C# 12 consumer
cases passed on net10.0/net11.0. All test processes exited 0. Reports and the
build binlog are retained as `merge-main-*` session artifacts.

The paired counterfactual results below remain bound to their original source
identities; they were not relabeled as having run on this base integration.

| Contract / case | Counterfactual | Without behavior | Correct implementation |
| --- | --- | --- | --- |
| `LegacyAssertionReplacements_ExecuteFromPackedFrameworkWithCSharp12`, both TFMs | Keep the final compiled consumer unchanged and substitute the baseline package's framework DLL in an isolated copied layout | Both invocations execute all replacement assertions, then fail because `StringAssert` remains exported; child exit `-532462766` | Restore the current packed DLL: both identical consumers pass, exit 0 |
| `WhenCollectionAssertIsSubsetOf_FixesToContainsAll` | Restore the two original analyzer/fixer files, retaining the final tests and references | Both rows fail: expected three diagnostics, actual zero | Both pass with exact operand/message preservation |
| `WhenCollectionAssertMembershipWithNullElement_CastsToObject` | Same isolated analyzer comparison | Four rows detect missing object casts in fixed source | All four pass, including enabled/disabled nullable contexts |
| Type migration refusal and `WhenCollectionAssertAllItemsAreInstancesOfType_StaleDiagnostic_NoCodeFix` | Same isolated analyzer comparison | Two cases detect unsafe diagnostics; two detect an unsafe offered stale fix | All four pass with no lossy diagnostic/fix |
| Existing comparer refusal and remaining collection scenarios | Same comparison; no comparer enhancement ships | Remaining 15 cases pass | Same 15 pass |

The final analyzer comparison planned, selected, and executed the same 25 cases:
red exit 2, 15 passed / 10 intended assertion failures / zero skips; green exit 0,
25 passed / zero failures/skips. Both fresh builds succeeded with zero warnings
or errors. Current source/test hashes were matched to the isolated proof. The
shipping-only analyzer patch SHA-256 is
`46B00137DAA5ECDF5867B55182478135810441964873FF90446C1897DB3497DA`.

The final consumer comparison is explicitly a **packed binary-payload
counterfactual**, not a new pristine-baseline build. Its new null-tolerant and
comparer-precondition assertions are retained unchanged in both states. The
consumer source SHA-256 is
`84A47AF575694B7433FDED8000EDE43D1615AC901CB0989C2B6A7A2EAB9841ED`.
The same net10 consumer DLL
`D2B4A652745464022A578A78D1E73DDDBDEB5FD23BB0CC599FF923D7BF79240A`
and net11 consumer DLL
`9315730B39B38C94E74E04F9EC35EDB384DCAE99089EC83F3A26508FA2105171`
ran in both states. Each restored head DLL's hash and loaded path were checked.
The final framework package SHA-256 is
`02674D48DBAC1E2B270BE0FF55531C3458EBAFF08DA9B1D39203CC9FCE2D65F7`;
its net10 / net11 payload hashes are
`1FC552CFA568E5F5C76862C80D9C453A2C174FA5C61553084915FAFC5C6DB41F` /
`EE70EC608A641935C70B1720651B1E46765F06034BD3C18B99E90A91CFA6F3E1`.

### Reproduction and retained evidence

Commands below run from the worktree root; task-local log/result destinations
are omitted. The full command records, build binlogs, reports, source patches,
payload-comparison script, and before/after output are retained in the session's
artifact storage. No CI or public artifact publication is claimed.

```powershell
& $env:ComSpec /d /c 'set "PATH=C:\Windows\System32\WindowsPowerShell\v1.0;C:\Windows\System32;%PATH%" && powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng\build.ps1 -restore -build -pack -projects MSTest.slnf -bl'

.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Debug --no-build --filter "FullyQualifiedName~AssertSourceCompatibilityTests" -p:UsingDotNetTest=true -p:EnableCodeCoverage=false

# Repeat for net11.0 and net472.
.\.dotnet\dotnet.exe test --project test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -c Debug -f net10.0 --no-build -p:UsingDotNetTest=true -p:EnableCodeCoverage=false

# Direct internal framework hosts: net10.0 and net11.0 DLLs, then the net48 EXE.
.\.dotnet\dotnet.exe artifacts\bin\TestFramework.UnitTests\Debug\net10.0\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll --report-trx --progress off
.\.dotnet\dotnet.exe artifacts\bin\TestFramework.UnitTests\Debug\net10.0-windows10.0.18362.0\win-x64\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll --treenode-filter "/*/*/AssertTests/*" --report-trx --progress off

.\eng\bazel.ps1 test //:build_tools_tests //:sdk_package_tests --jobs=6 --nocache_test_results --test_output=errors
```

The owned detached comparison worktree was removed, retained consumer asset
directories were cleaned after copying evidence, and the task-owned Bazel output
base was expunged after saving its reports. Shared caches and other checkouts
were not cleared. The initial isolated SDK invocation nevertheless reported
ASP.NET development-certificate setup and workload installation-record writes;
exact ownership of that first-use state cannot be inferred, so no certificate
or shared SDK rollback was attempted.

## Open questions

The removal is limited to the MSTest 5 development branch. No downstream feature
is enabled and no 4.x servicing/backport is appropriate. External consumers own
their source migration and recompilation; in-repository callers and package
validation remain part of this change.

Windows validation is complete for this scope. Unix execution and CI are not
claimed; the unrelated full-solution MSTEST0037 baseline failures remain outside
the requested change.
