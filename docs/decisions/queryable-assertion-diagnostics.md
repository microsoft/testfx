# Queryable assertions: research and decisions

## Problem and contract

[#11872](https://github.com/microsoft/testfx/issues/11872) requests diagnostics
when collection assertions enumerate an `IQueryable` just to determine its
count or whether it contains elements. The implementation covers
`Assert.HasCount`, `Assert.IsEmpty`, `Assert.IsNotEmpty`, and `Assert.ContainsSingle`
when the returned item is ignored, including an explicit discard.

The rule is MSTEST0090, a Performance-category suggestion enabled by default.
The risk depends on the query provider, so it must not become a build-breaking
warning by default. C# and Visual Basic are supported. No framework APIs or
runtime assertion behavior change.

Provider-side `Count` and `Any` checks, materialized arrays/lists, and
`ContainsSingle` calls whose results are consumed remain valid. Query types are
recognized by symbol identity, including implementations, derived interfaces,
and constrained type parameters. Built-in casts and a direct
`Enumerable.AsEnumerable` do not materialize the query and do not exempt it.
User-defined conversions are opaque: they may actually materialize results.

Queries whose selected assertion uses a collection-count fast path are exempt.
For generic assertions, `ICollection<T>` must match the selected method's `T`.
Non-generic assertions only check `ICollection`; generic `HasCount`/`IsEmpty`
also check it. Generic `IsNotEmpty` only explicitly checks `ICollection<T>`.
`ContainsSingle` still enumerates count-bearing queries, so it keeps its diagnostic.

Ignored results include values forwarded through tuples, conditional branches,
or coalesce fallbacks to a discard. Conditions and coalesce inputs inspect the
returned item and are not treated as ignored.

This is deliberately not general dataflow analysis: queries hidden behind
`IEnumerable` variables or arbitrary methods are outside the initial scope.
Other collection assertions are not automatically considered bad; tests of
retrieved contents legitimately need enumeration.

## Research and experiments

### 2026-10-12: analyzer overlap and assertion implementation

- Read the issue and its complete discussion (no comments). The user subsequently
  requested an assertion rule and asked whether to require materialization or
  distinguish good and bad patterns.
- Reviewed TestFx at `833538df92ef773eace3ab3dbe24f6c54350238d`.
  `CollectionCountHelper.GetCount` falls back to `Enumerable.Count`.
  `IsNotEmptyCore` uses `Enumerable.Any` and short-circuits.
  `ContainsSingle` counts the entire sequence. None requests a provider-side
  scalar operation.
- Reviewed SDK analyzer sources at
  [`b0ab3d8c111329e7e365dc003160075be7d7bdf7`](https://github.com/dotnet/sdk/tree/b0ab3d8c111329e7e365dc003160075be7d7bdf7).
  [CA1851](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1851)
  addresses repeated enumeration, not a single expensive assertion; its tracked
  deferred types exclude `IQueryable`. Its configurable enumeration methods can
  help with ordinary `IEnumerable` assertions, so a blanket repeated-enumeration
  rule would duplicate existing functionality.
  [CA1827](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1827)
  and CA1828 optimize count-versus-existence checks but do not recognize these
  assertion calls.
- MSTEST0037 recognizes `Enumerable`, not `Queryable`, calls. It must continue
  to avoid rewriting provider-side scalar checks into enumerating assertions.
- A `Queryable.Where` before the assertion can still execute provider-side;
  conversion to `IEnumerable` does not retroactively move that filter client-side.
  A predicate passed directly to an assertion, however, runs client-side.

## Alternatives and decision

**No change:** leaves the single-enumeration risk in the ticket undiagnosed.

**Require materialization for all enumerable assertions:** rejected. It adds
buffering to otherwise legitimate streaming/in-memory assertions, overlaps
CA1851, and fetching every matching row is still unnecessary for scalar checks.
`AsEnumerable` is not materialization or caching.

**Chosen: detect query-enumerating count/existence assertions.** Suggest
`Queryable.Count` for cardinality and `Queryable.Any` for emptiness. When a test
actually needs retrieved contents or a snapshot for several assertions, explicit
materialization after provider-side filtering remains appropriate.

No automatic code fix is included initially. Moving a predicate delegate into
provider translation can change supported expressions and exceptions; replacing
an assertion also changes failure details and interpolated-message evaluation.
The diagnostic guides a deliberate choice without claiming equivalent behavior.
It describes a risk rather than guaranteeing SQL translation or a performance
improvement, since `IQueryable` may be in-memory.

## Validation and demonstration

All runs used the repository-pinned SDK `12.0.100-alpha.1.26509.101`, Debug
configuration, and source-built framework references. The analyzer targets
`netstandard2.0` and the repository's Roslyn 3.11 API; its Roslyn test harness
uses 4.10. The working source state is the base
`833538df92ef773eace3ab3dbe24f6c54350238d` plus this feature's patch.

| Run | Exit | Executed / passed / failed / skipped |
| --- | --- | --- |
| Existing assertion analyzer and release-tracking baseline, net8.0 | 0 | 135 / 135 / 0 / 0 |
| New diagnostic and release tracking, net8.0 | 0 | 22 / 22 / 0 / 0 |
| Same selection with reporting removed, net8.0 | 2 | 22 / 9 / 13 / 0 |
| Complete analyzer suite, net8.0 | 0 | 1,962 / 1,962 / 0 / 0 |
| New diagnostic and release tracking, net472 | 0 | 22 / 22 / 0 / 0 |

The 22-case selection contains 19 new cases and three release-tracking cases.
The focused tests compile real C# and Visual Basic inputs and assert diagnostic
IDs, locations and messages. Test identities include
`QueryableCountAndExistenceAssertions_Diagnostic` (four named C# rows),
`VisualBasicQueryableAssertions_Diagnostic` (four named VB rows),
`QueryExpressionsAndOverloads_Diagnostic`,
`QueryImplementationsAndConstraints_Diagnostic`,
`AsEnumerableAndBuiltInCasts_Diagnostic`,
`IgnoredContainsSingleResults_Diagnostic`, and
`VisualBasicQueryPatterns_Diagnostic`. Negative cases cover consumed results,
materialized collections, scalar queries, unrelated assertion methods and
materializing user-defined conversions.
`QueryableScalarChecks_DoNotTriggerMSTEST0037` protects the existing rule's
provider-side exclusion.

The isolated comparison used a task-owned detached worktree at the same base
commit. It retained the full feature patch and changed only
`context.ReportDiagnostic(invocation.CreateDiagnostic(...))` to
`_ = invocation.CreateDiagnostic(...)`. Thus the rule descriptor and test APIs
still existed; this is a **behavior-removal comparison**, not a pristine-base
run. The test file's SHA-256 was identical in both source states:
`79DC3025F483E0455C5D34589EA63FB13BB2D41CF054830C71272B2E548E6D13`.
The retained implementation patch SHA-256 was
`4AA7EC0605ED3911A7EED1E11AC62BC7AF594588FE32A1C35053499B24F805B5`.
All 13 red failures were missing-diagnostic assertions, for example
`Mismatch between number of diagnostics returned, expected "8" actual "0"`
in `QueryExpressionsAndOverloads_Diagnostic`. TRX parsing confirmed identical
selected names, 22 executed cases, and no skips in both states.

Repository-local build scripts bootstrapped the SDK before focused invocations.
The successful build/test commands, with `$evidence` denoting the task-owned
evidence directory, were:

```powershell
.\build.cmd -projects test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -bl
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net8.0 --no-restore -v:quiet "-bl:$evidence\queryable-green-build-{}.binlog"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green.trx --results-directory "$evidence\queryable-green-results"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --report-trx --report-trx-filename analyzers-net8.trx --results-directory "$evidence\queryable-full-results"
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net472 --no-restore -v:quiet "-bl:$evidence\queryable-net472-build-{}.binlog"
.\artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net472\MSTest.Analyzers.UnitTests.exe --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green-net472.trx --results-directory "$evidence\queryable-net472-results"
```

The comparison was rebuilt through `eng\build.ps1 -restore -build -projects`
with the same test project and pinned SDK. Its host invocation used exactly the
same filter as the green run, with separate `queryable-red-results\red.trx`
output. Binlogs and TRX remain in task-owned evidence storage; the concise
results above are versioned reviewer evidence.

The first implementation caught a VB reduced-extension mismatch in
`VisualBasicQueryPatterns_Diagnostic`: `AsEnumerable` uses the invocation
instance rather than a first argument in that operation model. Recognizing
`ReducedFrom` and the instance fixed the failure without changing the expectation.
An initial using-alias ordering error was corrected before test execution.
The command wrapper also rejected an unquoted `{}` binlog argument; subsequent
focused SDK commands quoted the entire switch and preserved native exits.

Localization was regenerated using the owning analyzer project's `UpdateXlf`
target and checked with both `test_check_localization.py` (41 passes) and
`check_localization.py` (364 valid catalogs). No generated XLF was hand-edited.

### Packed consumer demonstration

The shipping contract is the `MSTest.Analyzers` package loaded by the C# compiler
against a `net8.0`, C# 12 consumer. The framework binary comes from the same
source-built Debug layout. The compiler owns analyzer loading; there is no
runtime assertion execution, cross-process protocol, or handshake to validate.

Built `MSTest.GlobalConfigsGenerator`, then packed
`src\MSTest\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj` using
the pinned SDK (exit 0). The produced package was
`MSTest.Analyzers.4.6.0-dev.nupkg`, SHA-256
`9E1E18F7C8B701856CA7E7293A9AC7AC1AE08D7F7439432B06A2BB8262C8EF97`.
Both C# and VB analyzer entries exist. Generated globalconfigs give MSTEST0090
`suggestion` in Default, `warning` in Recommended/All, and `none` in None.

The independent consumer used a fresh, task-owned package cache and source
mapping that restricts `MSTest.Analyzers` to the local Shipping feed.
`project.assets.json` and `.nupkg.metadata` confirmed the chosen cache and feed;
the cached package hash matched the produced package. The cached analyzer DLL
also matched the source-built DLL, SHA-256
`F41265922A46DA9C094597399BB6FC666DECD4DB34D57DFCEA9C238AD03E576F`.

The same consumer source contains `BAD` and good compile-time branches.
Recommended mode with `TreatWarningsAsErrors=true` makes the intended failures
observable. The bad branch has one call from each of the four assertion families;
the good branch uses provider-side `Count`/`Any`, consumes `ContainsSingle`'s
result, and explicitly materializes a content check.

```powershell
.\.dotnet\dotnet.exe build "$evidence\queryable-consumer\consumer.csproj" -v:minimal -p:DefineConstants=BAD "-p:ErrorLog=$evidence\queryable-consumer\bad.sarif" "-bl:$evidence\queryable-consumer-bad-{}.binlog"
.\.dotnet\dotnet.exe build "$evidence\queryable-consumer\consumer.csproj" --no-restore -v:minimal -p:DefineConstants=GOOD "-p:ErrorLog=$evidence\queryable-consumer\good.sarif" "-bl:$evidence\queryable-consumer-good-{}.binlog"
```

The bad branch exits 1 with exactly four MSTEST0090 errors; the good branch exits
0 with no warnings or errors. The compiler messages are a readable demonstration:

```text
MSTEST0090: Use 'Queryable.Count' instead of 'Assert.HasCount' to avoid enumerating query results for a count or existence check
MSTEST0090: Use 'Queryable.Any' instead of 'Assert.IsEmpty' to avoid enumerating query results for a count or existence check
MSTEST0090: Use 'Queryable.Any' instead of 'Assert.IsNotEmpty' to avoid enumerating query results for a count or existence check
MSTEST0090: Use 'Queryable.Count' instead of 'Assert.ContainsSingle' to avoid enumerating query results for a count or existence check
```

An initial consumer restore failed because the SDK also requested the .NET 8
WindowsDesktop reference pack; adding that exact reference-pack ID to the
scratch source mapping resolved it. That restore failure is not red-phase proof.
Text/compiler diagnostics communicate this feature better than staged IDE media.
No runtime database benchmark, downstream release-branch validation or CI run is
claimed.

Parallel-safety preflight: the analyzer tests run on MSTest under MTP, with
assembly-level method parallelization and CPU-count workers (32 in these runs).
Each case owns its Roslyn test workspace and compilation. The new analyzer captures only immutable
compilation symbols and has no mutable per-analysis static state, process-global
mutation, workers, or shared test assets requiring resource declarations.

Cleanup: the comparison worktree registration and stale Git link were removed,
and its generated toolset/output directories and the repository's generated
Python cache were deleted. The comparison source snapshot and all TRX/binlog/
consumer evidence remain in session artifact storage. Windows file locks blocked
complete deletion of the comparison SDK directory, including
`Microsoft.NET.Build.Tasks.dll`; its remaining files are reported rather than
removing unrelated build servers or shared caches. Build-server termination was
restricted to explicitly verified PIDs from the task-owned comparison SDK.

### 2026-10-12: review correction for count-bearing queries

Review identified a false positive for a query that also implements
`ICollection<T>`. Re-reading the framework helpers confirmed that the exemption
must follow the selected overload, not merely the presence of any count:
`ICollection<T>` is invariant, non-generic overloads do not inspect it, and
`ContainsSingle` always enumerates. `IReadOnlyCollection<T>` alone is not an
explicit framework fast path either.

Added `CountBearingQueries_UseCountFastPaths_NoDiagnostic`,
`CountBearingQueriesWithoutMatchingFastPath_Diagnostic`, and
`VisualBasicCountBearingQueries_RespectSelectedOverload`. Their inputs cover
concrete hybrid query types, derived type-parameter constraints, generic and
non-generic collection interfaces, direct casts and `AsEnumerable`, mismatched
generic type arguments, read-only collections, and `ContainsSingle` with and
without a predicate.

The red state was commit `5589cc59be24c54c48127e4b199b4ab0f13e0b39` plus only
the new test/support code. Built into the isolated
`artifacts\queryable-review-red` output/restore area before changing production.
An initially considered output area outside the repository was not used for
execution because `RootFinder` requires the host to have a Git-root ancestor.
The redirected build emitted generated-resource warnings; the normal green
builds had zero warnings/errors. Neither warning output nor compilation failure
was used as behavioral proof.

With the selector `FullyQualifiedName~CountBearingQueries`, three cases executed
in both states. Red exited 2 with two intended assertion failures and one pass:
the C# negative case expected zero diagnostics but received 13; the VB mixed case
expected three but received seven. Green exited 0 with three passes and no skips.
The retained test file had the same SHA-256 in both states:
`4EA11A7297B7D8A12F60983911AA3F81DF4597B9EE021589C9406EF51F6BF239`.
The full focused analyzer/release-tracking selection then passed 25/25 cases
with no skips on both net8.0 and net472. Assembly method parallelization remained
enabled, with 32 workers and no shared mutable test resources.

The commands below use the same `$evidence` convention as above. All builds and
green test commands exited 0; the isolated red host deliberately exited 2.

```powershell
$red = Join-Path (Get-Location) 'artifacts\queryable-review-red'
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net8.0 -v:quiet "-p:ArtifactsDir=$red\" "-bl:$evidence\queryable-review-red-build-{}.binlog"
.\.dotnet\dotnet.exe "$red\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll" --filter "FullyQualifiedName~CountBearingQueries" --report-trx --report-trx-filename red.trx --results-directory "$evidence\queryable-review-red-results"
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net8.0 --no-restore -v:quiet "-bl:$evidence\queryable-review-green-build-{}.binlog"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~CountBearingQueries" --report-trx --report-trx-filename regression-green.trx --results-directory "$evidence\queryable-review-green-results"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green.trx --results-directory "$evidence\queryable-review-green-results"
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net472 --no-restore -v:quiet "-bl:$evidence\queryable-review-net472-build-{}.binlog"
.\artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net472\MSTest.Analyzers.UnitTests.exe --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green-net472.trx --results-directory "$evidence\queryable-review-green-results"
.\.dotnet\dotnet.exe pack src\MSTest\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj -c Debug --no-restore -v:quiet "-bl:$evidence\queryable-review-pack-{}.binlog"
```

Repacked the analyzer and restored a separate net8.0/C# 12 consumer into another
fresh, task-owned cache. The current package/cache SHA-256 matched:
`9A8CAA8E8956FC598A6CAA9BA1E0D802CDC0F2D35B7D19B0E81D57EBB169F233`.
The count-bearing query's matching generic assertions compiled with no MSTEST0090
and exit 0. `ContainsSingle`, a widened `HasCount<object>` and a non-generic
`IsEmpty` call over a generic-only collection produced exactly three MSTEST0090
errors and exit 1. Both builds used Recommended mode and warnings-as-errors:

```powershell
.\.dotnet\dotnet.exe build "$evidence\queryable-review-consumer\consumer.csproj" -v:quiet -p:DefineConstants=GOOD "-p:ErrorLog=$evidence\queryable-review-consumer\good.sarif" "-bl:$evidence\queryable-review-consumer-good-{}.binlog"
.\.dotnet\dotnet.exe build "$evidence\queryable-review-consumer\consumer.csproj" --no-restore -v:quiet -p:DefineConstants=BAD "-p:ErrorLog=$evidence\queryable-review-consumer\bad.sarif" "-bl:$evidence\queryable-review-consumer-bad-{}.binlog"
```

Only focused suites were rerun for this correction; the earlier full-suite
result remains evidence for the initial implementation, not a CI claim for
the review-fix commit. No localization resources or runtime framework behavior
changed.

### 2026-10-12: null-guard clarity and discarded composite expressions

Code-quality review flagged the nullable `collectionType` access. The existing
`DerivesFrom` helper already returns false for null and declares
`NotNullWhen(true)`, so the previous short circuit was safe. Added an explicit
guard at the call site to make that behavior locally visible without depending
on interprocedural annotation support.

The review summary also identified a real missed diagnostic: a `ContainsSingle`
result forwarded through a conditional or tuple into a discard was not recognized.
The parent walk now follows tuple elements, conditional branch results, and
coalesce fallbacks. It does not follow conditional conditions or coalesce inputs,
because those inspect the item, and it still stops at other consumers and
user-defined conversions.

Added `CompositeContainsSingleDiscardedResults_Diagnostic`,
`CompositeContainsSingleConsumedResults_NoDiagnostic`, and
`UntypedNullCollections_NoDiagnostic`. With unchanged production at
`de1404264175c3c205386ecb2f71a5aabc5858a0`, retained new tests, and isolated
`artifacts\queryable-composite-review-red` outputs, the exact three-case selection
executed and exited 2: the discarded-composite case expected eight diagnostics
but received zero; the consumed-composite and untyped-null cases both passed.
The same selection passed all three cases after the change (exit 0, no skips).
The null guard therefore has green-before/green-after evidence, while the
composite behavior has an intended red/green assertion comparison.

The test-file SHA-256 was unchanged between those runs:
`37E8EE8C850FB446131A2D670E9BB32476189C793774A2247C1FE32343368E38`.
The complete focused analyzer/release-tracking selection passed 28/28 cases on
net8.0 and net472, with no skips. Normal-output builds had zero warnings/errors;
the redirected red build had generated-resource warnings but compiled and
executed the intended assertions successfully. Method-level parallelization
remained enabled with 32 workers and per-test Roslyn workspaces.

```powershell
$red = Join-Path (Get-Location) 'artifacts\queryable-composite-review-red'
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net8.0 -v:quiet "-p:ArtifactsDir=$red\" "-bl:$evidence\queryable-composite-review-red-build-{}.binlog"
.\.dotnet\dotnet.exe "$red\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll" --filter "FullyQualifiedName~CompositeContainsSingle|FullyQualifiedName~UntypedNullCollections" --report-trx --report-trx-filename red.trx --results-directory "$evidence\queryable-composite-review-red-results"
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net8.0 --no-restore -v:quiet "-bl:$evidence\queryable-composite-review-green-build-{}.binlog"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~CompositeContainsSingle|FullyQualifiedName~UntypedNullCollections" --report-trx --report-trx-filename regression-green.trx --results-directory "$evidence\queryable-composite-review-green-results"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net8.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green.trx --results-directory "$evidence\queryable-composite-review-green-results"
.\.dotnet\dotnet.exe build test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -f net472 --no-restore -v:quiet "-bl:$evidence\queryable-composite-review-net472-build-{}.binlog"
.\artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net472\MSTest.Analyzers.UnitTests.exe --filter "FullyQualifiedName~AvoidEnumerableAssertionsOnQueryableAnalyzerTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --report-trx-filename green-net472.trx --results-directory "$evidence\queryable-composite-review-green-results"
.\.dotnet\dotnet.exe pack src\MSTest\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj -c Debug --no-restore -v:quiet "-bl:$evidence\queryable-composite-review-pack-{}.binlog"
```

All builds and green runs exited 0. The repacked analyzer was consumed with a
new `.packages-composite` cache. The package/cache SHA-256 matched:
`8D128D4B7BB6FEE4713ED1DDA247E944D368FA2A4D14E2EECFE2E330144BA7D2`.
Consumed conditional/tuple results and control-flow inputs compiled successfully
with no MSTEST0090. The bad branch produced eight MSTEST0090 errors: five for
discarded composite results and three existing count/enumeration cases. This
consumer initially hit an unused-tuple-local compiler warning promoted to an
error; explicitly consuming its good-branch values corrected the fixture without
changing or suppressing product diagnostics.

```powershell
$consumer = "$evidence\queryable-review-consumer"
$cache = "$consumer\.packages-composite"
.\.dotnet\dotnet.exe build "$consumer\consumer.csproj" -v:quiet -p:DefineConstants=GOOD "-p:RestorePackagesPath=$cache" "-p:ErrorLog=$consumer\composite-good.sarif" "-bl:$evidence\queryable-composite-consumer-good-{}.binlog"
.\.dotnet\dotnet.exe build "$consumer\consumer.csproj" --no-restore -v:quiet -p:DefineConstants=BAD "-p:RestorePackagesPath=$cache" "-p:ErrorLog=$consumer\composite-bad.sarif" "-bl:$evidence\queryable-composite-consumer-bad-{}.binlog"
```

The good consumer exited 0 with zero warnings/errors; the bad consumer exited 1
with only the eight expected MSTEST0090 errors. No CI result is inferred from
these local runs.

## 2026-11-23: native scalar assertion support

### Problem and contract

Issue [#12244](https://github.com/microsoft/testfx/issues/12244) asks for
provider-native scalar behavior in `Assert.HasCount`, `Assert.IsEmpty`, and
`Assert.IsNotEmpty`.

- Add overloads taking `IQueryable<T>` alongside the existing
  `IEnumerable<T>` overloads. Keep both message-string and interpolated-string
  handler call shapes.
- Use `Queryable.Count` for `HasCount` and `IsEmpty`. `IsEmpty` currently
  reports the exact actual count on failure; using `Any` would either lose that
  diagnostic or require another provider execution.
- Use `Queryable.Any` for `IsNotEmpty`.
- Add matching `IQueryable<T>` handler constructors so interpolated messages
  retain lazy evaluation and use the same provider operation as the selected
  assertion overload.
- Do not catch provider failures or fall back to `Enumerable`. A provider that
  cannot translate the selected scalar operation should surface its failure;
  enumeration could be expensive or change semantics. No database provider or
  SQL behavior is promised.
- Make MSTEST0090 recognize that direct calls select native overloads. Keep its
  warning for queryables explicitly viewed as `IEnumerable<T>` (including
  `AsEnumerable()`), and leave `ContainsSingle` unchanged.

The existing public `IEnumerable<T>` signatures and their behavior remain
unchanged. A statically typed `IQueryable<T>` now selects the more-specific
overload; a cast or `AsEnumerable()` retains enumerable semantics. This is an
intentional source-binding and runtime behavior change for direct queryable
calls, and the overloads and handler constructors must be tracked in the
unshipped public API baseline.

### Research and alternatives

- On 2026-11-23, read #12244 and the current assertion, interpolated-handler,
  analyzer, public-API, and C# 12 source-compatibility implementations at base
  `8c841a21bf80dd5b19330547ed875f47e853d2bc`. The framework currently targets
  `netstandard2.0` and supported .NET Framework/.NET TFMs; the analyzer package
  targets `netstandard2.0`. The package IDs are `MSTest.TestFramework` and
  `MSTest.Analyzers`, with repository version
  `5.0.0-preview.26571.1`.
- `CollectionCountHelper.GetCount` currently probes `ICollection<T>`,
  non-generic `ICollection`, then `Enumerable.Count`; `IsNotEmptyCore` uses
  `ICollection<T>` or `Enumerable.Any`. LINQ's `Enumerable.Count` checks
  in-memory collection and iterator capabilities; it does not dispatch to
  `IQueryable.Provider`.
- Runtime-type probing for `IQueryable` was considered and rejected. Since
  `AsEnumerable()` returns the same object, such a probe cannot preserve the
  caller's choice to bind as `IEnumerable<T>`; it would also ask providers to
  translate `Count` or `Any` even when their supported operation is enumeration.
  Explicit overloads make provider execution opt-in through the static type.
- The prior design, MSTEST0090 alone, remains a valid workaround but does not
  provide native assertion syntax. Broad support for other assertions remains
  out of scope because their return values, predicates, and diagnostic
  contracts differ.

### Consumer and validation plan

The producer is `MSTest.TestFramework` (`MSTest.TestFramework.dll`, all package
TFMs); `MSTest.Analyzers` consumes the framework API to decide whether
MSTEST0090 applies. The consumer boundary is a C# 12 package consumer using
the repository-pinned SDK `12.0.100-alpha.1.26570.125`. There is no process
launch, handshake, cancellation protocol, external service, or result
reporting boundary in this feature. The feature author owns local package,
analyzer, and source-compatibility validation; release-branch servicing or
backport decisions remain for maintainers before release.

| Requirement / risk | Automated evidence | Rendered check / gap |
| --- | --- | --- |
| Direct `IQueryable<T>` assertions use provider scalar execution, do not enumerate, and report correct outcomes | Provider-recording unit tests for `HasCount`, `IsEmpty`, and `IsNotEmpty`, including failure diagnostics | Nonvisual API behavior |
| Message and handler paths use provider operations while preserving lazy message evaluation | Handler tests for successful and failing assertions | Nonvisual API behavior |
| Framework and analyzer packages work together for C# consumers | `PackagedQueryableAssertionsUseNativeOverloads` builds and runs against both Shipping packages; it checks no warning for three direct calls and warnings for enumerable-bound calls and `ContainsSingle` | Compiler diagnostics are the affected surface; no IDE layout change |
| Explicit enumerable binding remains enumerable-bound; analyzer guidance matches the selected overload | MSTEST0090 unit tests for direct queryable calls, `AsEnumerable()` / enumerable casts, and `ContainsSingle` | No IDE surface/layout change |
| Existing array, enumerable, named-argument, message, handler, and queryable calls remain compilable | Packed C# 12 `AssertSourceCompatibilityTests` consumer | No rendered surface |

**Source-compatibility caveat:** adding these overloads makes an existing explicit-generic null/default call such as `Assert.IsEmpty<int>(default!)` ambiguous with the array overload (`CS0121`). The C# 12 consumer exposed the same ambiguity for `HasCount`, `IsEmpty`, and `IsNotEmpty`. Callers of those shapes must explicitly bind to `IEnumerable<T>`, for example `Assert.IsEmpty<int>((IEnumerable<int>)default!)`; the source-compatibility fixture now uses that cast. This is a source break for null/default-literal calls, while existing method signatures and binaries remain unchanged.

Parallel-safety preflight found no shared mutable test resource: TestFramework
unit tests run through the sequential `TestContainer` engine; analyzer tests run
at method-level parallelism but use per-test compiler inputs; the packed
consumer harness creates a unique temporary directory and package cache per
asset. The change does not add process/global state or change test scheduling.

## Validation and test-value evidence (2026-11-24)

Base commit: `8c841a21bf80dd5b19330547ed875f47e853d2bc`. The implementation is
uncommitted; validation below applies to the source and tests in this worktree.
Source-and-test diff SHA-256 (excluding this decision record):
`0CD688F50B1C51F949FBAF96CF56253E290A87AFE47F5A211EE04F8064A6EE84`.

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- | --- |
| `Count_IQueryable_UsesQueryableCountWithoutEnumerating` | In a detached comparison worktree, changed the `IQueryable<T>` `HasCount` helper from `Queryable.Count` to `Enumerable.Count`; mutation patch SHA-256 `F72DEB890A6E1342DBEBB9857FBADED34194C9326D0E2A0591BC30906C608C46` | Executed: 1 failed, at the assertion expecting provider operation `Count`; exit 2 | Executed: 1 passed; exit 0 | Buildable behavior-removal test; feature patch was applied to the comparison worktree before the mutation. |
| Provider-backed behavior for `HasCount`, `IsEmpty`, and `IsNotEmpty`; analyzer binding and enumerable-bound cases | No separate counterfactual for this group | Unverified | Focused analyzer tests: 20 passed, 0 failed, 0 skipped on `net10.0`; prior framework TRX reports include all 10 new provider assertion tests passing | Analyzer TRX: `artifacts\TestResults\QueryableAnalyzer\queryable-analyzer-targeted.trx`; framework TRX: `artifacts\TestResults\Debug\TestFramework.UnitTests_net10.0_x64.trx`. |
| Shipping package consumer and C# 12 source compatibility | Not applicable to the scalar-behavior mutation | Unverified | 3 outer acceptance cases passed, 0 failed, 0 skipped: `PackagedQueryableAssertionsUseNativeOverloads` and `PublicAssertCallShapes_CompileWithCSharp12` for `net11.0` and `net10.0`; exit 0 | Release consumer restored the current framework package into an isolated cache and the source-compatibility helper compared the cached/runtime assembly payload with the package. |

The final Release pack used `.\build.cmd -pack -c Release -v:quiet
"/bl:artifacts\TestResults\QueryableFeature\pack-final2-73884039168040b28b81d85941c401ba\pack.binlog"`
and exited 0. Package SHA-256 values were:

- `MSTest.TestFramework.5.0.0-dev.nupkg`: `4DBA9718D2EC92F8DC56CCEA3DFE909EAD352A833A30A65096CE39697E7D0009`
- `MSTest.Analyzers.5.0.0-dev.nupkg`: `E532875C61C2DFD7F136A610489000AD345AA98943D2C60EEE3927171C663286`

The acceptance host build was
`.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -v:quiet "/bl:artifacts\TestResults\QueryableFeature\acceptance-final2-e25e3b5adf8743f59b84750841dc0bff\acceptance-build.binlog"`
(exit 0). Independent discovery used `dotnet test --project
test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj
-c Release --no-build --list-tests json` with `UsingDotNetTest=true` and
reporter arguments cleared (exit 0). Execution used `--minimum-expected-tests 3`
and these discovered UIDs:

```text
11f41e7f-7852-80aa-a58d-352634ea13b4
1e590805-3ea6-85a5-99e8-41c04e1c164d
18d4e45e-17fa-8a73-a26c-a78f8321271a
```

The acceptance verifier reported planned=3, selected=3, executed=3, passed=3,
failed=0, skipped=0, outer process exit=0. TRX:
`artifacts\TestResults\QueryableFeature\acceptance-final2-e25e3b5adf8743f59b84750841dc0bff\MSTest.Acceptance.IntegrationTests_net12.0_x64.trx`.

The full analyzer suite was also attempted on `net10.0`: 3,049 passed, 10 failed,
and 1 skipped. The failures were unrelated global-config-generator continuation
tests because expected `artifacts\bin\MSTest.GlobalConfigsGenerator\Debug\net12.0`
outputs were absent. The changed queryable analyzer cases were included in the
focused 20/20 passing selection; the full analyzer suite is not claimed green.
The first package-consumer attempt also confirmed the null/default-literal
ambiguity above; a subsequent attempt exposed and corrected an invalid
`IsEmpty` test input. The final package consumer run passed.

These are local results, not CI results. UI rendering is not applicable to this
nonvisual API/analyzer change. Release-branch servicing/backport decisions
remain for maintainers before release.

## Open questions

- Extending detection to queries hidden in enumerable aliases or additional
  client-side LINQ chains would require a separately justified analysis scope.
- Content-fetching assertions and consumed `ContainsSingle` results may deserve
  separate guidance, but are not count/existence-only mistakes.
- No downstream rollout or runtime-protocol change is included here; maintainers
  still need to decide release-branch servicing/backports before release.
