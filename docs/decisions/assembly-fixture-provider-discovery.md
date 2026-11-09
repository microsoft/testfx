# Assembly fixture provider discovery

## Problem and contract

`AssemblyFixtureProviderAttribute` was introduced in MSTest 4.3.0
([#8677](https://github.com/microsoft/testfx/pull/8677),
[#757](https://github.com/microsoft/testfx/issues/757)). MSTest 4.4 added the
dynamic-code guard and MSTEST0072; it did not introduce shared assembly fixtures.

The original discovery implementation loads the consumer's transitive,
non-framework assembly references before checking for provider attributes.
Consequently, a library referenced only by an unexecuted method is loaded even
when no assembly declares a provider. On Windows/.NET Framework this also locks
the library's DLL. Reusing the consumer's load context avoids duplicate assembly
instances, not first-time loads.

The fix must preserve:

- Providers referenced in compiled metadata, including through unmarked
  intermediate libraries, without requiring the consumer to execute their code.
  An unused project/package reference that the compiler omits from metadata is
  still insufficient.
- Markers on the consumer itself, multiple markers, existing provider validation
  and duplicate-provider diagnostics, and one fixture invocation per consuming
  assembly.
- Local initialize and cleanup precedence independently. Both local hooks
  short-circuit provider discovery; a single hook leaves the other slot available.
- The runtime's assembly identity and load context when a provider is loaded.
- Existing unsupported-AOT behavior and MSTEST0072.

The supplying library opts into **providing** fixtures with the attribute.
Consuming assemblies do not separately opt into **discovery**. This fix does not
add a setting or change that compatibility contract.

The shipping boundary is `MSTest.TestAdapter` (which bundles
`MSTestAdapter.PlatformServices.dll`) plus the aligned `MSTest.TestFramework`,
under both VSTest and Microsoft.Testing.Platform. The regression is nonvisual:
the important outputs are loaded assembly identities, successful DLL deletion,
fixture execution, child exits, and TRX results. There is no new handshake,
cancellation protocol, reporter mapping, or UI.

## Research and experiments

### 2026-11-06: verify the source baseline

- The clean implementation worktree and refreshed `origin/main` both resolve to
  `da5f1226964da20a8882500f5f26baa8c309036b`.
- `origin/rel/4.5` resolves to
  `72fd2b4b64ea00b275e0c18b1aeb36159348c1c7`. Its
  `TypeCache.ProviderDiscovery.cs` is identical to main's.
- `CreateTestAssemblyInfo` first discovers local hooks, then calls
  `DiscoverFixturesFromProviders`. `EnumerateCandidateAssemblies` currently
  loads every traversed reference. `HasAssemblyFixtureProviderMarker` runs only
  after that load.
- Existing package-consuming coverage executes a provider's methods, but the
  test body also calls the provider library. It does not assert passive loading,
  transitive unmarked intermediaries, or file-lock behavior.
- The supplied public-package reproduction established no-provider unloading and
  deletability with 4.2.3, the regression in 4.3.0 and 4.5.2, and the difference
  between initialize-only and both-local-hooks cases. Those are historical
  investigation results, not runs of this fix.

### 2026-11-06: reject component-path guesses as binding authority

The first metadata prototype passed 57 package-consuming cases and 37 unit
cases. Its paired original-discovery run executed the same 57 cases: 12
loading/deletion assertions and four transitive-intermediary assertions failed
for the intended behavior. Five compatibility cases reached passing child
provider/state/event assertions but failed the newly required fallback trace.
All eight duplicate-provider cases passed in both states: VSTest exits 1 and
native MTP exits 7, with UTA013/UTA014 and a failed, zero-executed-result child
TRX. Those errors are not successful zero-work execution.

The creating session then demonstrated an additional valid binding case: a
later-loaded consumer's component `AssemblyDependencyResolver` finds an
adjacent, matching-identity unmarked v1, while Default's `Resolving` handler
selects a marked v2 elsewhere without preloading it. Thus neither a component
resolver nor an adjacent matching-identity file proves Default's binding.
The prototype adapter package is retained for a targeted regression run of this
case; its 57-pass result is not final validation of the corrected design.

Definitive negatives must use actual runtime binding data: Default's trusted
platform assembly (TPA) map or a known loaded ALC binding, and conservative
neutral, unsigned default probing on .NET Framework. Strong-named Framework
references can bind from the GAC before an adjacent file; non-default binding
therefore remains on the diagnosed runtime fallback path.

## Alternatives and decision

| Approach | Decision |
| --- | --- |
| Keep eager discovery and recommend empty local hooks | Rejected: leaves the regression in place and makes a workaround look like mandatory setup. |
| Inspect only already-loaded assemblies | Rejected: silently loses passively referenced and transitive providers. |
| Require consumer registration or discovery opt-in | Rejected for this fix: breaks the existing automatic-discovery contract and complicates servicing. |
| Add only a discovery opt-out | Rejected as the primary fix: leaves the surprising default and forces consumers to discover and configure a mitigation. |
| Read PE metadata before loading references | Chosen: traverse references without loading unmarked libraries; use normal runtime resolution for actual providers. |

Use the maintained `System.Reflection.Metadata` reader rather than a custom PE
parser or a second reflection-only execution context. Dispose metadata readers
and streams before running user code, with read/write/delete file sharing.
Preserve breadth-first traversal and full-identity deduplication.

Resolve metadata paths from the runtime's actual binding data, not a component
dependency resolver or the directory of a dynamically loaded consumer. When a
reference cannot be inspected safely as a file
(for example an in-memory/single-file assembly or an opaque custom resolver),
retain runtime resolution with a trace diagnostic rather than silently dropping
potential providers. This compatibility fallback can still load an unmarked
assembly; the metadata fast path must not claim otherwise. Provider loading
continues through the referrer's load context, not a new/default context.

These are implementation decisions, not a claim that a fixed package has
already shipped.

## Validation and demonstration

### Final implementation and package provenance

The final implementation uses Default's TPA map, not component ADR or neighboring
files. An already-loaded same-simple-name candidate or a custom ALC is resolved
at runtime, avoiding both version-unification and stale on-disk-image assumptions.
Framework metadata negatives are restricted to neutral unsigned application-base
probing without redirects, `codeBase`, in-memory configuration, GAC/strong-name,
culture, or host probing overrides. Other cases preserve runtime resolution.
UWP keeps that conservative runtime path.

The tested source state is base
`da5f1226964da20a8882500f5f26baa8c309036b` plus behavioral patch SHA-256
`4139707EBBB48D7B168B34343E3CBCD7A5381BC428CD9C78CA70DD103ED97C28`.
The original-discovery comparison retains identical tests and support/dependency
changes, restores `TypeCache.ProviderDiscovery.cs` exactly to that base, and
removes the new metadata helper. Its behavior-removal patch SHA-256 is
`AB19CE5B773C77D29E6512A141CB465A912EBA7BADCA858A7B8AB3D7FF80A46C`.
This is a targeted counterfactual, not a pristine baseline build.

Windows x64 validation used the pinned SDK
`12.0.100-alpha.1.26509.101`, a net12.0 outer acceptance host, and real net472
(.NET Framework 4.8.10008.0) and net11.0
(`11.0.0-rc.3.26522.148`) product hosts. Existing provider coverage additionally
ran net10.0 and net462. VSTest CLI reported
`18.13.0-preview-26509-101`; VSTest assets reference `Microsoft.NET.Test.Sdk`
18.10.1. Native MTP assets assert that package is absent from resolved assets.

| Shipping package | Final SHA-256 |
| --- | --- |
| MSTest.TestAdapter 5.0.0-dev | `A6C08E128FE0AE15AF6ED147D9340A44F155B7B7476A19A92EFD83E499676DBE` |
| MSTest.TestFramework 5.0.0-dev | `667830B8ADB30DB2388F0741D341677E4F2D027CEAB2E0DA0E59CF889AF229E5` |
| Microsoft.Testing.Platform 3.0.0-dev | `F32C71835A5001CD55F3F94E81F1CDA77EC6E09F698306A4EA3AF4F5042DC970` |
| Microsoft.Testing.Extensions.TrxReport 3.0.0-dev | `F33DD0E5EA6000ED97C804627B73FE2FAA016E2B1D59CD40BD1376C161954471` |

The original-discovery adapter package has SHA-256
`EB505AFED45E3B8FDE1C41EA9DFA64CB1E6B567C2DA2CFBD59580C09C9398AD5`.
Framework, MTP and TRX packages are byte-identical across the paired runs.
Each generated consumer checks the restored PlatformServices payload against the
packed DLL and, for executing children, checks the loaded DLL hash too. Resolved
assets, feed configuration and child TRX files are retained as result attachments.

### Executed requirements

| Requirement | Evidence |
| --- | --- |
| Unmarked DLLs remain unloaded and deletable under all four local-hook combinations | `UnmarkedDependenciesRemainUnloadedAndDeletable`: 16 cases across net472/net11 and VSTest/native MTP. Both the direct library and its unused transitive leaf are checked. |
| Passive direct/transitive providers and consumer markers work | `PassiveProvidersAreDiscoveredWithoutExecutingReferenceMethods`: 12 cases; ordered fixture events and the initialized state are asserted without calling reference methods. |
| Local slots retain independent precedence | `LocalFixturesOverrideOnlyTheirOwnProviderSlot`: 12 cases. |
| Duplicate providers retain errors rather than becoming successful zero-work runs | `DuplicateProviderSlotsRetainTheirDiagnostics`: eight cases, UTA013/UTA014, VSTest exit 1 / MTP exit 7 and failed zero-result child reports; identical in the original implementation. |
| Binding and load-context identity are preserved | `CustomLoadContextUsesItsProviderInsteadOfAnUnmarkedSameIdentityFile`, `LoadedHigherVersionProviderIsNotHiddenByUnmarkedRequestedVersion`, `DeferredDefaultResolutionDoesNotHideProviderBehindAnUnmarkedComponentFile`, `FrameworkBindingRedirectDoesNotTreatWrongVersionAsUnmarked` (both hosts), and `FrameworkLoadFileDecoyDoesNotHideTheApplicationBaseProvider`. |
| Opaque resolution remains an explicit runtime-load fallback | `OpaqueLoadContextRetainsRuntimeFallbackForAnUnmarkedReference`: the unmarked image is loaded in the custom context, no provider hooks run, and the diagnostic is asserted. |
| AOT and existing providers remain compatible | `AssemblyFixtureProvider_WhenDynamicCodeUnsupported_IsSkipped` and the three existing `AssemblyFixtureProvider_FromReferencedLibrary_RunsAssemblyInitializeAndCleanup` target-framework cases. |

The fixed selection independently discovers, selects, executes and passes all
**59** acceptance cases, with zero skipped cases and outer exit **0**. Focused
`TypeCacheAssemblyFixtureProviderTests` pass **18/18** on net10.0 and **19/19** on
net462. The tests preserve method-level acceptance scheduling; every generated
asset, child process, report and mutable file is per case. No assembly scheduling
or process-global test state was changed.

### Test-value evidence

The identical 59-case original-discovery selection executes with outer exit
**2**, **36 passed**, **23 failed**, and zero skipped:

- Twelve no-provider cases fail the intended `Assert.IsFalse(loaded)` assertion.
  Their child reports show the unused dependency loaded; Framework reports also
  contain the actual `UnauthorizedAccessException` from attempted DLL deletion.
  The four both-local-hooks cases remain green.
- Four transitive-provider cases execute their fixtures but fail because the
  unmarked intermediary was loaded.
- Seven binding/fallback cases have passing child results, correct provider
  state and ordered events, then fail only the new fallback-trace assertion.
  These are diagnostic detections, not claims that pre-fix binding was broken.
- All eight duplicate-provider error cases pass unchanged in both states.

A separate check uses the rejected component-probe adapter package (SHA-256
`EC04A5825278665E720EAABF7DA8FE2E547FD37EFDBEDFE837B4CB68B1E8E443`).
`DeferredDefaultResolutionDoesNotHideProviderBehindAnUnmarkedComponentFile`
executes its child once and fails at
`The passive provider must be loaded before the test starts` (child exit 2),
not compilation, setup or discovery. The Framework LoadFile case passes in that
same two-case run. The final implementation passes both.

### Reproduction commands and retained evidence

Run from the checkout root with the repository-local toolchain. The complete
Windows pack, including classic UWP content and package-metadata validation, ran:

```powershell
.\build.cmd -pack -c Release -bl
```

Result: **exit 0, zero warnings/errors**. The execution environment needed a
process-local Windows PowerShell path for the wrapper's child commands; no global
environment was changed.

The fixed and original-discovery acceptance runs use the same arguments:

```powershell
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$filter = 'FullyQualifiedName~AssemblyFixtureProvider'
$results = Join-Path (Get-Location) 'artifacts\TestResults\Release\fixture-authority-green'
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --no-launch-profile `
    --list-tests text --filter $filter -p:UsingDotNetTest=true `
    -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= `
    "-bl:$results\discovery.binlog"
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --no-launch-profile `
    --no-artifact-post-processing --filter $filter -p:UsingDotNetTest=true `
    "-p:ArtifactsTestResultsDir=$results" "-bl:$results\execution.binlog" --show-test-results all
```

In the comparison checkout, use the `fixture-authority-red` results leaf with the
same project, filter and arguments. Discovery exits **0** with the same 59 identities in both variants. Execution
exits **0** fixed and **2** original, with the attributed results above. The
repository acceptance verifier confirms all 59 planned fixed results are fresh,
executed and passed. Nested child reports retain their exact bytes with a
`.trx.xml` attachment suffix so they are not merged as outer modules.

The focused unit commands use
`--no-ansi --progress off --treenode-filter '/*/*/TypeCacheAssemblyFixtureProviderTests/*'`
and `--report-trx` with separate results directories:

```powershell
.\.dotnet\dotnet.exe artifacts\bin\MSTestAdapter.PlatformServices.UnitTests\Release\net10.0\MSTestAdapter.PlatformServices.UnitTests.dll --no-ansi --progress off --treenode-filter '/*/*/TypeCacheAssemblyFixtureProviderTests/*' --report-trx --report-trx-filename unit-net10.trx --results-directory artifacts\TestResults\Release\fixture-authority-unit\net10
.\artifacts\bin\MSTestAdapter.PlatformServices.UnitTests\Release\net462\MSTestAdapter.PlatformServices.UnitTests.exe --no-ansi --progress off --treenode-filter '/*/*/TypeCacheAssemblyFixtureProviderTests/*' --report-trx --report-trx-filename unit-net462.trx --results-directory artifacts\TestResults\Release\fixture-authority-unit\net462
```

Both exit **0**, with 18 and 19 executed/passed cases respectively.

Local evidence is retained under `artifacts\TestResults\Release\fixture-authority-green`,
`fixture-authority-unit`, and `artifacts\fixture-evidence`. The task-owned sibling
comparison retains `fixture-authority-red` and `unsafe-component-probe` results.
These logs are not uploaded; this versioned record carries the decisive commands,
hashes, counts and assertion outcomes for reviewers. The unchanged product has no
new rendered surface, so screenshots are inapplicable. CI, hosted IDE rendering,
UWP activation and real GAC installation were not executed.

## Servicing and ownership

### 2026-11-09: 4.5 servicing implementation

The contextual backport starts at release commit
`a39964ce2cf09c4abcbaa7bb8f4e5818638a9de3`, using the production correction
and regression cases from main commit
`127798de9bd5c3f7204dc8932cddf043bf4bd6c6` ([#12269](https://github.com/microsoft/testfx/pull/12269)).
The release retains its .NET 8/.NET 9, .NET Framework, classic/modern UWP and
WinUI assets. `System.Reflection.Metadata` 8.0.0 remains explicit for legacy
targets; PlatformServices' existing optional .NET Standard dependency
conditions also cover the reader. The cached dictionary uses syntax supported
by the release's pinned SDK rather than upgrading the toolchain.

The same loading/binding cases additionally execute against the lowest modern
release target, .NET 8. Package-provenance assertions map the .NET 10 consumer
to the release's selected .NET 9 adapter asset instead of assuming that the
consumer TFM is itself packed. The main-branch validation recorded above is
historical evidence, not proof of this backport.

Windows x64 release execution used SDK `12.0.100-alpha.1.26480.103`,
`MSTest.TestAdapter`/`MSTest.TestFramework` `4.5.3-dev`, and MTP `2.5.3-dev`.
The Framework and adapter producers packed every declared release target with
zero warnings/errors; the acceptance host compiled for net11.0 using the
repository's pinned runtime. Independent discovery and the source-derived
plan both selected 82 cases. All 82 executed and passed, with zero skips,
including real MTP/VSTest net8.0, net10.0 and Framework consumers. Every new
consumer compared its restored and loaded PlatformServices DLL with the
current adapter nupkg payload. That nupkg's SHA-256 was
`142C2CA401722AF36FB48D043E3720693CB6B192960008EE6175E854ECCE703B`.

The isolated counterfactual retains the same test bytes, reader, dependency
wiring and runtime, but restores eager runtime loading when metadata identifies
an unmarked reference. Its full retained patch SHA-256 is
`17C6B57D3E9006C594B17E0021ACDF15AB239068C4251779CDE67E688D58C395`.
The same 82 cases executed: 24 loading/transitive-intermediary assertions
failed for the intended defect, 58 compatibility controls passed, no cases
were skipped, and the outer exit was 2. The correct source patch tested green
was `D12DFAA285D11545D0B3658D43E7ED66C51A4C651DE7BC56F6C7F56EEF128FBA`.
The 13 existing `TypeCacheAssemblyFixtureProviderTests` controls also passed
on net8.0, net9.0, net462, net48 and the WinUI target: 65 executed, zero failures
or skips.

The executed commands use separately checked, GUID-named `$binaryLog` files;
the Windows build wrapper does not preserve a literal `{}` placeholder:

```powershell
.\build.cmd -pack -c Release -projects src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj -msbuildEngine vs "/bl:$binaryLog"
.\build.cmd -pack -c Release -projects src\TestFramework\TestFramework.Extensions\TestFramework.Extensions.csproj -msbuildEngine vs "/bl:$binaryLog"
.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net11.0 -p:EnableCodeCoverage=false "/bl:$binaryLog"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net11.0\MSTest.Acceptance.IntegrationTests.dll --list-tests --filter 'FullyQualifiedName~AssemblyFixtureProviderDiscoveryTests|FullyQualifiedName~AssemblyFixtureProviderTests' --no-ansi --progress off
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net11.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~AssemblyFixtureProviderDiscoveryTests|FullyQualifiedName~AssemblyFixtureProviderTests' --report-trx --report-trx-filename fixture-green.trx --results-directory $results --no-ansi --progress off --show-test-results failed
.\build.cmd -c Release -projects test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -msbuildEngine vs /p:EnableCodeCoverage=false "/bl:$binaryLog"
```

The counterfactual uses the same acceptance command/selector with its own
root, packages and results destination. Its initial long artifact path caused
an unrelated Framework deployment `PathTooLongException`; the clean paired
run used a short, task-owned sibling worktree with byte-identical built
payloads. That setup failure, and an earlier missing NonShipping feed, are
not counted as regression proof. Raw TRX, commands, binlogs, expected identities
and package-resolution/loaded-binary evidence are retained in session artifacts.
No released fixed version, hosted IDE/UWP activation, GitHub publication or CI
execution is implied.

The regression is confirmed in public versions 4.3.0 and 4.5.2 and remains
present in the inspected 4.5 release branch. A compatible fix is a candidate for
4.5 servicing; a backport is recommended. No fixed 4.5 version is assigned here: a release owner must accept
the backport and validate the release branch's actual package layout and targets
and aligned MTP 2.x packages before public documentation names a serviced version.
The older release targets, including netstandard2.0 where applicable, need their
own metadata dependency conditions rather than a blind copy of main's project
files. No backport, push, PR publication or CI result is implied. The implementation owner
remains responsible for the code, regression evidence, and aligned documentation
handoff; merging main alone is not proof of a serviced package.
