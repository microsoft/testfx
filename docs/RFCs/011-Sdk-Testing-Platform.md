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

## Proposal

Make this option configurable in global.json. We chose global.json because it's located on the current directory level or its parent directories and is picked up by the dotnet sdk.

Here is a global.json sample:

### Examples of Usage

#### 1. Example 1

```json
{
  "test" : {
    "runner": {
      "name": "MicrosoftTestingPlatform"
    }
  }
}
```

It contains the properties below:
- test: Contains configuration related to the test settings.
- runner: Specifies the test runner details.
- name: The name of the test runner to be used, in this case, "MicrosoftTestingPlatform".
 
#### 2. Example 2

```json
{
  "test" : {
    "runner": {
      "name": "VSTest"
    }
  }
}
```

This design is extendable. If later on we decided to support dotnet test as an external dotnet tool.
We can simply add the type/source property and other options as well.

### Default

- If no test runner was provided in global.json, the default is set to vstest.
