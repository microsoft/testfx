# Test method cancellation tokens: research and decisions

## Problem and contract

Tracked by [#11986](https://github.com/microsoft/testfx/issues/11986).

MSTest test methods may declare one additional, trailing, by-value
`CancellationToken`. A test without data can take that parameter alone; a test
with X data arguments can take X data parameters followed by the token.
The adapter supplies the current `TestContext.CancellationToken`, not a new or
independently linked token.

The token is bound immediately before the test body, after initialization and
inside the existing timeout/cancellation scope. Discovery, display names,
`TestContext.TestData`, serialization and custom data sources retain their data
arguments without an injected token. Each data row and retry obtains its own
current context token. Cleanup cancellation and token ownership are unchanged.
Lifecycle methods are outside this feature.

Compatibility decisions:

- Only a trailing `CancellationToken` is injectable. Nullable tokens, token
  sources and by-reference tokens are not injectable.
- Existing full-arity data or custom invocations that explicitly supply the
  final token retain their behavior; the adapter does not overwrite user data.
- Ordinary data binding, including optional arguments, tuple-valued arguments,
  single `object[]` arguments and generic inference, remains applicable to the
  data parameters. Missing required data and extra arguments still fail.
- Reflection and source-generated invokers receive the same completed arguments.
  No new public API, attribute, option, capability handshake or fallback is needed.
- Ordinary `params` arrays remain data, including `params CancellationToken[]`.
  C# requires `params` to be last, so this trailing-token convention does not
  inject a token before a `params` parameter.

The producer is the shared adapter engine in
`MSTestAdapter.PlatformServices.dll`, shipped by `MSTest.TestAdapter`; consumers
use the aligned `MSTest.TestFramework`, `MSTest.Analyzers` and, when enabled,
`MSTest.SourceGeneration` packages. The source branch is `main`-based MSTest
4.6.0-preview / MTP 2.6.0-preview. The repository SDK is pinned in `global.json`
to `12.0.100-alpha.1.26509.101`. Exact built package versions, hashes and consumer
paths will be recorded with execution evidence.

This is an additive convenience for a future feature release; no release-branch
backport, downstream enablement, flag or denylist is proposed. The feature owner
retains responsibility for aligned package validation. Host process exit,
result aggregation and cancellation classification remain existing contracts.

## Research and experiments

### 2026-10-16: source inspection

Baseline: `2cf09d01522dcca5bf6bee848c9ebb90d86f9255`.

- `TestMethodValidator` and `TestMethodShouldBeValidAnalyzer` already allow
  parameterized methods. `TypeCache` uses signature checking with parameter-count
  validation disabled. The standalone signature helper still needs to recognize
  a token-only method when parameter-count validation is requested.
- `TestMethodInfo.SetArguments` resolves optional/params arguments before
  invocation. Token injection there would capture a token before initialization
  and timeout setup; bind it in `ExecuteInternalAsync` instead.
- Reflection and generated invokers share
  `MethodInfoExtensions.GetInvokeResultWithParametersAsync`. Completing arguments
  before this shared boundary avoids two independent injection implementations.
- `TestDataSourceHelpers` normalizes tuples and single `object[]` arguments during
  both discovery and execution. It must recognize an additional trailing token
  without changing full-arity data.
- `CombinatorialDataAttribute` enumerates every method parameter, so it must
  exclude the injected token from the Cartesian product. Display-name metadata
  must likewise recognize a single data-array parameter followed by a token.
- `DataRowShouldBeValidAnalyzer` validates counts, types and generic inference
  against the method parameters. Its data-parameter view must exclude an omitted
  injectable token. `DynamicDataShouldBeValidAnalyzer` validates source members,
  not row arity, and needs no change.

### 2026-10-16: implementation and executed experiments

- A first net8-only build passed. Packing the framework's actual net462 and
  netstandard2.0 targets then exposed their intentional lack of `System.Index`.
  Replaced the new framework array-end indexing with ordinary integer indexing;
  the declared framework/adapter target lists now build and pack successfully.
- Kept explicit full-arity tokens, including reflection's null-to-default
  conversion. The existing single-object-array convention needs to distinguish
  `[dataArray, suppliedToken]` from an entire array of data values; both runtime
  binding and the analyzer retain that distinction.
- Combinatorial data excludes an unprovided trailing token, but retains an
  explicit `ICombinatorialValuesProvider`. A token-only combinatorial method
  generates one empty row; truly parameterless combinatorial behavior is unchanged.
- Final compatibility inspection found that checking provider instances before
  ordinary row generation constructed an explicit trailing-token provider twice.
  Strengthened `GetDataPreservesExplicitCancellationTokenValueProvider` with an
  exact constructor-count assertion: it executed and failed with expected 1,
  actual 2. The eligibility check now reads `CustomAttributeData` without creating
  attributes, leaving provider instantiation to the existing values helper.
  The constructor counter is owned by that one sequential TestContainer test and
  restored in `finally`. Framework and adapter packages were repacked afterward.
- The first invocation test captured its retained execution context before
  installing its instance factory. Moving the capture inside the owned factory
  scope corrected the test setup; this was not a product defect.
- The packed-consumer build caught use of the internal `ITestDataRow` interface
  in the test asset. Changed the asset to public `TestDataRow<T>` sequences.
- Reflection-free generation already rejects generic methods (`AOTSG0004`).
  Generic token methods run in reflection and Rooting modes, and unit tests cover
  integer/string inference. The reflection-free asset excludes only that
  pre-existing unsupported shape, while executing the remaining token methods.
- Reflection-free invokers already report a wrong argument type as an
  `InvalidCastException` before entering user code; reflection/Rooting report the
  friendly argument-mismatch diagnostic. The negative tests pin those existing
  mode-specific outcomes rather than introducing unrelated diagnostic changes.
- The selected unit suites execute 255 tests successfully. Before the final
  provider-constructor assertion was added, retaining the identical unit test
  files in an isolated baseline checkout produced 17
  attributable behavioral failures: eight adapter/signature, three
  framework/combinatorial/name, and six analyzer cases; no tests are skipped.
  The provider-constructor assertion has its own attributable red/green check
  against the initial feature implementation. After that correction, all 255
  unit cases and ten packed acceptance cases were rebuilt and rerun successfully;
  the final-source packed no-injection control still produces eight behavioral
  failures in the identical ten-case selection.
- The packed feature consumer executes in native MTP reflection, Rooting and
  reflection-free modes, plus VSTest. All ten outer acceptance cases pass,
  including nine deliberately invalid child rows and retry-token rebinding.
  The existing execution verifier confirms planned=selected=executed=passed=10,
  failed=skipped=0 and parent exit 0; JUnit and CTRF each contain ten passed cases.
- Packed behavior-removal proof retains the same acceptance source/assertions,
  framework, analyzer and generator packages, but removes only
  `arguments = GetArgumentsWithCancellationToken(arguments)` before the shared
  invocation. After rebuilding/repacking the adapter, all ten outer cases
  execute: eight fail at attributable behavioral assertions, two pass, none
  skip, and the parent exits 2. MTP positive/retry children exit 2 instead of 0;
  VSTest exits 1 instead of 0. The generated invalid-type diagnostic also changes
  because the missing token now fails the argument-count check first.
- The first nested-checkout control attempt encountered two identical
  `.globalconfig` policies and failed during asset compilation; that run is
  **not** red-phase evidence. Setting the comparison copy's `global_level=101`
  disambiguated precedence while retaining the identical
  `require_api_files=true` policy. The successful behavioral control uses fresh
  assets and a separate per-run NuGet cache.
- Package SHA-256 checks prove that the reflection, Rooting and reflection-free
  consumers contain the exact framework/adapter payloads from their respective
  green or control NuGets. `project.assets.json` identifies net11.0, the isolated
  cache, MSTest 4.6.0-dev, and MTP 2.6.0-dev. Fixture cleanup removes that cache
  after each run, but the resolved-assets record, built consumers and payload
  hashes were retained; no shared cache was cleared.
- Restored the injection call in the isolated comparison, rebuilt/repacked the
  adapter, and reran the identical ten outer acceptance cases with fresh
  consumers/cache: ten pass, zero fail/skip, parent exit 0. The final production
  and test source manifest still matches the delivered working-tree files.

### 2026-10-18: review feedback and binding compatibility

Review comparison base: `dbd69abc4ad58e501f3d05e57ff86a649aaef592`.
The review batch preserves the trailing-scalar-token contract and the upstream
dispatcher/lifecycle changes merged before it.

- An already supplied single `object[]` argument must not be wrapped a second
  time before injection. Preserve its identity and elements for both required
  and optional tokens, direct invocation and stored arguments, including empty
  and nested arrays.
- A valid full-arity tuple supplies its own token before either single-object
  or single-array interpretation. Reuse the existing tuple expander for modern
  `ITuple` and legacy `Tuple`/`ValueTuple` paths. Retain intentional tuple-valued
  data when arity or assignability does not describe an explicit-token row.
  This avoids a new data-source API or separate legacy normalization policy.
- Empty rows with an optional generic data parameter still require generic
  inference diagnostics. Run the existing generic analyzer before its
  empty-row return; explicit data continues to infer the type.
- Accept the nullable `is true` pattern and the combinatorial test's
  `.Select(GetData)` projection. The existing implicit LINQ import makes an
  additional explicit using unnecessary; these suggestions preserve behavior.
- Retain identical test bytes in an isolated comparison checkout. An initial
  incremental analyzer build skipped the copied test because copying preserved
  an older source timestamp. Its 38-test result was discarded, not counted as
  regression evidence. A forced rebuild executed all 39 tests and reproduced
  the intended missing-diagnostic failure (expected two, actual zero).
- The first isolated acceptance attempt stopped in shared fixture initialization
  because the focused feed lacked unrelated package-version prerequisites; it
  is not behavioral red evidence. Stage the unchanged support packages
  byte-for-byte in both feeds, retain their hashes, and rebuild/repack each
  variant's changed products. The analyzer's shipping entry point is
  `MSTest.Analyzers.Package`, not the non-packable analyzer implementation
  project; verify its actual DLL payload rather than trusting `pack` exit 0.

The pre-fix production with all new tests executes 195 modern adapter, 193
legacy adapter, 47 framework and 39 analyzer cases. Four adapter tests fail on
each target for array identity/tuple expansion, one analyzer test fails for the
missing diagnostics, and the remaining cases pass; none are skipped. The
framework projection remains green against the unchanged production code.

## Alternatives and decision

The existing approach is to retrieve `TestContext.CancellationToken` inside the
test body. It remains supported but requires context access solely to pass a
token. A general parameter-injection system or a new opt-in attribute adds an
unnecessary API and ambiguity.

Choose one narrowly defined trailing-token convention and complete the argument
list at the shared execution boundary. Preserve explicit full-arity tokens rather
than reserving all trailing tokens unconditionally, which would break existing
`DynamicData` tests and custom executors. Resolve optional data arguments without
materializing a default token, then obtain the current token after initialization.
Reconsider this decision if a supported data-source shape cannot distinguish its
data from a supplied token without changing existing behavior.

## Validation and demonstration

Built packages are `MSTest.TestFramework`, `MSTest.TestAdapter`,
`MSTest.Analyzers` and `MSTest.SourceGeneration` **4.6.0-dev**, with the aligned
MTP/TrxReport dependencies **2.6.0-dev**. Source configuration is Release.
The framework builds netstandard2.0, net462, net8.0, net9.0, net10.0 and net11.0;
the adapter builds net462 and the four modern .NET targets. Classic/modern UWP
and WinUI builds are excluded from this focused validation.

Representative executed commands, from the worktree root:

```powershell
.\.dotnet\dotnet.exe build test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -c Release -f net8.0 -p:SupportedNetFrameworks=net8.0 -p:SkipUapTargetFramework=true -p:ModernUwpMinimum= -p:WinUiMinimum= -p:ErrorOnOutOfDateXlf=false -bl:artifacts\tmp\cancellation-toolchain\build-MSTestAdapter.PlatformServices.UnitTests-r5.binlog
.\.dotnet\dotnet.exe pack src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj -c Release -p:SkipUapTargetFramework=true -p:ModernUwpMinimum= -p:WinUiMinimum= -p:ErrorOnOutOfDateXlf=false -bl:artifacts\tmp\cancellation-toolchain\provider-fix-adapter-pack.binlog
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net11.0 --no-build --no-launch-profile --filter "ClassName=MSTest.Acceptance.IntegrationTests.TestMethodCancellationTokenTests" -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$PWD\artifacts\TestResults\Release\cancellation-acceptance-final" -p:SkipUapTargetFramework=true -p:ModernUwpMinimum= -p:WinUiMinimum= -bl:artifacts\tmp\cancellation-toolchain\acceptance-test-final.binlog --show-test-results all
```

The unit build and project pack exit 0. The same unit build is applied to
`TestFramework.UnitTests` and `MSTest.Analyzers.UnitTests`. Direct test-host
selectors are `/*/*/(TestMethodInfoTests)|(MethodInfoExtensionsTests)/*`,
`/*/*/(CombinatorialDataAttributeTests)|(DataRowAttributeTests)/*` and
`ClassName=MSTest.Analyzers.Test.DataRowShouldBeValidAnalyzerTests`, respectively.
Their runs export and validate TRX, JUnit and CTRF.

The repository entry point bootstrapped the pinned SDK. Its initial attempts
encountered a missing legacy Windows PowerShell PATH entry and exhausted system
temporary storage; process-scoped PATH/TEMP/TMP overrides resolved those.
`ErrorOnOutOfDateXlf=false` is a command-line-only workaround for pre-existing
AzureDevOpsReport catalog drift; no RESX/XLF or shared build settings are changed.
The root Windows package verifier expects the complete product/package matrix,
so focused packing uses the actual project pack targets after the repository
entry-point attempt. Package layout and consumer binaries are checked separately.

Working evidence is retained in task-owned
`artifacts\tmp\cancellation-toolchain` (binlogs, command output, package hashes,
comparison patches) and `artifacts\TestResults\Release\cancellation-*` (reports).
These local diagnostics are not public artifact links or a CI claim.

### Test-value evidence

The initial feature validation used uncommitted source based on
`2cf09d01522dcca5bf6bee848c9ebb90d86f9255`. Its tracked source/test patch SHA-256 is
`46DD2AC398BBD8A72024E65BDAEDF4D3474CA538B2E033AFD416C0CE0DD7A5CA`;
the new acceptance file SHA-256 is
`B2D1F1A07D6CAB1BCBDFB1BF0A4BEE425CCA60B94637A052D2B23730D763DEE3`.
The behavior-removal patch SHA-256 is
`FC9EC1D108E6B20D5A6155AE16BB840B39C4B3141C585E6A8C29CF9F24F43328`.
These hashes come from raw tool output, not abbreviated/reconstructed identities.

| Contract / named test | Without behavior | Correct implementation |
| --- | --- | --- |
| Plain token and data/repeated invocation: `InvokeAsyncInjectsCurrentCancellationTokenForSupportedReturnTypes`, `InvokeAsyncRefreshesInjectedCancellationTokenForRepeatedRows`, `InvokeAsyncResolvesOptionalDataButInjectsOmittedOptionalCancellationToken` | Executed: detected; baseline rejects token-only/data signatures or binds the wrong arguments | Executed: all 170 selected adapter/signature tests pass |
| Combinations and data-array names: `GetDataExcludesTrailingCancellationTokenFromCombinations`, `GetDataWithOnlyCancellationTokenProducesOneEmptyRow`, `GetDisplayNamePreservesSingleObjectArrayBeforeTrailingCancellationToken` | Executed: detected; unsupported token dimension or different exact display name | Executed: all 47 selected framework tests pass |
| Explicit combinatorial provider: `GetDataPreservesExplicitCancellationTokenValueProvider` | Executed: detected in the initial feature implementation; expected one constructor call, actual two | Executed: one call with non-instantiating eligibility detection; final 47-case framework run passes |
| Analyzer count/type/inference: `WhenTrailingCancellationTokenIsOmitted_NoDiagnostic`, `WhenOmittedCancellationTokenFollowsGenericData_NoDiagnostic` | Executed: detected; unexpected count/diagnostic result | Executed: all 38 selected analyzer tests pass |
| Packed composition: `Mtp_InjectsTokensWithoutChangingUserData`, `Mtp_RebindsCancellationTokenForRetry`, `Vstest_InjectsTokensWithoutChangingUserData` | Executed: detected in the repacked one-line behavior-removal control; same ten outer identities, eight behavioral failures | Executed: all ten outer acceptance cases pass, covering 26 reflection, 26 Rooting, 25 reflection-free and 26 VSTest positive child cases, plus invalid data and retry cases |

The feature is a method-signature convenience, so an exact source example and
real test-result transcript communicate it better than a screenshot. Evidence
will distinguish the outer test runner from intentionally failing child hosts.

### Params-array publication preflight

On the requested follow-up, inspection confirmed that the injected-token check
matches a trailing `CancellationToken` scalar, not the element type of an array.
The existing required/optional/params binding path therefore needs no production
change.

- Added `InvokeAsyncPreservesParamsCancellationTokenArrays`: empty, singleton and
  multiple explicit tokens must be packed into the declared array without
  injecting the context token or changing `TestContext.TestData`.
- Strengthened `OptionalParamsAndInvalidArgumentsProduceExactResults`: the four
  generated `ParamsArray` rows now assert their independent expected lengths
  (0, 1, 2, 3) and every string value, not merely the fixed argument.
- The identical three focused params unit tests pass against pristine production
  at `2cf09d01522dcca5bf6bee848c9ebb90d86f9255` and feature production: three
  executed/passed, no skips, exit 0 in both states. This is preservation
  green/green evidence, not a missing-feature red phase.
- The packed legacy cases pass on net462, net8.0, net9.0, net10.0 and net11.0.
  Each outer case asserts 11 child passes and three intentional invalid-data
  failures, including four params-array executions with exact values.
- The final combined selection passes 171 adapter, 47 framework and 38 analyzer
  unit tests, plus 15 packed acceptance cases. Planned, selected, executed and
  passed counts agree; there are no skipped cases.

The final acceptance command is the command above with the filter
`ClassName=MSTest.Acceptance.IntegrationTests.TestMethodCancellationTokenTests|FullyQualifiedName~LegacyDataRowBehaviorTests.OptionalParamsAndInvalidArgumentsProduceExactResults`,
`--minimum-expected-tests 15`, results under
`artifacts\TestResults\Release\cancellation-params-pr`, and binlog
`artifacts\tmp\cancellation-toolchain\params-acceptance-test.binlog`:
exit 0, 15 executed/passed. The focused params selector is
`/*/*/TestMethodInfoTests/(InvokeAsyncPreservesParamsCancellationTokenArrays)|(ResolveArgumentsShouldReturnEmptyParamsWithNoneProvided)|(ResolveArgumentsShouldReturnPopulatedParamsWithAllProvided)`;
its pristine and feature runs are retained under `params-baseline` and
`params-feature`. The owned comparison checkout was removed after verifying
identical test-source bytes.

The publication source/test patch SHA-256 is
`7CA21E3E16797888FCDE67B054D688F4F9C674896F41DA58A2E2B6E113E76527`;
the complete production/test file-manifest SHA-256 is
`25CA9ABD66BC83AFD6350675E1D75C36756D3211336AB510260516F944DFC89C`.
Producer source and package contents did not change during this params-only
follow-up; the new source identity includes its additional test coverage.

### Review-batch execution evidence

The final reviewed source/test patch against
`dbd69abc4ad58e501f3d05e57ff86a649aaef592` has SHA-256
`59491AFE1570336B0F80A73E6075EF86B8D3BA5A48E51467B55AFEF8A005EB86`.
The comparison's identical retained tests and policy-precedence patch have
SHA-256 `C4D2914A7035D64B549913CF5719FFC61615ED077D6EF79C09F1882834582998`.
While validation was running, the PR received review-suggestion commit
`66a6cca9e15c8bdad3a2ecaa24ebc9d594fbdcf5`. It was fast-forwarded locally;
its overlapping functional changes were retained, and all final production/test
bytes were checked against the saved, tested tree before publication.

Release validation uses the pinned SDK and normal localization checks, without
the historical `ErrorOnOutOfDateXlf` override. The modern unit selection passes
195 adapter/helper/signature, 47 framework and 39 analyzer cases; the net462
adapter selection passes 193 cases. TRX, JUnit and CTRF agree on all 474
executed/passed cases, with zero skips. The independently planned 15-case packed
selection passes after the red experiment, also with matching reports and no
skips.

| Contract / named test | Pre-fix production with identical tests | Correct implementation |
| --- | --- | --- |
| Array identity and elements: `InvokeAsyncPreservesWrappedObjectArrayBeforeInjectingCancellationToken` | Executed: received `[[17, "array"]]` rather than the supplied `[17, "array"]`; fails on net8.0 and net462 | Executed: required/optional token, direct/stored arguments, empty/nonempty/nested arrays pass |
| Explicit-token tuple precedence: `TryHandleTupleDataSource_ExpandsExplicitTokenTuplesForObjectArgument`, `IsDataConsideredSingleArgumentValue_RejectsExplicitTokenTuplesForObjectArrayArgument`, `TryHandleITestDataRow_ExpandsExplicitTokenTuplesForObjectAndObjectArrayArguments` | Executed: three distinct expansion/shortcut assertions fail on both targets | Executed: raw/wrapped, reference/value tuples, explicit/default/null/missing tokens and intentional tuple-valued data pass |
| Empty-row generic inference: `WhenEmptyDataRowWithOptionalGenericDataBeforeCancellationToken_Diagnostic` | Executed: expected two unresolved-`T` diagnostics, actual zero | Executed: exact diagnostics pass; explicit generic data still produces no diagnostic |
| Packed composition: `Mtp_InjectsTokensWithoutChangingUserData`, `Vstest_InjectsTokensWithoutChangingUserData` | Executed: four outer failures, attributable to tuple discovery names and bad bound values; direct MTP execution reaches seven intended array/tuple failures in each metadata mode, and VSTest reaches the same seven failures | Executed: 37 reflection, 37 Rooting, 36 reflection-free and 37 VSTest positive child cases pass with exact TRX identities/outcomes |
| Params preservation: `OptionalParamsAndInvalidArgumentsProduceExactResults` | Executed: all five target-framework cases pass | Executed: net462 and net8.0-net11.0 cases pass, retaining exact array lengths/elements and intentional invalid-row failures |

The clean packed comparison executes 15 outer cases: 11 pass, four fail at
the intended behavioral assertions, none skip, and the parent exits 2.
Its MTP positive discovery commands exit 0 but report the wrong tuple names;
direct positive execution exits 2 with seven failed cases, while VSTest exits 1.
The correct implementation's parent exits 0. A first long-path comparison also
hit net462's deployment `MAX_PATH` limit; that unrelated failure is excluded.
Rerunning through an owned short-path junction removes it without changing the
tests, package payloads or production code.

Reproduction uses the existing unit selectors documented above, expanded to
include `TestDataSourceHelpersTests` in both adapter runs:

```powershell
.\.dotnet\dotnet.exe artifacts\bin\MSTestAdapter.PlatformServices.UnitTests\Release\net8.0\MSTestAdapter.PlatformServices.UnitTests.dll --treenode-filter "/*/*/(TestMethodInfoTests)|(MethodInfoExtensionsTests)|(TestDataSourceHelpersTests)/*"
.\artifacts\bin\MSTestAdapter.PlatformServices.UnitTests\Release\net462\MSTestAdapter.PlatformServices.UnitTests.exe --treenode-filter "/*/*/(TestMethodInfoTests)|(MethodInfoExtensionsTests)|(TestDataSourceHelpersTests)/*"
.\.dotnet\dotnet.exe build src\Analyzers\MSTest.GlobalConfigsGenerator\MSTest.GlobalConfigsGenerator.csproj -c Release -bl:generator.binlog
.\.dotnet\dotnet.exe pack src\Analyzers\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj -c Release -bl:analyzer-pack.binlog
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net11.0 --no-build --no-launch-profile --filter "ClassName=MSTest.Acceptance.IntegrationTests.TestMethodCancellationTokenTests|FullyQualifiedName~LegacyDataRowBehaviorTests.OptionalParamsAndInvalidArgumentsProduceExactResults" --minimum-expected-tests 15 -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\review-batch-acceptance-green-final -p:SkipUapTargetFramework=true -p:ModernUwpMinimum= -p:WinUiMinimum= -bl:review-acceptance.binlog --show-test-results all
```

Each command exits 0 for the correct implementation. Before acceptance, repack
the configuration-matched framework, adapter, source generator and SDK as in
the earlier validation, then rebuild the acceptance project. Discovery uses
the same filter with `--list-tests text` and finds the independently planned
15 identities. Reports are retained in `artifacts\TestResults\Release\review-batch-*`;
commands, binlogs, patch identities and physical payload records are in
`artifacts\tmp\cancellation-toolchain\review-batch`.

Both variants consume MSTest 4.6.0-dev and MTP 2.6.0-dev using fresh fixture-owned
NuGet caches. SHA-256 checks match the three actual consumer output layouts to
their respective NuGet payloads and their built producers. The shipping analyzer
DLL is likewise matched to its producer. The final green adapter package hash is
`829A9F97527AACEC9B4B0F30BA60CEF7C644E5798FA0A7FDCC14D3C507DEEE98`;
the pre-fix adapter package hash is
`F19A8FF2BB0F2256C89E53586C3A3942483856573FA3B2DB71FD5F1EB75D8023`.
The corresponding analyzer package hashes are
`289B5EDA89FFD437DAB7148B3A73EF584A92310031B6FA03520880A2FAE20B91`
and `6095F2568349105BA71B29DFA9F33B51AB1ED5D4BB5844045B30DB37ABF25EA0`.
Unchanged support packages needed by shared fixture initialization are copied
byte-for-byte into the isolated feed and separately hashed, not rebuilt as part
of an unrelated product validation claim.

Parallel-safety reconciliation keeps the internal TestContainer suites serial,
analyzer and acceptance assemblies method-parallel with CPU-count workers, and
the generated consumer at two workers. New arrays, tokens and assertion state
are per invocation/row; generated output variants are separate and read-only
during test execution. No scheduler, exclusion, lock, global lifetime or IPC
registration change is introduced. There is no file overlap with the separate
startup-registration slice.

### Additional analyzer array-shape review

The next Agent merge tick identified three related analyzer discrepancies against
`199d5dba631eb7fc67c89afa25174b4b6aa5f70a`: flattened typed arrays were accepted,
generic `T` bound to an array could crash analysis, and a singleton scalar bound
to `object[]` was rejected despite supported runtime binding.

Keep the pre-existing non-token diagnostic policy unchanged. When a token is
omitted, only `object[]` receives flattened values, including empty and singleton
rows; typed arrays require an actual supplied array. Generic `T` infers the whole
array type, while `T[]` infers its element type. Whole-array/scalar conflicts must
remain visible rather than merely suppressing the analyzer exception.

Add exact diagnostic regressions, a singleton invocation check and three packed
consumer cases with stable display names. Scheduling, token lifetime and
runtime binding behavior remain unchanged. The comparison retains identical tests
against the pre-fix analyzer; compilation of the test harness is not red evidence,
but an executed harness reporting the analyzer's AD0001 crash is the reported
product defect.

The packed fixture now explicitly enables analyzer execution instead of
inheriting the infrastructure's `RunAnalyzers=false` default. Its existing
intentional invalid-data file keeps its local MSTEST0014 suppression; valid
singleton and generic-array rows must compile with the actual shipping analyzer.
The first enabled run exposed MSTEST0057 on the fixture's existing derived
test-method attribute. Forward caller-file and caller-line constructor arguments
to the base attribute rather than suppressing that diagnostic; the setup failure
is not red-phase evidence.

#### Analyzer array-shape execution evidence

The tested source/test patch against
`199d5dba631eb7fc67c89afa25174b4b6aa5f70a` has SHA-256
`8936F35F6856E44598E56A964A098613C3937443C46D478201030223A28754FB`.
The identical analyzer regression file in both variants has SHA-256
`6BCCE429C73B9D429EAAEA57F6EF8BC8395D8296F5542197D972969F2EB48717`.

| Contract / named test | Pre-fix or targeted counterfactual | Correct implementation |
| --- | --- | --- |
| `WhenOmittedCancellationTokenFollowsTypedArray_RequiresArrayArgument` | Executed: expected three diagnostics, actual one; flattened and empty typed-array rows wrongly pass | Executed: exact count/type diagnostics pass; supplied arrays and existing non-token policy remain accepted |
| `WhenGenericDataBeforeCancellationTokenIsWholeArray_NoDiagnostic` | Executed: AD0001 with `InvalidCastException` instead of a valid analysis result | Executed: integer/string/object whole arrays and `T[]` element inference pass |
| `WhenGenericArrayDataHasConflictingTypes_Diagnostic` | Executed: the pre-fix analyzer crashes; a separate compiling control removing only the `MakeArrayType` block yields zero expected conflict diagnostics instead of two | Executed: both argument orders report the whole-array/scalar conflict; restoring the block returns all 42 cases to green |
| `WhenTrailingCancellationTokenFollowsSingleObjectArray_NoDiagnostic`, `InvokeAsyncPacksSingletonObjectArrayBeforeInjectingCancellationToken` | Executed: the pre-fix analyzer reports an unwanted MSTEST0014 for `[DataRow(7)]` | Executed: analysis accepts the row; required/optional and direct/stored invocations receive exactly `[7]` and the current context token |

The pre-fix run executes all 42 analyzer cases: 38 pass and four fail for the
reported defects, with no skips and exit 2. The inference-only control executes
42 cases with one intended assertion failure (expected two diagnostics, actual
zero), exit 2; its restored run executes/passes all 42, exit 0. These are executed
analyzer-harness assertions, not a failed test-project compilation.

Final focused validation passes 42 analyzer and 196 adapter/helper/signature
cases, plus all 15 packed acceptance cases, with exit 0 and no skips. Positive
children execute 40 reflection, 40 Rooting, 38 reflection-free and 40 VSTest cases,
including exact array values and `typeof(T) == typeof(int[])`. Reflection-free
generation still excludes the two unsupported generic methods. Existing retry,
invalid-data and five-target params-preservation cases remain green. Independently
planned identities agree with TRX, JUnit and CTRF.

Use the earlier unit/acceptance commands with the DataRow analyzer class filter
and the same 15-case acceptance selector. Build logs, exact commands, source
patch and control records are retained under
`artifacts\tmp\cancellation-toolchain\analyzer-arrays`; reports are under
`artifacts\TestResults\Release\analyzer-arrays-*` and
`review-batch-acceptance-analyzer-arrays-complete`. The packed fixture's build
binlog records `RunAnalyzers=true` with the pinned SDK.

The final shipping analyzer package SHA-256 is
`A9FF74A539533F899827B2D5200C720E83ADD153220ADEE5385FBE9CE112C0B1`;
its DLL payload, producer and unit-test copy share SHA-256
`76E838B5BBC165458B76D658E538BCCA1AF7D27B2A7BB9A5461C20971567CC7A`.
The unchanged framework/adapter products were also repacked after the current
checkout rebuilt their assembly metadata; all three consumer layouts now match
their packed payloads and built producers. Fresh fixture-owned caches and resolved
assets prove package selection. No runtime binding, lifecycle, lock or scheduler
behavior change is introduced.

A late CodeQL thread requested the same nullable `is true` pattern in
`TryHandleTupleDataSource`. Accept it without changing semantics: the identical
196-case adapter selection and 15 packed cases pass before and after that
one-line change. Its source is included in the final patch identity above.

## Open questions

The packed control is a compiling behavior-removal experiment, not a
pristine-baseline package run. The final restore/repack/rerun gate is green;
the owned comparison checkout and its borrowed-SDK junction have been removed.
Reports, binlogs, source manifests, package hashes and the failing control
package remain in task-owned evidence storage. No temporary mutation is present
in the delivered worktree.

The regular Windows Release solution test step selects this acceptance project
when `SkipTests=false`; these cases have no app-model category or conditional
attribute. No CI or release-branch execution has been performed by this session.
