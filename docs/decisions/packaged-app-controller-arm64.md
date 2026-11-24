# Native ARM64 packaged-app controller

## Problem and contract

The only precompiled native binary shipped in the MSTest and Microsoft.Testing.Platform
packages is the packaged-app controller `mstest-appmodel-controller.exe`, packed in
`Microsoft.Testing.Extensions.PackagedApp.MSBuild`. It was built only for `win-x64`, so an
ARM64 machine needed an x64 .NET runtime and ran the controller under emulation.

Contract: an ARM64 .NET SDK runs the controller natively and needs no x64 runtime, while an
x64 SDK (including an emulated one on ARM64) behaves exactly as before. The controller's
architecture is independent of the packaged application's architecture, which is activated by
AUMID.

## Research

- Searching `src/`, `eng/` and the packaging projects found no other shipped `.exe`, native
  `.dll` or `runtimes/` payload. The remaining P/Invokes call OS APIs only. `MSTest.Sdk` and the
  adapters do not pin x64, and `_TestArchitecture` already resolves ARM64.
- The controller dll is managed, but the apphost, `deps.json` and the native assets of the
  `Microsoft.Testing.Extensions.CodeCoverage` dependency are RID-specific. That package already
  ships `runtimes/win-arm64/native`, including an ARM64 `msdia140.dll`.
- Package size: each additional RID adds one complete layout per target framework. Only packaged
  WinUI and UWP consumers download this package.

## Decisions

| Question | Decision | Rejected alternative |
| --- | --- | --- |
| Layout | `tools/AppModelController/<tfm>/<rid>/` for both `win-x64` and `win-arm64`. The path is private to this package's targets and tests, and its TFM segment already changed with the .NET 10/11 retarget. | Keeping x64 at `<tfm>/` and adding `<tfm>-arm64/` makes ARM64 a second-class add-on. A nested `<tfm>/arm64/` would be swallowed by the staging glob. |
| Selection | The architecture of the .NET SDK in use (`NETCoreSdkRuntimeIdentifier`), then the MSBuild process architecture for non-SDK projects, then x64. `TestingPlatformPackagedAppControllerArchitecture` (`x64` or `arm64`) overrides it. | The OS architecture would break an emulated x64 SDK that has no ARM64 runtime. The process architecture alone is wrong for 32-bit or x64 MSBuild paired with an ARM64-only SDK. |
| Shared layout | Two complete layouts, no deduplication. | One portable layout with two renamed apphosts needs custom apphost generation and untested multi-RID `deps.json` resolution for about 10-12 MB per TFM. |
| Staging | The consumer's `obj\PackagedAppController\<rid>\` folder is keyed by RID so switching architecture cannot reuse another architecture's files. | A shared folder relies on `SkipUnchangedFiles` size and timestamp comparison. |
| Build input | A private `_PackagedAppControllerRuntimeIdentifier` global property selects the RID. `RuntimeIdentifier` stays a local property, so an outer `-r` cannot leak in. | Passing `RuntimeIdentifier` was already a silent no-op because of `TreatAsLocalProperty`. |
| Runtime discovery | Native SDK launches align an unset or matching application RID with the controller. Preserve a different explicit application RID and configure the staged controller apphost with its matching SDK's relative runtime path, unless the caller supplied a controller root or disabled automatic root handling. Direct MTP launches receive the matching SDK root only when the SDK RID matches the controller architecture. | An x86 SDK root is not a valid `DOTNET_ROOT_X64`. A `dotnet exec` prefix would suppress launch-profile arguments. |

`_TestArchitecture` continues to describe the packaged application for logs. For native SDK
launches, the targets update `RuntimeIdentifier` after `ComputeRunArguments` only when the
application RID is unset or matches the selected controller and the SDK architectures match.
Otherwise, when the SDK architecture matches the controller and no controller root or opt-out was
provided, the SDK's `CreateAppHost` task configures an architecture-specific template with the
relative SDK runtime path. The template is packaged alongside each controller layout; only the
consumer's staged outputs refer to the local path (through a directory junction across volumes).
Like the SDK apphost it is copied from, the template stays unsigned: `CreateAppHost` patches its
bytes, which would invalidate a signature. `eng/SignCheckExclusionsFile.txt` marks it
`DO-NOT-SIGN` for the official signing validation, which first failed on main builds after this
change.
MTP-only environment items do not suppress this native SDK discovery. This preserves the explicit application RID
and empty `RunArguments`, allowing `dotnet run` to apply named launch-profile arguments. A
write-if-different runtime-root marker invalidates apphost generation when the root or opt-out
changes, while unchanged runs remain incremental. The staged files participate in `Clean`.
For direct MTP
launches, the targets forward the matching SDK root to the controller unless the caller supplied
that root already or set `TestingPlatformDisableAppHostDotnetRoot`. Root forwarding requires an
exact SDK architecture suffix (`-arm64` or `-x64`), so an x86 SDK is never treated as an x64
runtime root.
When an explicit controller architecture differs from the SDK, the caller must provide a matching
`DOTNET_ROOT_<ARCH>` if the runtime is not installed in the standard location.

## Validation

- Packed layouts contain `win-x64` and `win-arm64` for `net10.0` and `net11.0`. The apphost and
  `msdia140.dll` PE machine types are `0x8664` and `0xAA64` respectively, which the package test
  asserts together with each `deps.json` `runtimeTarget` and every listed runtime asset.
- Real consumer builds assert the selected, staged layout (including the apphost machine type) for
  the override and SDK-RID paths, plus the explicit error for an unsupported value.
- `ComputeRunArguments_UsesControllerRuntimeRoot` asserts both cross-architecture directions and
  that an x86 SDK does not populate `DOTNET_ROOT_X64`,
  preserves caller-supplied `DOTNET_ROOT_ARM64`/`DOTNET_ROOT_X64` values and the disable switch,
  and verifies the controller apphost and its configured runtime root.
- `ComputeRunArguments_PreservesGlobalApplicationRuntimeIdentifier` asserts that a global
  `RuntimeIdentifier=win-x64` remains unchanged while the matching ARM64 controller apphost is
  configured with the SDK root.
- `DotnetRun_CrossArchitectureApplicationPreservesLaunchProfileArguments` runs the real SDK with
  the opposite application RID and a named profile containing `--info`. It checks the actual
  packaged controller module and its app-relative runtime discovery.
- In the original PR author's validation, on a Windows 11 ARM64 machine
  (Debug, `.\build.cmd -pack`), both the native `win-arm64` and the
  `win-x64` controllers ran `--info` on `net10.0` and `net11.0`. `PackagedAppIntegrationTests` and
  `WindowsApplicationModelPackageTests` passed (128/128), including the tests that launch the
  packaged controller.
- The real application-model tests (`PackagedAppNativeDotnetTestTests`, packaged WinUI activation
  through `dotnet test` and `InvokeTestingPlatform`) passed with the native `win-arm64` controller
  staged under `obj\...\PackagedAppController\win-arm64`.
- Follow-up consumer coverage for the runtime-root contract:

  | Requirement / risk | Automated E2E evidence | Rendered check / gap |
  | --- | --- | --- |
  | Direct MTP receives the SDK-matching root only by default, while preserving caller override and opt-out behavior. | Seven `ComputeRunArguments_UsesControllerRuntimeRoot` rows build generated consumers against the packed package and assert the emitted controller environment. | Nonvisual MSBuild/environment contract; the generated controller is not launched. |
  | Native SDK launch preserves an explicit application RID while selecting a compatible controller runtime. | `ComputeRunArguments_UsesControllerRuntimeRoot` covers default root, explicit root, and opt-out paths; `ComputeRunArguments_PreservesGlobalApplicationRuntimeIdentifier` passes the application RID globally; `DotnetRun_CrossArchitectureApplicationPreservesLaunchProfileArguments` executes the packaged controller through a named SDK launch profile. | Executed on x64 with a private SDK runtime and an ARM64 application RID; native ARM64 execution remains unverified. No output-layout change is intended. |
  | An x86 SDK does not forward its host folder as `DOTNET_ROOT_X64`. | The x86 row in `ComputeRunArguments_UsesControllerRuntimeRoot` asserts no root injection and preserves the application RID. | Nonvisual MSBuild/environment contract; the generated controller is not launched. |
  | Personalized staging remains incremental, responds to opt-out changes, and cleans generated files. | Both `ComputeRunArguments_StagedControllerIsIncrementalAndCleaned` rows check timestamps and cleanup; the cross-architecture row also disables and re-enables runtime-root configuration and compares the resulting apphost bytes. | Generated-consumer filesystem contract. |

- The follow-up runtime-root change was validated against the packed
  `Microsoft.Testing.Extensions.PackagedApp.MSBuild.3.0.0-dev` consumer on Windows x64 with the
  pinned .NET SDK `12.0.100-alpha.1.26562.108`. The source/test patch at base
  `42e661e9c45f0ac1ea9bf3b26267d92fc1e1fa1d` had SHA-256
  `D5191DF6C7E654DDDA122E147FFA3E417B1B317E2992AC101CA8BA6BA9A7210D`. Release packing succeeded;
  the shipping package and the consumer's cached package both had SHA-256
  `2548042E297E5EE0E64C31B4A6AC52AC04B908EFF9D96D19AD5CE28533736E1D`, and the imported target
  hash matched the source target (`7290CBDB4DBF83D428EC1CBC405CD3D8EDC0F538C1068BBBCAD9FA10D372691D`).
  `ComputeRunArguments_UsesControllerRuntimeRoot` independently discovered and executed six rows:
  both RID directions, default root injection, caller-provided roots, and the opt-out. The Release
  acceptance build exited 0; the test run and `verify_execution.py` exited 0 with 6 selected,
  6 executed, 6 passed, 0 failed, and 0 skipped.
  From the worktree root, `$project` was
  `test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj`,
  `$uid` was
  `MSTest.Acceptance.IntegrationTests.PackagedAppIntegrationTests.ComputeRunArguments_UsesControllerRuntimeRoot`,
  and `$results` was
  `artifacts\TestResults\Release\arm64-runtime-root-final-a7616ec9f3fc4c4ea8bfa64b0c41a5c4`.
  The green commands were:

  ```powershell
  .\build.cmd -pack -c Release -bl
  .\.dotnet\dotnet.exe build $project -c Release "-bl:$results\acceptance-build.binlog"
  .\.dotnet\dotnet.exe test --project $project -c Release -f net12.0 --no-build --list-tests -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= -- --filter-uid $uid
  .\.dotnet\dotnet.exe test --project $project -c Release -f net12.0 --no-build -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" -- --filter-uid $uid --show-test-results all
  python .github\skills\testfx-acceptance-validation\scripts\verify_execution.py --trx (Join-Path $results 'MSTest.Acceptance.IntegrationTests_net12.0_x64.trx') --expected-tests (Join-Path $results 'expected-tests.json') --started-after '2026-11-16T11:09:00+00:00' --process-exit-code 0
  ```

- Sensitivity was checked in a detached worktree at the same base with the test retained. A full
  pre-fix package run failed all six rows: two at the missing-root assertion and four at the
  separate controller-RID assertion. To isolate root injection, a buildable targeted counterfactual
  kept the current RID alignment and removed only the two `DOTNET_ROOT_<ARCH>` injection items.
  Its patch (including the retained test) had SHA-256
  `CD1BEA6C94D7EFE5B9B037AF2FC6A82D6786168D3786546863725682D9F4FBDF`; pack and acceptance build
  succeeded, and its package SHA-256 was
  `E567C4D265525B21D1FC2AB93C6376ED7279E9CD465B36703061041BE3E00AD5`. The same six rows executed
  with exit 2: four passed and the two default-injection rows failed at the intended assertion
  (`expected: true`, `actual: false`); no cases were skipped. The target embedded in that package
  matched the counterfactual source target hash
  `58A750F9D315B73610380AA28325703D0D7FD39B2C95DE720F4551AD2B5BF5B0`.
  In that detached worktree, the same `$project` and `$uid` were used; `$results` was the
  `targeted-counterfactual-results` subdirectory of the retained artifact directory above. The
  commands were `.\build.cmd -pack -c Release -bl`,
  `.\.dotnet\dotnet.exe build $project -c Release "-bl:$results\counterfactual-acceptance-build.binlog"`,
  `.\.dotnet\dotnet.exe test --project $project -c Release -f net12.0 --no-build --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= -- --list-tests --filter-uid $uid`,
  and `.\.dotnet\dotnet.exe test --project $project -c Release -f net12.0 --no-build --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" -- --filter-uid $uid --show-test-results all`.
- Commands and reports for both states are retained under
  `artifacts\TestResults\Release\arm64-runtime-root-final-a7616ec9f3fc4c4ea8bfa64b0c41a5c4`.
  This follow-up exercised the generated packed consumer on Windows x64 with simulated SDK/app
  RIDs; it did not execute the new runtime-root behavior on a native ARM64 machine.

### Review-comment follow-up (2026-11-16)

Delivery verification found that the initial `dotnet exec` fallback suppresses
`launchSettings.json` command-line arguments: the .NET SDK applies those only when the
computed command arguments are empty
([SDK 10 source](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Run/RunCommand.cs)).
A real x64 SDK launch with a global `win-arm64` application RID did start the controller
through this fallback, but that is not sufficient compatibility evidence. The fallback was
replaced with a staged apphost configured through the SDK's `CreateAppHost` task:
the package supplies the architecture-correct unbound apphost template, and the consumer
embeds a relative path to its matching SDK installation. This keeps `RunArguments` empty
for launch profiles and leaves global application properties untouched. Explicit roots
and the opt-out continue to use normal apphost discovery. No new public API or runtime
dependency is required.

The two review comments on PR [#12468](https://github.com/microsoft/testfx/pull/12468) identified
that an x86 SDK must not be forwarded as `DOTNET_ROOT_X64`, and that a globally supplied application
RID must not be overwritten. The implementation requires a nonempty SDK RID with the matching
architecture suffix before forwarding a runtime root or personalizing the staged apphost. The
application RID remains unchanged.

The rejected DLL draft had eight passing generated-consumer cases and eight corresponding
pre-fix failures, but did not exercise launch-profile arguments. Its source/test patch hash was
`3d57a297aecbe2c5f6ba9a984d6a0ed43118224c375de1d305312ffd05ebfbb0`, and its package hash was
`D31DD7F39860B6F88C411D7DE1706B8F5F3641A2CF798B3A1A96531120FBAE1A`. Those historical logs remain
under `artifacts\TestResults\Release\arm64-review-comments-final-4d67e213565348eaadd1d535f86bdf07`;
they are not evidence for the final apphost implementation.

### Final apphost evidence

The tested source/test patch is based on PR head `d5f0a44a69324fcc641844c400b68fa07c3cd9b6`;
the PR merge base is `548d3bfc1567fe0aa3663d8dacbe78ab33cedf6e`. The binary Git diff of the
controller project, packaged targets, and acceptance test file has SHA-256
`3A6820C6DA5044E8B92C9D98B725B0E6B804CB6EFF84DEC6C75DF0D9E0FBF585`. The decision record is excluded
from that hash. The same test assembly and source (SHA-256
`F132DEAE81CEEA6B0DB22B9D33D98E5475C9E7E81A3C7DF50E31393B4D8828F9`) ran in all comparisons.

| Variant | Package SHA-256 | Imported target SHA-256 |
| --- | --- | --- |
| Final apphost | `D48AB7C3158191FD60E44B237AF931734445F20198B1684E2F283F27F4D8BF1E` | `64C61C1592392B593F4678403FA0C14388B0DBD69F9AE66A3EC21011DCCEC4A3` |
| Targeted pre-fix comparison | `0252FAE4CFB88E171B9491F2964A6631B2F27A549C4C1CDB71F5E0E1C4A532BD` | `7290CBDB4DBF83D428EC1CBC405CD3D8EDC0F538C1068BBBCAD9FA10D372691D` |

Both variants use `Microsoft.Testing.Extensions.PackagedApp.MSBuild/3.0.0-dev`. The comparison
repacked a separate copy of the final package with only its `buildTransitive` target replaced by
the exact target from `d5f0a44a69324fcc641844c400b68fa07c3cd9b6`; every other archive payload was
verified byte-identical. This is a targeted package counterfactual, not a full base-revision
rebuild. Separate run-owned caches contain the corresponding archives, SHA-512 metadata, and
extracted targets. All 14 generated consumers in each final run were checked for the expected
package SHA-512, cache folder, and generated target import.

**Test-value evidence:**

| Contract / cases | Pre-fix target | Final apphost |
| --- | --- | --- |
| Reject the x86 SDK root / one runtime-root row | Executed: detected, expected no injected root but received one. | Passed. |
| Preserve explicit application RIDs / six runtime-root rows and the global-RID case | Executed: detected, expected application RID but received controller RID. | Seven passed. |
| Named profile and private runtime / real SDK launch | The controller launched, but the app-relative runtime-routing assertion failed. This does not independently prove a launch-profile defect in the base target. | Passed: profile `--info` reached the staged x64 controller, whose host trace selected the repository's private runtime through its app-relative path. |
| Explicit launch overrides, argument tail, incremental staging and cleanup / five cases | Five passed; preservation checks. | Five passed, including opt-out/re-enable apphost regeneration. |

Independent discovery found 14 cases. The final implementation ran 14/14 passed, 0 skipped
(exit 0), the comparison ran 14 executed, 9 failed, 5 passed, 0 skipped (exit 2), and a confirming
final-package run again passed 14/14 (exit 0). Both final green runs passed `verify_execution.py`.
There were no compilation, restore, or launch failures counted as counterfactual proof.
The assembly remains MSTest on MTP, method-level parallelism with `Workers=0`: each case owns
its generated project, staging files, profile and trace; environment overrides are child-only.
No new lock, scheduling change, or shared mutable test asset was introduced.

Commands ran from the worktree root on Windows x64, pinned SDK
`12.0.100-alpha.1.26562.108`, Release acceptance runner `net12.0`, generated consumer `net10.0`.
The real controller selected the private `Microsoft.NETCore.App/11.0.0` runtime. The package
contains all four net10.0/net11.0, x64/ARM64 unbound templates.

```powershell
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$results = 'artifacts\TestResults\Release\arm64-apphost-followup-983c2055'
.\build.cmd -pack -c Release -bl
.\.dotnet\dotnet.exe build $project -c Release -f net12.0 --no-restore "-bl:$results\acceptance-build-final.binlog" -v:quiet

$uids = @(
    'ComputeRunArguments_UsesControllerRuntimeRoot',
    'ComputeRunArguments_PreservesGlobalApplicationRuntimeIdentifier',
    'DotnetRun_CrossArchitectureApplicationPreservesLaunchProfileArguments',
    'ComputeRunArguments_StagedControllerIsIncrementalAndCleaned',
    'ComputeRunArguments_PreservesExplicitOverrides',
    'ComputeRunArguments_ResolvesRelativeTargetAndPreservesTailWithoutAppHost'
) | ForEach-Object { "MSTest.Acceptance.IntegrationTests.PackagedAppIntegrationTests.$_" }
.\.dotnet\dotnet.exe test --project $project -c Release -f net12.0 --no-build --list-tests -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= "-bl:$results\discovery.binlog" -- --filter-uid $uids

# These retained scripts run the selector above with separate result folders and cache variants.
& "$results\run-focused.ps1" -Label green-final -Cache green-cache
& "$results\prepare-counterfactual.ps1"
& "$results\run-focused.ps1" -Label counterfactual-red -Cache counterfactual-cache -ExpectFailure
& "$results\run-focused.ps1" -Label green-confirmation -Cache green-cache
& "$results\verify-provenance.ps1"
```

Final packing, acceptance build, discovery, execution gates and provenance checks exited 0.
Earlier attempts exposed two test-formatting errors, a missing collection-cache marker, a missing
consumer restore before `--no-build`, and a cleanup invocation without the matching application
RID; these were corrected before the final runs and are not red-phase evidence. Logs and reports
remain under the results directory above.

### Additional review follow-up (2026-11-16)

The next review found two native-launch gaps: MTP-only environment items suppressed
private SDK discovery despite not being forwarded by `dotnet run`, and a runtime root
on another volume cannot be embedded as an app-relative path. The native SDK reads
only command, arguments, working directory and RID/framework properties
([SDK run contract](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Run/RunCommand.cs)).

Keep `TestingPlatformEnvironmentVariable` overrides scoped to direct MTP execution;
only inherited architecture-specific root properties suppress native personalization.
For cross-volume staging, use a project-owned Windows directory junction to the borrowed
SDK and embed its relative name. [`mklink /J`](https://learn.microsoft.com/windows-server/administration/windows-commands/mklink)
supports a local absolute target without symbolic-link privileges. The junction is
unlinked (not recursively deleted) on root changes, opt-out and Clean. No SDK files
belong to `FileWrites`. Same-volume launches retain the existing relative path.
Paths are passed through child environment variables rather than interpolated into shell
commands. This avoids copying an entire runtime or introducing another native launcher.
Reusing `dotnet exec` remains unsuitable because it suppresses named-profile arguments.

The real named-profile consumer test now crosses ordinary/cross-volume staging with
absent/present MTP-only root items. It checks the controller identity, runtime host trace,
and cross-volume cleanup without deleting the borrowed SDK. Cross-volume cases require
the system temporary directory and repository SDK on different volumes; single-volume
hosts report that capability gap instead of claiming coverage. Each case owns a unique
project and temporary directory, with child-only environment overrides and no shared
drive mappings or scheduling changes.

The anonymous-token review fix removes the `OpenThreadToken`-based level query only
from `CreateServerStream_AnUnauthorizedIdentityCannotConnect`; duplicated AppContainer
tokens retain their guard. Windows documents that anonymous tokens cannot be opened
([OpenThreadToken](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openthreadtoken)).
Successful connections and exceptions other than exact access denied still fail.
Only `IOException` with HRESULT `0x80070542` (`ERROR_BAD_IMPERSONATION_LEVEL`) reports
an explicit capability-related inconclusive outcome; it is not evidence that the pipe
denied access. Impersonation cleanup remains in `finally`.

#### Follow-up execution evidence

Source state: `e0600dc84d96f8f16b91b38f08139a6f5a3c0eec` plus the source/test binary
patch SHA-256 `63D89A20A1FA7AD24548FE08338DD7FEC847DAC866A014E88C709BDC52A6C3F7`
(documentation excluded). The pinned SDK, runner TFM and configuration are unchanged.
Artifacts are retained under `artifacts\TestResults\Release\arm64-final-comments-20261116`.
The final package SHA-256 is
`EBCB9DCDF09AF867EC22AF09EAA7F48969B6ED1D63A416247A1517CD5A089420`;
its target and the source target both hash to
`C25B6AB3E3A58AE44339246F9DCCD82C04FC0E51672E89FCE8993E6C4D46B1DA`.
Every one of the 17 generated consumers was checked for the exact package SHA-512,
isolated cache and generated target import.

| Requirement / case | Executed outcome |
| --- | --- |
| MTP-only roots do not suppress native discovery / `ComputeRunArguments_UsesControllerRuntimeRoot` and named-profile cases | Both simulated architecture directions pass; the real x64 controller uses the private runtime with the MTP-only item present or absent. |
| Cross-volume launch / `DotnetRun_CrossArchitectureApplicationPreservesLaunchProfileArguments` | SDK on Q:, intermediates on C:, opposite global application RID: both MTP-item variants pass, including named-profile `--info`, controller identity and host-trace assertions. |
| Junction lifetime / the same cross-volume cases | Unchanged staging preserves the apphost timestamp; opt-out unlinks the junction; re-enabling restores identical apphost bytes; Clean unlinks without deleting the borrowed SDK. |
| Existing launch behavior / remaining focused cases | Explicit launch overrides, argument tails, global RID preservation and same-volume incremental staging/cleanup pass. |
| Anonymous pipe denial / `CreateServerStream_AnUnauthorizedIdentityCannotConnect` | Source correction compiles. This host lacks usable impersonation: all three TFMs explicitly skip on error 1346. No successful denial execution or red/green proof is claimed. |

Commands from the worktree root:

```powershell
.\build.cmd -pack -c Release -bl
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$results = 'artifacts\TestResults\Release\arm64-final-comments-20261116'
.\.dotnet\dotnet.exe build $project -c Release -f net12.0 --no-restore "-bl:$results\acceptance-build.binlog" -v:quiet
& "$results\run-focused.ps1" -Label discovery -Discovery
& "$results\run-focused.ps1" -Label green-final
& "$results\prepare-counterfactual.ps1"
& "$results\run-focused.ps1" -Label counterfactual-red -Cache counterfactual-cache
& "$results\run-focused.ps1" -Label green-confirmation
& "$results\verify-provenance.ps1"
foreach ($label in @('green-final', 'green-confirmation')) {
    python .github\skills\testfx-acceptance-validation\scripts\verify_execution.py --trx "$results\$label\MSTest.Acceptance.IntegrationTests_net12.0_x64.trx" --expected-tests "$results\expected-tests.json" --started-after (Get-Content "$results\$label\started-after.txt") --process-exit-code 0
}
```

Pack, build, discovery, both final runs, provenance checks and both execution gates
exited 0. Discovery and both final executions reconcile to 17 cases, 17 passed,
zero skipped. An initial run passed 15/17 because the cross-volume test expectation
omitted the SDK-appended `net10.0` path segment; that test error was corrected before
the recorded final runs.

The PR's Windows Release path includes the acceptance project through the full
`dotnet test --solution TestFx.slnx` invocation in
`eng/pipelines/steps/test-windows-configuration-tests.yml`, gated by `SkipTests=false`
and the product-change job conditions. These cases are Windows-only, not AppX activation
tests; the cross-volume rows additionally require distinct SDK/temp volumes. This is
static selection evidence, not proof that CI executed the new revision.

**Test-value evidence:** the comparison keeps the exact final test assembly and
repackages only the pre-fix target from the stated base into an isolated copy
(package SHA-256 `2997C4ADC0BDEBED454C88AE651CF60FDB9AF58566188B56E59FA458AAE82DA1`;
all other payload entries byte-identical). It executes all 17 cases, exits 2, and
reports 12 passes and five failures: four runtime-root/host-trace assertions detect
the missing native root selection; the fifth reproduces `AppRelativePathRootedException`
in the generated consumer build. That build failure reproduces the reported symptom
but is not counted as behavioral red-phase proof. The final package passes the
same selection twice.

Anonymous-token commands used the repository-local SDK and `Debug` unit-test project
`test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj`:
`dotnet test --project <project> --configuration Debug --framework net10.0 --filter
FullyQualifiedName=Microsoft.Testing.Platform.UnitTests.NamedPipeServerSecurityTests.CreateServerStream_AnUnauthorizedIdentityCannotConnect
--report-trx --results-directory artifacts/TestResults/pr12468-anonymous-baseline-20261116-01
-bl:artifacts/log/Debug/pr12468-anonymous-baseline-{}.binlog` exited 0 with one skip.
The same command after the fix, without `--framework` and with `anonymous-fixed`
artifact paths, exited 2 with three failures (`net10.0`, `net11.0`, `net462`):
`IOException` for invalid impersonation level instead of `UnauthorizedAccessException`.
After adding the exact capability-error handling, the same three-TFM command using
`artifacts/TestResults/pr12468-anonymous-capability-20261116-01` and
`artifacts/log/Debug/pr12468-anonymous-capability-{}.binlog` exited 0 with zero passes,
zero failures and three explicit skips. TRX, JUnit and CTRF were parsed. Denial on a
capable host remains unverified; the skip is not counted as successful test-value evidence.

## Gaps

- `--coverage` and `--hangdump` were not run with the `win-arm64` controller, and Test Explorer
  was not exercised on ARM64.
- The runtime-root matrix includes simulated SDK RIDs on Windows x64; the final real SDK/controller
  launch also ran on x64. Native ARM64 execution of these new paths remains unverified. The
  real SDK run used the pinned preview SDK, not a separate shipping .NET 10 SDK. CI was not checked.
- Inherited architecture-specific roots are preserved; MTP-only items remain scoped to direct MTP
  execution. Runtime-root overrides introduced
  only by a launch profile after that evaluation have not been validated; the personalized
  apphost searches its app-relative location before environment/global locations.
- An explicit controller architecture that differs from the SDK architecture requires a matching
  `DOTNET_ROOT_<ARCH>` when that runtime is privately installed; cross-architecture private SDK
  discovery is not inferred from `DOTNET_HOST_PATH`.
- The cross-volume fallback requires Windows directory-junction support. UNC-hosted SDK
  installations were not exercised. Anonymous-token denial still needs execution on a host
  with the required impersonation privilege.
- The Bazel `//:windows_controllers` validation build and the test harness RID constants
  (`win-x64` test applications) are unchanged; they describe the application under test, not the
  controller.
