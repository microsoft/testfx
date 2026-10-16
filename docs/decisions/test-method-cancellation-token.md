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
