# RFC 011 - Run dotnet test with Microsoft.Testing.Platform

- [x] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Current State

Currently, when we run `dotnet test` in CLI, we use vstest as a test runner/driver to run tests in test projects.

## Motivation

With `dotnet test`, users should be able to use [Microsoft Testing Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro?tabs=dotnetcli#microsofttestingplatform-pillars) to run their tests for the sake of improving their experience. They should have the option to opt-in/out this new experience.

The reason for opting-in/out this experience is

1. Autodetecting if test projects are using vstest or the testing platform is complex, and we would end up with many false positives, making it hard to deliver a working and consistent experience.
2. Mixed mode (i.e. having projects using vstest and testing platform in the same solution) will never work as the two platforms have different command line options and different features, thus the mapping will not work as expected.

## Note

We want to design in a way that is future proof and easy to keep backwards compatible.

## Proposals

Make this option configurable in global.json. We chose global.json because it's located on the current directory level or its parent directories and is picked up by the dotnet sdk.

Here are some global.json suggestions:

### 1. Enable/Disable Testing Platform

#### Example of Usage

```json
{
"testSdk" :
  {
    "useTestingPlatform": true
  }
}
```

- `testSdk`: Represents the configuration settings for the test SDK.
  - `useTestingPlatform`: A boolean value that determines whether to use the Microsoft Testing Platform. If set to `true`, the testing platform will be used; if set to `false`, vstest will be used.

#### Unresolved Questions

What if we want to support another test runner? We simply can't, with this approach we either use the testing platform, or fallback to vstest if this property was set to false.

### 2. Specify the Test Runner Tool

#### Examples of Usage

```json
{
"testSdk" :
  {
    "tool": "testingplatform"
  }
}
```

- `testSdk`: Represents the configuration settings for the test SDK.
  - `tool`: Specifies the testing tool to be used (`vstest` or `testingplatform`). In this case, `testingplatform` is the tool being used.

#### Unresolved Questions

What if we decide to use the testing platform as an external tool/service? We still could support more options.

But if, for some reason, the latest version of the testing platform was broken, we will break as well.

### 3. Specify the Test Runner Tool and Other Options

#### Example of Usage

```json
{
"testSdk" :
  {
    "tool": "testingplatform",
    "version": "1.5.0",
    "allowPrerelease": false
  }
}
```

- `testSdk`: Represents the configuration settings for the test SDK.
  - `tool`: Specifies the name of the testing tool being used. In this case, it is `testingplatform`.
  - `version`: Indicates the version of the testing tool. Here, it is set to "1.5.0".
  - `allowPrerelease`: A boolean value that determines whether pre-release versions of the testing tool are allowed. It is set to false, meaning pre-release versions are not permitted.

This provides more control over the test runner tool and ensures compatibility with specific versions. If it's not specified, then we will fallback to the latest version.

### Default

- If no test runner was provided in global.json, the default is set to vstest.
