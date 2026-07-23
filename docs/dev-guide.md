# Development Guide

This document contains all the required information to build, test, and consume MSTest.

## Prerequisites

To build and test all functionalities of MSTest, we recommend installing [Visual Studio 2026](https://visualstudio.microsoft.com/) with the following workloads:

- `.NET desktop development`
- `Universal Windows Platform development`
- `.NET Core cross-platform development`

## Recommended workflow

We recommend the following overall workflow when developing this repository:

- Fork this repository.
- Always work on your fork.
- Always keep your fork up to date.

### Starting with your fork

Before updating your fork, run this command:

```shell
git remote add upstream https://github.com/Microsoft/testfx.git
```

This will make management of multiple forks and your work easier over time.

### Updating your fork

We recommend the following commands to update your fork:

```shell
git checkout main
git clean -dfx
git fetch upstream
git rebase upstream/main
git push
```

Or more succinctly:

```shell
git checkout main && git clean -xdf && git fetch upstream && git rebase upstream/main && git push
```

This will update your fork with the latest from `microsoft/testfx` on your machine and push those updates to your remote fork.

## Building

The easiest and recommended solution is to build the repository with the provided scripts at the repo root.

For Windows:

```shell
build.cmd
```

For Linux and macOS:

```shell
./build.sh
```

### Common building options

By default, the script generates a *Debug* build type, which is not optimized code and includes asserts. As its name suggests, this makes it easier and friendlier to debug the code. If you want to make performance measurements, you ought to build the *Release* version instead, which doesn't have any asserts and has all code optimizations enabled. Likewise, if you plan on running tests, the *Release* configuration is more suitable since it's considerably faster than the *Debug* one. For this, you add the flag `-configuration release` (or `-c release`). For example:

For Windows:

```shell
build.cmd -configuration release
```

For Linux and macOS:

```shell
./build.sh --configuration release
```

Another common flag is `-pack` which will produce the NuGet packages of MSTest. These packages are required for the acceptance tests (see [testing section](#testing)).

For more information about all the different options available, supply the argument `-help|-h` when invoking the build script. On Unix-like systems, non-abbreviated arguments can be passed in with a single `-` or double hyphen `--`.

### MSBuildCache

The Windows PR pipeline experimentally runs [MSBuildCache](https://github.com/microsoft/MSBuildCache) before the regular Arcade build. The cache-aware build uses Arcade's `eng/common/msbuild.ps1` launcher to invoke the solution directly because Arcade's outer `Build.proj` discovers projects dynamically and cannot expose the repository's static project graph to the cache plugin. PR builds consume the immutable Azure Pipeline cache read-only, and fork PRs skip this step because they do not receive the required token scope.

Every product merge to `main` runs a dedicated, batched seed stage for both Debug and Release. This is required: the cache fingerprints project inputs, so the nightly seed becomes stale whenever shared build inputs such as `global.json`, `eng/Versions.props`, or Arcade change. The nightly build remains as a fallback refresh. Debug and Release use separate cache universes because Azure Pipeline cache entries are immutable and configuration-independent projects can otherwise race while the two configurations publish in parallel.

The cache's detached-process exclusions use fully rooted paths (for example, `$(WinDir)\**`). A drive-relative pattern such as `\Windows\**` does not match the absolute file-access paths reported by MSBuild and causes otherwise successful cache builds to fail after compilation. Cache builds also pass `-warnAsError:$false` to Arcade's launcher because MSBuildCache intentionally warns about allowlisted detached telemetry accesses. The regular Arcade build still treats warnings as errors and remains authoritative.

MSBuildCache currently requires Windows, the Visual Studio version pinned in `global.json`, Git on `PATH`, and a clean repository. It does not support incremental developer builds. To validate the local cache from a clean checkout, run:

```powershell
eng\common\msbuild.ps1 -msbuildEngine vs -warnAsError:$false TestFx.slnx /restore /graph /m /reportfileaccesses /t:Build /p:Configuration=Release /p:MSBuildCachePackageEnabled=true /p:MSBuildCacheEnabled=true /p:MSBuildCacheLogDirectory=artifacts\log\Release\MSBuildCache\Plugin /bl:artifacts\log\Release\MSBuildCache\Build.binlog
```

Use `MSTest.slnf` or `Microsoft.Testing.Platform.slnf` in place of `TestFx.slnx` to validate a filtered graph. Delete `artifacts\msbuild-cache` to clear the local content cache. Change the prefix of `MSBuildCacheCacheUniverse` in `Directory.Build.props`, or pass `/p:MSBuildCacheCacheUniverse=<new-value>` for one invocation, to invalidate cache entries. The default universe includes `$(Configuration)`, so use the same configuration when populating and consuming a cache.

### Build layout

MSTest uses Microsoft common infrastructure called [arcade](https://github.com/dotnet/arcade) as such all outputs follow this structure:

```text
artifacts
  bin
    $(MSBuildProjectName)
      $(Configuration)
  packages
    $(Configuration)
      Shipping
        $(MSBuildProjectName).$(PackageVersion).nupkg
      NonShipping
        $(MSBuildProjectName).$(PackageVersion).nupkg
      Release
      PreRelease
  TestResults
    $(Configuration)
      $(MSBuildProjectName)_$(TargetFramework)_$(TestArchitecture).(xml|html|log|error.log)
  SymStore
    $(Configuration)
      $(MSBuildProjectName)
  log
    $(Configuration)
      Build.binlog
  tmp
    $(Configuration)
  obj
    $(MSBuildProjectName)
      $(Configuration)
  toolset
```

with

| directory         | description                                            |
|-------------------|--------------------------------------------------------|
| bin               | Build output of each project.                          |
| obj               | Intermediate directory for each project.               |
| packages          | NuGet packages produced by all projects in the repo.   |
| TestResults       | Test results produced by test runs.                    |
| SymStore          | Storage for converted Windows PDBs                     |
| log               | Build binary log and other logs.                       |
| tmp               | Temp files generated during build.                     |
| toolset           | Files generated during toolset restore.                |

## Testing

MSTest uses the following 3 kinds of tests:

- Unit tests
  - Very fast tests primarily validating individual units.
  - Named as `<ProjectUnderTest>.UnitTests` where `<ProjectUnderTest>` is the project under test.
- Integration tests
  - Slightly slower tests with File system interactions.
  - Named either as `<ProjectUnderTest>.IntegrationTests` or as `<PackageUnderTest>.Acceptance.IntegrationTests` where
    `<ProjectUnderTest>` is the project under test
    `<PackageUnderTest>` is the package under test
- Performance tests
  - Focused tests that ensure the performance of specific workflows of the application

The easiest way to run the tests is to call

For Windows:

```shell
build.cmd -pack -test -integrationTest
```

For Linux and macOS:

```shell
./build.sh -pack -test -integrationTest
```

Note that `-test` allows to run the unit tests and `-integrationTest` allows to run the two kinds of integration tests. Acceptance integration tests require the NuGet packages to have been produced hence the `-pack` flag.

## Working with Visual Studio

If you are working with Visual Studio, we recommend opening it through the `open-vs.cmd` script at the repo root. This script will set all the required environment variables required so that Visual Studio picks up the locally downloaded version of the .NET SDK. If you prefer to use your machine-wide configuration, you can open Visual Studio directly.

Inside Visual Studio, all projects can be built normally. All but acceptance tests can be tested directly from Visual Studio. The acceptance tests will always use the version of the NuGet packages produced in the `artifacts/packages/shipping` folder so if you have made some changes and run these tests, it's likely that the changes will not be applied.

## Developing MSTest.Sdk

Do not use `IsImplicitlyDefined="true"` on `PackageReference` items in the MSTest.Sdk `.targets` files. The package would be defined twice, which can produce `NU1009` warnings that are commonly treated as errors.

Do not use `VersionOverride` on those `PackageReference` items. Although it can override a version under Central Package Management (CPM), it is forbidden when `CentralPackageVersionOverrideEnabled` is `false` and causes `NU1013`.

Instead, split version specification based on CPM:

- When `ManagePackageVersionsCentrally` is not `true`, set `Version` directly on the `PackageReference`.
- When `ManagePackageVersionsCentrally` is `true`, leave the `PackageReference` unversioned and add a matching `PackageVersion` item.

This supports both values of `CentralPackageVersionOverrideEnabled` without producing `NU1009`.

### Layering MSTest.Sdk on another base SDK

`MSTest.Sdk` implicitly imports `Microsoft.NET.Sdk`. To combine it with another base SDK, such as `Microsoft.NET.Sdk.Web`, import both SDKs manually and list the other SDK first:

```xml
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk.Web" />
  <Import Project="Sdk.props" Sdk="MSTest.Sdk" />

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <Import Project="Sdk.targets" Sdk="MSTest.Sdk" />
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk.Web" />
</Project>
```

Because an `<Import>` cannot specify the SDK version in the same way as `<Project Sdk="MSTest.Sdk/x.y.z">`, pin it in `global.json`:

```json
{
  "msbuild-sdks": {
    "MSTest.Sdk": "x.y.z"
  }
}
```

`Sdk.props` and `Sdk.targets` guard their `Microsoft.NET.Sdk` imports with `_MSTestSdkImportsMicrosoftNETSdk`. MSTest.Sdk imports the base SDK only when another SDK has not already set `UsingMicrosoftNETSdk`, avoiding `MSB4011` duplicate-import warnings.

## Visual Studio version requirement

If working with Visual Studio, this repository uses the new, modern, XML-based slnx solution file format (`TestFx.slnx`). This solution file can only be opened or loaded successfully using Visual Studio 2022 17.13 or higher. Opening the TestFx.slnx directly with a different version of Visual Studio installed other than Visual Studio 2022 17.13 or higher will just open the slnx file in a raw solution XML format.
