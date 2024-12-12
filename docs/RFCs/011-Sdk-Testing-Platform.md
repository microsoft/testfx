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

Here are some global.json samples:

1.
```json
{
"testSdk" :
  {
    "useTestingPlatform": true
  }
}
```

What if we want to support another test runner?

We simply can't, with this approach we either use the testing platform, or fallback to vstest if this property was set to false.

2.
```json
{
"testSdk" :
  {
    "testRunner": "vstest/testingplatform"
  }
}
```

What if we decide to extract the testing platform as an external tool?

We still could support more options.

But if, for some reason, the latest version was broken, we will break as well.

3.
```json
{
"testSdk" :
  {
    "testRunner": "vstest/testingplatform/...",
    "version": "1.5.0"
  }
}
```

Users are allowed to force install a specific version of the tool.

If not specified then we will fallback to the latest version.

### Defaults

- If no test runner was provided in global.json, the default is set to vstest.

## Unresolved questions

None.
