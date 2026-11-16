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
| Runtime discovery | Native SDK launches resolve the controller runtime using the controller RID, not the packaged application's RID. Direct MTP launches receive the matching SDK root from `DOTNET_HOST_PATH` when the caller has not supplied an architecture-specific root. | The framework-dependent controller apphost cannot locate a private SDK installation from the application RID when the two architectures differ. |

`_TestArchitecture` continues to describe the packaged application for logs. For native SDK
launches, the targets update `RuntimeIdentifier` after `ComputeRunArguments` only when the selected
controller and SDK architectures match, so the SDK chooses the matching `DOTNET_ROOT_<ARCH>` while
keeping the existing executable command and argument tail. For direct MTP launches, the targets
forward the matching SDK root to the controller unless the caller supplied that root already or
set `TestingPlatformDisableAppHostDotnetRoot`.
When an explicit controller architecture differs from the SDK, the caller must provide a matching
`DOTNET_ROOT_<ARCH>` if the runtime is not installed in the standard location.

## Validation

- Packed layouts contain `win-x64` and `win-arm64` for `net10.0` and `net11.0`. The apphost and
  `msdia140.dll` PE machine types are `0x8664` and `0xAA64` respectively, which the package test
  asserts together with each `deps.json` `runtimeTarget` and every listed runtime asset.
- Real consumer builds assert the selected, staged layout (including the apphost machine type) for
  the override and SDK-RID paths, plus the explicit error for an unsupported value.
- `ComputeRunArguments_UsesControllerRuntimeRoot` asserts both cross-architecture directions,
  preserves the empty argument tail and controller command, and honors caller-supplied
  `DOTNET_ROOT_ARM64`/`DOTNET_ROOT_X64` values and the disable switch.
- On a Windows 11 ARM64 machine (Debug, `.\build.cmd -pack`), both the native `win-arm64` and the
  `win-x64` controllers ran `--info` on `net10.0` and `net11.0`. `PackagedAppIntegrationTests` and
  `WindowsApplicationModelPackageTests` passed (128/128), including the tests that launch the
  packaged controller.
- The real application-model tests (`PackagedAppNativeDotnetTestTests`, packaged WinUI activation
  through `dotnet test` and `InvokeTestingPlatform`) passed with the native `win-arm64` controller
  staged under `obj\...\PackagedAppController\win-arm64`.
- Follow-up consumer coverage for the runtime-root contract:

  | Requirement / risk | Automated E2E evidence | Rendered check / gap |
  | --- | --- | --- |
  | Direct MTP receives the SDK-matching root only by default, while preserving caller override and opt-out behavior. | Six `ComputeRunArguments_UsesControllerRuntimeRoot` rows build generated consumers against the packed package and assert the emitted controller environment. | Nonvisual MSBuild/environment contract; the generated controller is not launched. |
  | Native SDK launch selects the controller RID without changing its command or empty argument tail. | The same rows assert `RuntimeIdentifier`, `RunCommand`, and `RunArguments`. | Nonvisual launch-property contract; a real native SDK/controller launch with a private cross-architecture runtime remains unverified. |
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

## Gaps

- `--coverage` and `--hangdump` were not run with the `win-arm64` controller, and Test Explorer
  was not exercised on ARM64.
- An explicit controller architecture that differs from the SDK architecture requires a matching
  `DOTNET_ROOT_<ARCH>` when that runtime is privately installed; cross-architecture private SDK
  discovery is not inferred from `DOTNET_HOST_PATH`.
- The Bazel `//:windows_controllers` validation build and the test harness RID constants
  (`win-x64` test applications) are unchanged; they describe the application under test, not the
  controller.
