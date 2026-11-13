# MSTest SDK composition: layer 3

## Problem and contract

This layer replaces the SDK/helper/composition portion of
[#12435](https://github.com/microsoft/testfx/pull/12435), following
[#7225](https://github.com/microsoft/testfx/issues/7225),
[#12412](https://github.com/microsoft/testfx/pull/12412),
[#12428](https://github.com/microsoft/testfx/pull/12428), the
[shared-core layer](shared-core-ui-contract.md)
([#12446](https://github.com/microsoft/testfx/pull/12446)), and the
[explicit-package layer](explicit-mstest-packages.md)
([#12450](https://github.com/microsoft/testfx/pull/12450)).

`MSTestApplicationModel` accepts `Default`, `Uwp`, or `WinUI`. An empty value
preserves automatic inference, including early UWP inference and explicit
`UseUwpTools=false`. Selection is recomputed for each inner target framework;
the public property is never filled with an inferred outer-build value.
The override selects packages, not UI tooling. Invalid or conflicting choices
fail explicitly. ClassicEngine, NativeAOT, VSTest, helpers, and central package
management use the same selected composition.

Helpers retain required compile-time UI APIs and generated runtime hooks, but
their adapter references are private, exclude runtime assets, suppress manual
copy-local payloads and satellites, and do not register the MSTest application
builder hook. The stable hook GUID/order and unrelated MTP dependency hooks
remain intact. Applications own their selected runtime payload.

Adapter validation uses `ResolvePackageAssets` and `PackageDependencies` for
the current inner TFM, including ProjectReference-transitive packages whose
build assets were excluded. Non-SDK projects retain imported-marker checks.
All three conflicting pairs fail; separate ordinary/UI inner TFMs remain valid.
Exact UI/core ranges and package/runtime ownership belong to lower layers and
are unchanged here.

The producers are the aligned Release `MSTest.Sdk`, `MSTest.TestAdapter`,
`MSTest.Uwp`, and `MSTest.WinUI` packages. Generated SDK and direct-package
consumers exercise their real packed imports, resolved assemblies, copied
payloads, and helper nuspecs. MTP launch/activation, handshake/no-handshake,
cancellation, fallback, exit aggregation, and disposal responsibilities do not
change. Native/Bazel parity and real app activation are explicitly outside this
layer. The series coordinator owns rollout and release-branch/backport decisions;
this layer does not enable a downstream or servicing release.

## Research and decision

2026-11-13: the clean parent is
`f9708f5edbd37215b7e53cef840ef1e29ddf3f93`. The frozen source ref and live
#12435 head are `00428d48c212cf3630dcf6fa31d131c5ae745c98`; the carving input is
`8dd78e1347417e400c3ef8d786059a8bb346162f...refs/pr-stack/source/12435`.
Reconcile that bounded slice with the parent rather than replacing whole files.

No change would retain leaking source-generated helper dependencies and allow
ambiguous adapter packages. Checking only imported markers is simpler, but misses
excluded build assets and transitive packages. Scanning every assets target
would falsely reject valid multi-targeting. The chosen package-task output
already models the current inner target and avoids a new assets parser.
Moving only MSTest's hook registration from props to targets lets the project
body's `IsTestApplication=false` participate without removing dependency hooks.

## Validation and demonstration

| Requirement | Automated evidence | Surface / limitation |
| --- | --- | --- |
| Explicit, inferred, invalid, and conflicting models | `MSTestSdk_ExplicitApplicationModel_SelectsCompositionBeforeLateToolingImports`, `MSTestSdk_UwpToolingOptOut_RespectsExplicitApplicationModel`, `MSTestSdk_ApplicationModel_RejectsInvalidOrConflictingChoices` | Real packed SDK imports and resolved package/reference identity |
| Every injection path and CPM | `MSTestSdk_AdapterInjection_UsesSelectedCompositionWithCentralPackageManagement`, `MSTestSdk_SourceGenerationOptIn_LibraryBuilds`, `MSTestSdk_SourceGenerationOptIn_NativeAotLibraryBuilds`, `MSTestSdk_SourceGenerationOptIn_VSTestTakesPrecedenceOverNativeAot` | Package selection, generated hooks, and real ordinary execution |
| Helpers do not contaminate applications | `MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition`, `MSTestSdk_UiApplication_WithOrdinarySourceGeneratedHelper_DoesNotResolveOrdinaryAdapter`, `MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively` | Compile, copy-local/output inspection, copied hash identity, helper nuspec |
| Conflicts and valid multi-TFM closure | `ExplicitPackages_RejectConflictingAdaptersBeforeCompilation`, `MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework` | Direct/excluded/ProjectReference-transitive pairs and independent inner closures |
| Classic UWP and ordinary net462 compatibility | `RunTests_With_VSTest`, `RunTests_With_MSTestRunner_Standalone`, `RunTests_With_CentralPackageManagement_Standalone`, `MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition` | Six Debug/Release multi-TFM cases run net11.0, net10.0, and net462; classic composition uses desktop MSBuild, not activation |
| Non-SDK imported-marker fallback | Six packed-import `non-sdk-composition.proj` probes | All three single compositions exit 0; all three conflicting pairs exit 1 without SDK package-task outputs |

The acceptance assembly uses method-level, CPU-count parallelization. Every new
case generates a fresh asset directory; shared package inputs are immutable
during a run and the assembly fixture chooses an isolated cache. No test
scheduling declarations change. This build-policy layer is nonvisual; diagnostic
and resolved-asset transcripts are more useful than screenshots.

### Revision, toolchain, and package identity

Executed on 2026-11-13 in this layer's worktree. The tested shipping/test source
checkpoint is `17b8abac7c8959e73413e01f2c92b0998b590551`, built from the parent
plus retained patch SHA-256
`979C1B1DA2D38DFD63592CBC24D64FCAF44D9E18B8A41F967C4358C9079192BE`
before committing that checkpoint. Later evidence-only changes do not change
shipping files or tests. Historical unsplit-source runs are not counted.
Before publication, parent evidence commit
`9d2bc45c1a2603ad0ff908504f8d00bbf95ceb47` was fetched and merged.
Its only change is the lower layer's decision record; shipping/test source
still matches the tested checkpoint byte-for-byte.

The pinned SDK is `12.0.100-alpha.1.26556.115`; desktop MSBuild reports
`18.13.0.52701`, and SDK MSBuild reports `18.14.0-1.26556.115`.
The outer acceptance host targets `net12.0`; generated consumers retain their
declared net10.0/net11.0/net12.0, Windows, net462, and classic UAP targets.
The build script bootstrapped a task-owned `.dotnet`; the borrowed worktree and
its outputs were not modified. Producer dependencies reuse
`Q:\.tools\.nuget\packages` without copying or clearing it. Consumers use fresh
marked, task-owned caches, not that shared same-version SDK cache.

All four compared packages are `5.0.0-dev`; the MTP platform/reporting family
uses `3.0.0-dev`.
Cached archives matched the corresponding packed archives byte-for-byte:

| Package | Parent SHA-256 | Current SHA-256 |
| --- | --- | --- |
| MSTest.Sdk | `242229B6802F52613D60B6D69B9292F1436265BEFFB0FED25897B29DFF147A08` | `6C6411E3C2C8CF7B2B181EBABF3C3517F2796CC75679DE2685BE7B07780C8A91` |
| MSTest.TestAdapter | `424DC27A56BD9A990F077A07E92E1F631D80141030F73EDDD7A418F0AF15866E` | `00362CA19A3C7EDC3A216CE3361E0145BE46FA8DACDB3BCD41BDBE145D694B6F` |
| MSTest.Uwp | `DEDE4E3FAD8CA3EE2A216168F502440A2D538841F32F151760C068BF30F3B086` | `43E0E9809DF14A6F231C2CA31DF421393DF507C1A8F9898C406C8156A468A496` |
| MSTest.WinUI | `5A3D2FC98C4567A56D26354DD3534D5EF53828CF984AD0A1600457D33D47E99F` | `C06756B10CCF8044A6337287E2378BAAFAA4F6AA9752B577757FE85643B4C1F3` |

Current cached core and source-generator archive hashes are
`1A7AFD797D0D81AC1127F0AED3872BA83C96A21D2E0CFB405265D6B4DBD53C72`
and `C817DDFDF1A76464C3F025C608EBBD1DB9986098AF5984EF8E29AB644C1C4EF7`.
Physical UI nuspecs retain `[5.0.0-dev]` core dependencies for both UWP groups
and WinUI, with no ordinary/other-UI adapter dependency.

The 73 generated main-run assets files resolve to `cache-green-main`.
Six actual net462 SDK consumers copied 18 core/adapter/services DLLs matching
the current cache hashes. UI-with-helper cases independently compare both copied
adapter/services DLLs with the selected UI package. Adapter-referencing helpers
retain the unrelated
`8214564B-6107-422D-8A82-462F97602287` and
`98058041-B5B6-4A75-9834-58E6DF796A22` hooks while omitting MSTest's GUID.

### Commands and execution results

The pack filters contain actual `TestFx.slnx` source entries, not `src\**`:
42 source projects for the parent; the same entries plus
`test/IntegrationTests/MSTest.Acceptance.IntegrationTests/MSTest.Acceptance.IntegrationTests.csproj`
for current source and its test owner. Both commands exited **0**, with
**0 warnings and 0 errors**, and passed package-content/metadata verification:

```powershell
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer3-pack.slnf -bl
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer3-delivery.slnf -bl
```

The focused selector is reproducible without selecting the unrelated SDK suite:

```powershell
$env:DOTNET_ROOT = Join-Path $PWD '.dotnet'
$env:PATH = "$env:DOTNET_ROOT;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Windows\System32;C:\Windows;C:\Program Files\Git\cmd"
$env:NUGET_PACKAGES = 'Q:\.tools\.nuget\packages'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:MSBUILDUSESERVER = '0'
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$selected = @(
  'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.SdkTests.MSTestSdk_'
  'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.SdkTests.ExplicitPackages_'
  'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.SettingIsTestApplicationToFalseReducesAddedExtensionsAndMakesProjectNotExecutable'
  'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.TestApplicationRunsTestsFromReferencedMSTestSdkTestLibrary'
  'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_CentralPackageManagement_Standalone'
  'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_VSTest'
  'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_MSTestRunner_Standalone'
  'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.SourceGenerationNonAotTests.MSTestSdk_SourceGenerationOptIn_'
)
$filter = '(' + ($selected -join '|') + ')&FullyQualifiedName!~MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition'
$results = Join-Path $PWD 'artifacts\validation\green-main'
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --list-tests json --filter $filter -p:UsingDotNetTest=true '-p:TestingPlatformCommandLineArguments=' '-p:TestRunnerAdditionalArguments=' -bl:artifacts\validation\focused-discovery-json.binlog
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" --show-test-results failed
```

Discovery exited **0**, selecting the independently source-counted **76** cases.
Execution exited **0**: **76 executed/passed, 0 failed, 0 skipped**.
TRX, JUnit, and CTRF each contain the same 76 cases. The classic build-only case
was then selected alone, with `TESTFX_RUN_WINDOWS_APP_MODEL_TESTS=1` scoped to
that command; it does not activate or register an application:

```powershell
$filter = 'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition'
$results = Join-Path $PWD 'artifacts\validation\green-classic'
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --list-tests json --filter $filter -p:UsingDotNetTest=true '-p:TestingPlatformCommandLineArguments=' '-p:TestRunnerAdditionalArguments=' "-bl:$results\discovery.binlog"
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" --show-test-results failed
```

Both classic commands exited **0**; **1 executed/passed, 0 failed/skipped**.
The total is **77 distinct passed cases**, not a sum including repeated runs.
The existing execution verifier independently reconciled expected names, fresh
start times, results/counters, and exits for every green run:

```powershell
python .github\skills\testfx-acceptance-validation\scripts\verify_execution.py --trx $trx --expected-tests $expected --started-after $started --process-exit-code 0
```

### Test-value evidence

Retain the same acceptance DLL and test bytes while switching only the four
owned package archives between the separately packed parent/current variants.
Use a fresh cache per variant; restore current archives in `finally`.
No shared cache or source file is modified. The retained acceptance DLL SHA-256
is `06C75172A043C7CC33D372D4BE48B7E98E07E5729A8351DF460BDA0A8DC01283`;
`SdkTests.Composition.cs` is
`6D791487469B9964947013325B3603196530E7EB860C3C498283133437346202`,
and `SourceGenerationNonAotTests.cs` is
`424479BCBF65B6CEC3F74DDF6189711DAE1C30F5B181863F68C0AAFD33307343`.
All remained unchanged through the paired runs.

| Contract / exact case | Parent packages | Current packages | Evidence |
| --- | --- | --- | --- |
| `ExplicitPackages_RejectConflictingAdaptersBeforeCompilation`: all three pairs, each direct/excluded/ProjectReference-transitive | 9 executed failures | Same 9 passed | Successful child restore/prepare exits 0 instead of required 1; not compiler/setup failure |
| `MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition` (`Default`, true, `MSTest.TestAdapter`) | Executed failure | Passed | `Assert.DoesNotContain(IsMSTestApplicationPayload, copyLocal)` detects copied adapter DLL |
| `MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively` | Executed failure | Passed | `Assert.DoesNotContain("MSTest.TestAdapter", dependencyIds)` detects the helper nuspec leak |
| `MSTestSdk_ExplicitApplicationModel_SelectsCompositionBeforeLateToolingImports`: three models with/without CPM | 6 executed failures | Same 6 passed | Resolved package composition differs from the independent expected package |
| `MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework`: Uwp/WinUI with/without CPM | 4 executed failures | Same 4 passed | Parent builds successfully but lacks the model-report contract; current also verifies both inner package closures and reference identities |
| `RunTests_With_VSTest`, `RunTests_With_MSTestRunner_Standalone`, `RunTests_With_CentralPackageManagement_Standalone`: Debug/Release, net11.0/net10.0/net462 | 6 passed | Same 6 passed | Green/green preservation; real selected tests execute in every child TFM |

The first red/green cohort selected these exact 11 discovered UIDs:

```powershell
$uids = @(
  '17668ca7-783b-83c3-b529-ac85934e07e2', '19a9953e-2c93-8cf5-b659-5e692539ef3d',
  '14c52600-0d9c-81d4-ab4c-bb0e8702e6ff', '1423aa72-f6ee-8347-a8a9-3f59c4c59d09',
  '1228346b-3f3a-8da2-bc8a-60f3867b17c4', '1a780178-0a66-828c-b405-3c105865a756',
  '17005f89-c56d-89ef-854d-8efcd63b2f03', '14040bf7-34a6-8a49-b4ee-105846b6e0f1',
  '1c63aad6-a924-8f4c-9f8b-6e9a8c60c7e6', '1730c601-a215-8f51-99d3-f729b8cb4a16',
  '18d95242-f2c4-8e6b-8b98-b04cedc389c8'
)
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --filter-uid @uids -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" --show-test-results failed
```

With `$results` set to `red-parent`, outer exit **2** with **11 intended failed
assertions, 0 passed/skipped**. At `green-sensitivity`, exit **0**, **11 passed**.
The second red/green cohort uses the identical command with these ten UIDs:

```powershell
$uids = @(
  '14c2cf97-6c42-835f-af0c-7270c819ab74', '1a368880-1396-83e3-9568-270d21684cb0',
  '1f7126c2-4c4e-8445-b29b-c1dfd2d37d70', '1636c572-30c2-8537-b0b9-4a3950965bd9',
  '15497b59-635d-8cd3-bce2-4f9430ca2d36', '16720a6f-2a87-8ae6-9643-42d11a36403e',
  '19f60bac-9af5-88ad-acec-a29cd7b2c020', '1cb252d1-21a7-8021-8101-5968f7b9846f',
  '1a06de47-710d-8f42-9387-0c36ad072bc6', '1e1d0a65-01ba-857c-ad94-a93029268516'
)
```

At `red-models`, exit **2**, **10 intended failed assertions, 0 passed/skipped**.
At `green-models`, exit **0**, **10 passed**. Parent WinUI model cases do not
resolve `MSTest.WinUI`, as expected for the ignored override.
The compatibility command is:

```powershell
$filter = 'FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_CentralPackageManagement_Standalone|FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_MSTestRunner_Standalone|FullyQualifiedName=MSTest.Acceptance.IntegrationTests.SdkTests.RunTests_With_VSTest'
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" --show-test-results failed
```

Both `compat-parent` and
`compat-current` exited **0**, **6 passed, 0 failed/skipped**. This is
**21 executed detections plus 6 green/green preservation cases**, not mutation
coverage for every possible diagnostic or fault.

Six additional desktop-MSBuild packed-import probes used
`non-sdk-composition.proj -t:CheckComposition -nologo -nr:false
-p:FirstId=<package> -p:FirstTfm=<packed TFM> -bl:<case>.binlog`, adding
`SecondId`/`SecondTfm` for pairs. UsingMicrosoftNETSdk and PackageDependencies
are empty: all three valid singles exit **0**, all three pairs exit **1** at
the imported-marker diagnostic, including the classic UWP target.

Local `artifacts\validation` retains pack logs/binlogs, each named run's TRX and
commands, expected/discovered identities, hashes, per-run caches, and assertion
excerpts. The current feed was restored byte-for-byte after every comparison.
The versioned transcript and hashes above are reviewer-accessible evidence;
raw local logs are not published CI artifacts. An initial discovery invocation
omitted SDK 12's explicit `json`/`text` argument and failed before tests;
it was corrected and is not red-phase proof. Provenance probes were corrected
to use NuGet's actual `net462` target key and to expect absent WinUI in the
parent-model cache; these were verification-script issues, not product failures.

## Open questions

CI execution and release-branch validation are not established by these local
runs. Actual UWP/WinUI activation remains layer 2's contract; native/Bazel parity
remains layer 4's. No global developer-mode/registration settings were changed.
No separately installed/shared packages were removed. The unrelated native
net462 System.Reflection.Metadata baseline remains untouched.
