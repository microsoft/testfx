# RFC 011 - Run dotnet test with Microsoft.Testing.Platform

- [x] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Current State

Currently, when we run `dotnet test` in CLI, we use vstest as a test runner/driver to run tests in test projects.

## Motivation

With `dotnet test`, users should be able to use [Microsoft testing platform](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro?tabs=dotnetcli#microsofttestingplatform-pillars) to run their tests for the sake of improving their experience. They should have the option to opt-in/out this new experience.

## Note

We want to design in a way we won't break users and it should be backwards compatible.

### Proposed solution

Make this option configurable in global.json.

Here is a global.json sample:

```json
{
"testSdk" :
  {
    "useTestingPlatform": true
  }
}
```

### Defaults

- If no test runner was provided in global.json, the default is set to vstest.

## Unresolved questions

None.
