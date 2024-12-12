# RFC 011 - Run dotnet test with Microsoft.Testing.Platform

- [x] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Current State

Currently, when we run `dotnet test` in CLI, we use vstest as a test runner/driver to run tests in test projects.

## Motivation

With `dotnet test`, users should be able to use [Microsoft testing platform](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro?tabs=dotnetcli#microsofttestingplatform-pillars) to run their tests for the sake of improving their experience. They should have the option to opt-in/out this new experience.

The reason for opting-in/out this experience is

1. Autodetecting if test projects are using vstest or the testing platform is complex, and we would end up with many false positives, making it hard to deliver a working and consistent experience.
2. Mixed mode (i.e. having projects using vstest and testing platform in the same solution) will never work as the two platforms have different command line options and different features, thus the mapping will not work as expected.

## Note

We want to design in a way that is future proof and easy to keep backwards compatible.

## Proposed solution

Make this option configurable in global.json. We chose global.json because it's located on the current directory level or its parent directories and is picked up by the dotnet sdk.

Here are some global.json suggestions:

### 1. Enable/Disable Testing Platform

```json
{
"testSdk" :
  {
    "useTestingPlatform": true
  }
}
```

What if we want to support another test runner? We simply can't, with this approach we either use the testing platform, or fallback to vstest if this property was set to false.

### 2. Specify the Test Runner Tool

```json
{
"testSdk" :
  {
    "tool": "vstest/testingplatform"
  }
}
```

What if we decide to extract the testing platform as an external tool? We still could support more options.

But if, for some reason, the latest version of the testing platform was broken, we will break as well.

### 3. Specify the Test Runner Tool and Version

```json
{
"testSdk" :
  {
    "tool": "vstest/testingplatform",
    "version": "1.5.0",
    "allowPrerelease": false
  }
}
```

Users are allowed to force install a specific version of the tool. If it's not specified, then we will fallback to the latest version.

### Default

- If no test runner was provided in global.json, the default is set to vstest.
