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

The Windows PR pipeline experimentally runs [MSBuildCache](https://github.com/microsoft/MSBuildCache). The cache-aware build uses Arcade's `eng/common/msbuild.ps1` launcher to invoke the solution directly because Arcade's outer `Build.proj` discovers projects dynamically and cannot expose the repository's static project graph to the cache plugin. PR builds consume the immutable Azure Pipeline cache read-only, and fork PRs skip this step because they do not receive the required token scope.

The cache steps have three outcomes:

- **The cache build succeeds.** The pipeline runs the remaining Arcade restore, sign, and pack phases without rebuilding, then uses the cached outputs for the test steps. The regular Arcade `Build` step is skipped.
- **The cache build fails with errors the regular build would only reproduce.** A cache hit materializes previously stored outputs and never runs the compiler, so an ordinary build error came from a project that genuinely executed, exactly as the fallback would execute it. Re-running the whole solution through Arcade would report the same errors several minutes later, so the pipeline surfaces them on the cache step and fails the job there instead. Should such a verdict ever be wrong, the cache diagnostics published under `artifacts\log\<configuration>\MSBuildCache` show what the plugin did for that run.
- **The cache build or the preparation phase fails for any other reason.** The pipeline preserves the cache diagnostics, removes partial outputs, and runs the regular Arcade build as a fallback. This covers anything MSBuildCache itself reports (plugin, cache, or file-access problems, including its duplicate-output diagnostic), `NU*` restore failures, engine crashes and OOM (`MSB4166`, `MSB0001`, `MSB1025`, `MSB4017`), `MSB4260` project references that cannot be resolved with a static graph (a `/graph` limitation the fallback does not have), transient file locks (`MSB3021`, `MSB3027`), an unexpected failure of the wrapper script itself, and any failure that never reached MSBuild's error summary. The cache step stays green in this case: marking it `SucceededWithIssues` would make the job `PartiallySucceeded`, which Azure Repos build-validation policies treat as a failure even when the fallback build then succeeds.

Every merge to `main` that touches product build inputs runs a dedicated, batched seed stage for both Debug and Release. This is required: the cache fingerprints project inputs, so entries become stale whenever shared build inputs such as `global.json`, `eng/Versions.props`, or Arcade change. This stage is the only remote cache publisher; PR, manual, and nightly canary builds consume the cache read-only so they cannot race to publish immutable entries. Debug and Release use separate cache universes because configuration-independent projects can otherwise race while the two configurations publish in parallel.

The cache's detached-process exclusions use fully rooted paths (for example, `$(WinDir)\**`). A drive-relative pattern such as `\Windows\**` does not match the absolute file-access paths reported by MSBuild and causes otherwise successful cache builds to fail after compilation. Cache builds also pass `-warnAsError:$false` to Arcade's launcher because MSBuildCache intentionally warns about allowlisted detached telemetry accesses. The fallback Arcade build still treats warnings as errors.

Two settings exist purely to keep the cache usable in this pipeline, and both must stay in sync between the seed stage and the PR canary:

- **`NUGET_PACKAGES` is pinned to `$(Build.SourcesDirectory)\.packages\` on both steps.** MSBuildCache builds its path normalizer from `NUGET_PACKAGES` and fingerprints every restored package file as a project input, so restoring to a different folder changes every node's fingerprint and misses the entire graph. Neither the seed nor the canary can pass Arcade's `-ci` switch, because `eng/common/tools.ps1` would then report build failures through `Write-PipelineSetResult` and exit with code 0, which would make the pipeline treat a failed cache build as a success. Pinning the variable explicitly keeps the two identical without relying on `-ci`.
- **`MSBuildCacheIdenticalDuplicateOutputPatterns` is set to `\**` in `Directory.Build.props`.** By default MSBuildCache hardlinks every output into its content-addressable store and marks it read-only, both when materializing a cache hit and when ingesting the outputs of a cache miss. That makes the Arcade restore/sign/pack pass that follows the cached graph build fail with `UnauthorizedAccessException` while rewriting files such as `artifacts\bin\**\*.deps.json` or the generated `src\Package\MSTest.Sdk\Sdk\Sdk.props`. Clearing the read-only attribute is *not* a valid workaround: the output file and the cache entry share an inode, so writing through it corrupts the cache and can silently rewrite a same-content output owned by another project. Matching every output switches the plugin to copy semantics, which leaves the outputs writable. The trade-offs are extra I/O and a relaxed duplicate-output check that tolerates two projects writing the same path when the content is identical; writing the same path with *different* content is still an error.

Changing either setting invalidates the existing cache entries, so PR runs miss until the next `main` merge reseeds them.

MSBuildCache currently requires Windows, the Visual Studio version pinned in `global.json`, Git on `PATH`, and a clean repository. It does not support incremental developer builds. To validate the local cache from a clean checkout, run:

```powershell
eng\common\msbuild.ps1 -msbuildEngine vs -warnAsError:$false TestFx.slnx /restore /graph /m /reportfileaccesses /t:Build /p:Configuration=Release /p:FastAcceptanceTest=true /p:Publish=false /p:Test=false /p:MSBuildCachePackageEnabled=true /p:MSBuildCacheEnabled=true /p:MSBuildCacheLogDirectory=artifacts\log\Release\MSBuildCache\Plugin /bl:artifacts\log\Release\MSBuildCache\Build.binlog
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

### Mutation testing

The repository uses [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) to mutation-test the server-mode client sources. Restore the pinned local tool and run it from the configured unit-test project:

On Windows PowerShell:

```powershell
dotnet tool restore
Set-Location test/UnitTests/Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests
$env:MutationTesting = "true"
dotnet stryker --output ../../../artifacts/mutation-testing
```

On Linux and macOS:

```shell
dotnet tool restore
cd test/UnitTests/Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests
MutationTesting=true dotnet stryker --output ../../../artifacts/mutation-testing
```

The opt-in property selects Arcade's open strong-name key for the mutated assembly because Stryker's in-memory compiler cannot complete Microsoft delay signing.

The HTML and JSON reports are written to `artifacts/mutation-testing`. The [mutation testing workflow](../.github/workflows/mutation-testing.yml) also runs weekly and can be started manually.

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
