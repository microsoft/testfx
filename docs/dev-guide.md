# Development Guide

This document contains all the required information to build, test, and consume MSTest.

## Prerequisites

To build and test all functionalities of MSTest, we recommend installing [Visual Studio 2022](https://visualstudio.microsoft.com/) with the following workloads:

- `.NET desktop development`
- `Universal Windows Platform development`
- `.Net Core cross-platform development`

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

By default, the script generates a *Debug* build type, which is not optimized code and includes asserts. As its name suggests, this makes it easier and friendlier to debug the code. If you want to make performance measurements, you ought to build the *Release* version instead, which doesn't have any assets and has all code optimizations enabled. Likewise, if you plan on running tests, the *Release* configuration is more suitable since it's considerably faster than the *Debug* one. For this, you add the flag `-configuration release` (or `-c release`). For example:

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

## Visual Studio version requirement

If working with Visual Studio, this repository uses the new, modern, XML-based slnx solution file format (`TestFx.slnx`). This solution file can only be opened or loaded successfully using Visual Studio 2022 17.13 or higher. Opening the TestFx.slnx directly with a different version of Visual Studio installed other than Visual Studio 2022 17.13 or higher will just open the slnx file in a raw solution XML format.
