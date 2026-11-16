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

Not changed: `InvokeTestingPlatformTask` and `_TestArchitecture`. The controller runs in direct
executable mode, so DOTNET_ROOT injection does not apply, and `_TestArchitecture` correctly
describes the application for logs.

## Validation

- Packed layouts contain `win-x64` and `win-arm64` for `net10.0` and `net11.0`. The apphost and
  `msdia140.dll` PE machine types are `0x8664` and `0xAA64` respectively, which the package test
  asserts together with each `deps.json` `runtimeTarget` and every listed runtime asset.
- Real consumer builds assert the selected, staged layout (including the apphost machine type) for
  the override and SDK-RID paths, plus the explicit error for an unsupported value.
- On a Windows 11 ARM64 machine (Debug, `.\build.cmd -pack`), both the native `win-arm64` and the
  `win-x64` controllers ran `--info` on `net10.0` and `net11.0`. `PackagedAppIntegrationTests` and
  `WindowsApplicationModelPackageTests` passed (128/128), including the tests that launch the
  packaged controller.
- The real application-model tests (`PackagedAppNativeDotnetTestTests`, packaged WinUI activation
  through `dotnet test` and `InvokeTestingPlatform`) passed with the native `win-arm64` controller
  staged under `obj\...\PackagedAppController\win-arm64`.

## Gaps

- `--coverage` and `--hangdump` were not run with the `win-arm64` controller, and Test Explorer
  was not exercised on ARM64.
- The Bazel `//:windows_controllers` validation build and the test harness RID constants
  (`win-x64` test applications) are unchanged; they describe the application under test, not the
  controller.