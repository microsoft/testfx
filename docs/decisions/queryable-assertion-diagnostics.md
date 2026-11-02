# Queryable assertion diagnostics: research and decisions

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
`src\Analyzers\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj` using
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
.\.dotnet\dotnet.exe pack src\Analyzers\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj -c Debug --no-restore -v:quiet "-bl:$evidence\queryable-review-pack-{}.binlog"
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
.\.dotnet\dotnet.exe pack src\Analyzers\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj -c Debug --no-restore -v:quiet "-bl:$evidence\queryable-composite-review-pack-{}.binlog"
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

## Open questions

- Extending detection to queries hidden in enumerable aliases or additional
  client-side LINQ chains would require a separately justified analysis scope.
- Content-fetching assertions and consumed `ContainsSingle` results may deserve
  separate guidance, but are not count/existence-only mistakes.
- No downstream rollout, servicing change, or framework runtime change is
  required for this additive analyzer.
