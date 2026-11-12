# Explicit MSTest Windows application-model packages

## Problem and contract

This is the package stage of [#7225](https://github.com/microsoft/testfx/issues/7225),
after [#12412](https://github.com/microsoft/testfx/pull/12412) and
[#12428](https://github.com/microsoft/testfx/pull/12428). The starting point is
merged main `e1c66e5cbfa844384a59343c3727821f97e2a09a`, including #12428's final
head `ef322e36cc63e455e22552077a29916822b3204f`, not its earlier prototype.

The package boundary is:

- `TestFramework.csproj` owns `MSTest.TestFramework`. Its ordinary
  `netstandard2.0`, `net462`, `net10.0`, and `net11.0` assemblies contain the
  shared non-UI APIs. There are no application-model copies of core.
- `MSTest.Uwp` owns the existing AppContainer UI attribute and the matching
  adapter/platform-services payloads for both classic `uap10.0.16299` and modern
  UWP (`net10.0-windows10.0.17763.0`, also consumed by supported .NET 11 apps).
  Modern UWP is not WinUI.
- `MSTest.WinUI` owns the existing WinUI attributes and matching
  adapter/platform-services payloads for `net10.0-windows10.0.18362.0` and
  compatible .NET 11 consumers.
- UI assemblies use conventional `lib/<tfm>` assets. Each UI package owns its
  build assets and depends on the matching core version, never on the ordinary
  or other-flavor adapter package. `MSTest.TestAdapter` ships ordinary payloads
  only. SDK, metapackage, direct-reference, and generated-helper consumers select
  one composition; a non-UI helper library continues to reference core alone.

The existing application owns its Windows App SDK/UWP runtime, application
entry point, dispatcher, package identity, activation, cancellation, and cleanup.
This change adds no launcher, handshake, protocol, runtime fallback, or process
exit convention. Existing packaged and unpackaged launchers remain responsible
for their current parent/child exit propagation and per-module results.
Classic UWP retains its distinct bootstrap dependencies. .NET Framework retains
its resolver copy of core and its configuration support.

Removing the Extensions assembly/package layout is an intentional next-major
migration, not a binary-compatible type forwarding exercise. Existing shipped
API records remain historical evidence. The UI attribute names and behavior
remain unchanged; consumers must update package references and recompile.
Sharing the ordinary modern core also makes deployment/directory APIs visible
to modern UI consumers. UI runs must not invent deployment directories:
all six directory getters return null, even when the property bag contains
same-named entries. Unsupported `DeploymentItem` usage must fail before the test
body, including inherited attributes and reflection/source-generated execution,
rather than be ignored. Reuse the existing failed-test result path instead of
adding another exception or discovery framework. Classic UWP uses the netstandard
core, which does not expose `DeploymentItem`.

## Research and experiments

### 2026-11-12: merged-source inspection

- Both prerequisite merge commits are ancestors of the clean starting HEAD;
  origin/main equals the starting HEAD. #12428 is merged with the final head
  above.
- Main still builds UI-specific core assemblies and packages core together with
  Extensions. `_SelectMSTestUiCore` replaces compiler, runtime, and satellite
  items, including an explicit replacement `HintPath` required by desktop
  NuGet/MSBuild. Removing flavor selection entirely supersedes this mechanism;
  merely changing item identities would reintroduce the fixed bug.
- Platform-services' WinUI `AppModel` helper currently lives in Extensions.
  Moving that implementation to platform-services removes the only direct
  internal dependency on the retiring assembly without creating a
  core/UI/adapter project-reference cycle.
- The final prerequisite's Bazel Hosting-to-core correction and append-only
  consumer report are retained. Native Bazel UI-core API-boundary assertions
  must intentionally change to assert the shared-core contract, while keeping
  UI attribute and adapter flavor checks.
- Read-only Windows application-model preflight passed with VS 2026 UWP
  tooling, the classic 16299 SDK, modern Windows SDKs, an interactive desktop,
  and AppX support. No registrations, system settings, or other worktrees were
  changed.
- Initial baseline attempts could not locate unqualified `powershell` in native
  child commands (wrapper exit 9009, then bootstrap exit 1). The inherited PATH
  exceeds cmd.exe's supported length. A bounded command-local PATH containing
  Windows PowerShell, System32, Windows, Git, and the required tools fixes lookup
  without altering global settings. `build.cmd` then installs the pinned SDK,
  workloads, and runtimes normally. These setup failures are not regression
  evidence.
- The broad cold-cache attempts were stopped, but a child desktop MSBuild tree
  escaped the shell's stop and continued writing old restore/build outputs.
  The resulting adapter locking failures and first head package mismatch are
  contaminated setup evidence, not pre-existing repository defects. Exact
  process identities were captured, the owned PIDs were terminated, and a fresh
  process inventory confirmed no remaining build in this workspace. No
  process-name killing, other-worktree cleanup, or global cache eviction occurred.
- The focused framework-owner baseline passed with the repository desktop
  helper (`/restore /t:Build,Pack /p:Configuration=Release /m:8`), building all
  seven original TFMs with zero warnings/errors. The preserved baseline
  `MSTest.TestFramework.5.0.0-dev.nupkg` has SHA-256
  `B5C3A79B141FAFF19EBE1080CB3FFB47865A411A69982CF9C49F28CD3FEAA78C`.
  This is producer build/package evidence, not an executed behavioral baseline.
- Verified the configured effective producer dependency cache is
  `Q:\.tools\.nuget\packages\`. It is reused for immutable external dependencies;
  no cache is copied, cleared, or deleted. Same-version generated product
  consumers retain separate fresh caches.
- After owned-process cleanup, the new core built and packed all four ordinary
  TFMs successfully. New WinUI compilation exposed only missing declarations
  for its existing embedded polyfill attribute; those were recorded in the
  new assembly's unshipped API, not suppressed.
- NuGet rejected a shared `buildTransitive/common` directory with NU5129.
  Instead of suppressing the warning or adding a root import that would apply
  to unrelated TFMs, each supported TFM now owns its appropriately named
  package-local props/targets and `Parallelize.targets`. Existing shared target
  sources are mapped into those locations at pack time. UWP classic/modern
  satellites are kept beside their own TFM payloads to avoid collisions.
- `eng/mark-shipped.ps1` recursively discovers live shipped API filenames.
  Historical Extensions records therefore use `.txt.history` filenames; their
  original bytes are preserved and the release glob has zero archive matches.
- Native Windows libraries and both targeting/asset-selection analysis tests
  built/passed, but the new shared-core validation initially assumed a modern
  portable `TargetFrameworkAttribute`. The prerequisite ordinary Bazel core
  already omitted that attribute: only netstandard/net462 metadata was linked.
  Correct the new oracle, not the native producer: absent metadata remains
  supported, a declared wrong/Windows TFM is rejected, and desktop/UI core bytes
  are compared under the independently planned framework major. No broad native
  metadata, runtime, or source changes are introduced by this correction.

## Alternatives and decision

1. Keep Extensions and the current paired flavor selection. This preserves
   today's hidden platform-specific core API shapes but does not retire the
   project or give consumers explicit package ownership.
2. Rename the existing flavor bundles and continue shipping separate copies of
   core. This is the smallest layout-only alternative, but keeps property-based
   core replacement and lets a UI application disagree with an ordinary helper
   library about the same core assembly's API and identity.
3. Use one non-UI core and explicit UI packages (chosen). Standard NuGet
   selection replaces core swapping. The necessary contract effects are the
   visible modern directory/deployment APIs and explicit rejection of
   unsupported UI deployment. Existing UI behavior is moved, not redesigned.

No migration analyzer, broad exception-handling redesign, new test framework,
or unrelated native/UI fixes are included. Reconsider the selected shape if
real packaged consumers demonstrate an unresolvable core/runtime dependency
or helper composition incompatibility.

The adapter still builds the UI variants that the explicit packages bundle.
Its own pack operation narrows the public framework list only after Build,
before NuGet computes pack frameworks, and restores it afterwards. This also
covers older desktop pack walkers that use `TargetFrameworks` rather than only
NuGet's private item list. Sdk.Extras also caches its flavor matrix during
Build; pack filters that matrix and suppresses only the UI dependency groups
from the otherwise complete restore graph. The original matrix is restored
after pack. Source builds and restore retain the full graph.

The SDK may infer a safe early application-model default and expose a bounded
explicit override where restore precedes desktop UWP tooling imports. Selection
must apply to every runner and source-generated helper injection path. Conflict
checks inspect the current inner TFM's resolved closure, never every target in a
multi-targeted assets file. Package builder hooks must honor
`IsTestApplication=false` without dropping source-generation compile hooks.

## Validation and demonstration

Use the repository-pinned SDK/build scripts, configuration-matched shipping
NuGets, isolated effective consumer caches, and physical/loaded assembly
provenance. Do not equate package inspection, compilation, discovery, skips, or
zero tests with application execution.

| Requirement / risk | Existing or focused E2E evidence | Surface / gap |
| --- | --- | --- |
| Core ownership, API shape, matching outputs and satellites | `CoreFrameworkAssemblyTests`, `FrameworkTargetingTests`, `WindowsApplicationModelPackageTests` | Nonvisual package/API boundary; compare physical package, cache, compiler, runtime, and loaded bytes. |
| SDK defaults, explicit models, conflicts, CPM, multi-TFM and source-generated helper isolation | Relevant `SdkTests` cases plus focused missing composition cases | Generated consumer assets and executed host results; retain ordinary helper behavior. |
| Classic UWP | `ClassicUwpTests` with packed `MSTest.Uwp` | Real registration/activation, test identity/outcome/TRX, and exact owned cleanup. |
| Modern UWP on .NET 10/.NET 11 | `ModernUwpTests` and affected native SDK cases | Desktop UWP build, selected UWP payloads, real activated application and TRX. |
| Packaged/unpackaged WinUI | `PackagedWinUITests`, `WinUITests`, `UnpackagedWinUITests` | Real packaged/unpackaged UI dispatch and existing startup failure paths. |
| .NET Framework resolver/configuration and Hosting | Existing net462 compatibility and generated/ordinary Hosting cases | Real packed consumer, dependencies, selected test results, and core identity. |
| API histories and directly dependent tooling | API analyzer build and existing native Bazel package/library probes | No broad historical-baseline deletion; native tooling surfaces not executed remain explicit gaps. |
| Behavioral sensitivity | Same focused assertions on correct and isolated buildable counterfactual package behavior; preservation cases before/after | Record exact revisions/patches, commands, exits, executed identities/counts and assertion failures. |

### Executed shipping-layout evidence

All runs below use Release, repository SDK
`12.0.100-alpha.1.26556.115`, MSTest `5.0.0-dev`, and MTP `3.0.0-dev`.
The outer acceptance hosts target net12.0; generated consumers target the
frameworks named by their cases. Producer dependencies use the verified
immutable cache; generated consumers use fresh fixture/asset caches.

The producer entry point is:

```powershell
.\build.cmd -pack -c Release -projects $sourceFilter /m:8 /nr:false /bl:$packLog
```

`$sourceFilter` is a session-owned `.slnf` containing all **42 actual `src/`
project members** of `TestFx.slnx`, with an absolute solution path. It is not a
PowerShell/Arcade `src\**` glob. The final producer run exits 0 with zero
warnings/errors, and the unchanged root package verifier passes after updating
its expected physical payload counts: core 67, ordinary adapter 54, Uwp 73,
WinUI 53. Classic Uwp's PRI is retained. The ordinary adapter contains no UI
payload and has only net462/net10/net11 dependency groups.

For acceptance selections, the common command is:

```powershell
.\.dotnet\dotnet.exe test --project $project -c Release --no-build `
  --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" `
  "-bl:$binlog" --no-artifact-post-processing --show-test-results failed
```

Independent discovery uses the same project/filter with `--list-tests json`,
clearing `TestingPlatformCommandLineArguments` and
`TestRunnerAdditionalArguments`. Expected identities are reconciled with source
and verified against fresh TRX by the existing `verify_execution.py` gate.
Every passing row below has exit 0 and zero skips.

| Requirement | Exact selection / executed cases | Result |
| --- | --- | --- |
| Core/UI ownership, matching compile/runtime/satellite bytes, package closure, and pre-body UI deployment failure | `CoreFrameworkAssemblyTests`, `WindowsApplicationModelPackageTests`, and `.WinUITests.` | 43/43 passed. `DeploymentItemsFailBeforeBodiesIncludingInheritedAttributes` executes reflection, Rooting, and ReflectionFree and checks exact failed identities/messages, not merely a failed child exit. |
| SDK defaults/explicit choices, CPM, ordinary/UI multi-TFM, helper/source-generation isolation, and net462 | `SdkTests.MSTestSdk_`, `SettingIsTestApplicationToFalseReducesAddedExtensionsAndMakesProjectNotExecutable`, `TestApplicationRunsTestsFromReferencedMSTestSdkTestLibrary`, `SourceGenerationNonAotTests.MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively`, `FrameworkTargetingTests.PackedPackages_UseMatchingFrameworkAssetsAndRuntime`, `NetFrameworkRunnerCompatibilityTests` | 65/65 passed. Helper runtime payloads stay private; only the MSTest builder hook is excluded, without changing unrelated MTP dependency hooks. |
| Direct conflicting package pairs | `SdkTests.ExplicitPackages_RejectConflictingAdaptersBeforeCompilation` | 3/3 passed for ordinary+Uwp, ordinary+WinUI, and Uwp+WinUI. `PrepareForBuild` reports the composition diagnostic before compilation. |
| Actual classic/modern UWP and unpackaged WinUI | `ClassicUwpTests`, `ModernUwpTests`, `UnpackagedWinUITests` | 12/12 passed. Classic consumes SDK/metapackage/direct Uwp compositions; modern executes net10/net11 plus inherited unsupported deployment. x86/ARM64 layouts build; real execution is x64. Unpackaged dispatch, timeout cleanup, and constructor failure paths execute. |
| Actual packaged WinUI | `PackagedFullTrustWinUI_RegistersActivatesConnectsBackRunsUiTestsAndCleansUp`, `PackagedAppContainerWinUI_RunsThroughMtpControllerAndPublishesTrx`, `PackagedAppContainerWinUI_ExplicitIntegrationPublishesTrx` | 3/3 passed, including direct `MSTest.WinUI`. Names/outcomes and package identity are checked in the real activated host's TRX. |
| Hosting/core dependency and scope lifetime | `GeneratedMSTestHostingInjectionTests` | 8/8 passed across net462/net10/net11, including explicit unsupported AppDomain discovery/execution. |
| .NET Framework assembly-qualified configuration | `DataSourceTests.TestDataSourceFromAppConfig` | 1/1 passed: a net472 consumer selects the net462 package assets, loads `TestConfigurationSection, MSTest.TestFramework`, and produces the expected four passed/one intentionally failed child results. |
| Shipping native SDK routing | `PackagedAppNativeDotnetTestTests.NativeDotnetTest_` | 10/10 passed with isolated SDK 10.0.401 and verbose SDK diagnostics retained: activation, intentional nonzero child, retry, help/discovery, disabled launcher, AppContainer fallback rejection, and disabled routing. |
| Existing context/UI semantics | `TestContextTests` and `WinUIExtensionTests` | 20/20 context cases across net48/net10/net11/WinUI and 6/6 WinUI lifecycle cases passed. |
| Directly dependent native SDK tooling | `.\eng\bazel.ps1 test //:sdk_package_tests --jobs=2 --test_output=errors` | Bazel target passed. Native library/package follow-through is recorded in the maintained Bazel decision entry. |

Real activation preflight passes without changing machine settings. Each real
application owns its unique registration and cleanup. Loaded core/UI/services
paths are recorded in the tests' context output; `CoreFrameworkAssemblyTests`
also compares current NuGet, effective cache, compiler, copied runtime, and
satellite SHA-256 values.

The exact frozen shipping package hashes are:

| Package (`5.0.0-dev`) | SHA-256 |
| --- | --- |
| MSTest.TestFramework | `910EE1E42522F58B709F246B3F485E9D651B4A43C5A7AC2D53DCE6266A844FA4` |
| MSTest.TestAdapter | `4919459CA1CE3F34DC9E6A81185C3FBD35BA81B29E8159C307D6E1C1AAB6F07D` |
| MSTest.Uwp | `9D758A77C1A34781F43208C7488D3BEC0302D80021DACF8A7B0CD54D2AE0AA67` |
| MSTest.WinUI | `C91FABBC15223E201E2AE4E6CD95E68FD6AA8767333575EAD37BFED939C3B77C` |

### Test-value evidence

The isolated comparison starts at the same prerequisite commit and retains the
implementation/test patch SHA-256
`40A24B318FBC73958041B198553CFCA92956665BAF6EEFEC788E9A2E7092937D`.
The deliberate, buildable fault patch has SHA-256
`314F18C1C3B184930923EC5949C7722FE84CAB039D635943980B7506CE81E9E9`:
the UI `TestRunDirectory` getter calls base, and the deployment guard's OR
becomes AND. No API, test, assertion, selector, or package identity is removed.
Both UI packages are rebuilt/repacked into separate outputs; retained consumers
restore those same-version packages through fresh comparison caches.

| Contract / retained test | Wrong implementation | Correct implementation |
| --- | --- | --- |
| `PackedConsumersUseSharedCoreAndSelectedUiPackage` | Executed: four UI cases fail the intended null assertion with `TestRunDirectory = "must-not-be-a-directory"`; both ordinary cases pass. | Same six cases pass in the 43-case shipping selection. |
| `DeploymentItemsFailBeforeBodiesIncludingInheritedAttributes` | Executed: all three metadata modes fail the intended message/canary assertion because `DEPLOYMENT_BODY_EXECUTED` replaces the unsupported-operation result. | Same three cases pass in the shipping selection, including inherited method/class attributes. |

The comparison executes all 9 planned cases: exit 2, 7 intended failures,
2 ordinary passes, zero skips. The first comparison attempt lacked the pinned
10/11 runtimes and is **setup failure, not sensitivity proof**; installing those
exact runtimes fixes that setup without changing product/test bits. Correct
behavior is then restored byte-for-byte, rebuilt/repacked, and rerun separately.
That restored run exits 0: the **same 9/9 cases pass**, zero skips, with fresh
consumer caches. The four contract source/test files match the primary
worktree's SHA-256 values. Red/green NuGets, TRX, build/execution binlogs, retained
test identities, and both patch hashes are preserved in the task's
`counterfactual` evidence directory.
Package/SDK selection groups have executed green coverage but no additional
fault campaign; they are not credited with the two behavioral faults above.

Logs, binary logs, expected identities, and reports are task-owned evidence,
not source additions. This package boundary introduces no report/UI layout,
so a readable transcript and exact artifact/identity checks are more useful
than a staged screenshot. Actual UI activation/dispatch runs above are not
replaced by package manifests or source-only builds.

## Open questions and rollout

The delivery owner remains responsible through package flow and downstream
adoption. This targets main's next major MSTest release, not a servicing
backport that changes an existing released package boundary. Architecture and
release-branch surfaces not exercised locally must remain explicit validation
gaps. No merge or Agent merge is authorized by this package-stage delivery.
