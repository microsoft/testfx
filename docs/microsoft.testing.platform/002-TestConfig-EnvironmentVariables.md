# RFC 002 - testconfig.json environmentVariables section

- [x] Approved in principle
- [ ] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

Add an `environmentVariables` section to `testconfig.json` that lets users declare environment variables to apply to the test host process. This mirrors the `<EnvironmentVariables>` element of legacy `.runsettings`, removes the need for users to ship a custom `ITestHostEnvironmentVariableProvider`, and brings parity with the most-requested missing feature from the issue tracker (`#5491`).

## Motivation

Today, declaring per-test-project environment variables in Microsoft.Testing.Platform requires writing custom code:

```csharp
internal sealed class MyEnvProvider : ITestHostEnvironmentVariableProvider
{
    // ~50 lines of boilerplate to read appsettings/IConfiguration and call SetVariable.
}
builder.TestHost.AddEnvironmentVariableProvider(_ => new MyEnvProvider(...));
```

This is awkward for three audiences:

1. **Users migrating from `runsettings`** lose a feature they relied on (e.g. setting `DOTNET_ENVIRONMENT`, `HEADED`, profiler variables, or app-specific knobs for tests).
2. **CI authors** want to express test environment in a declarative file checked into source control, not via shell exports that diverge between runners.
3. **Tooling integrators** (IDE Test Explorer, `dotnet test`) want a stable place to read the test host environment without parsing source.

## Proposed feature

```jsonc
{
  "environmentVariables": {
    "DOTNET_ENVIRONMENT": "Development",
    "HEADED": "1",
    "ASPNETCORE_URLS": "http://localhost:5050"
  }
}
```

A built-in `ITestHostEnvironmentVariableProvider` (`TestConfigurationEnvironmentVariableProvider`) is registered automatically by `Microsoft.Testing.Platform`. The provider is **only enabled when the section exists and contains at least one entry**, so projects that do not use this feature pay no cost.

When the provider is enabled it opts in to the **test host controller process model**: the current process becomes the controller, sets the environment variables on `ProcessStartInfo`, and launches the actual test host as a child process.

## Detailed design

### Architecture

`Microsoft.Testing.Platform` already has the abstractions needed to apply environment variables to the test host: any registered, enabled `ITestHostEnvironmentVariableProvider` causes `TestHostBuilder` to take the `TestHostControllersTestHost` code path. That host:

1. Constructs an `EnvironmentVariables` bag and walks each enabled provider's `UpdateAsync`.
2. Calls `ValidateTestHostEnvironmentVariablesAsync` on every provider to give them a chance to reject the final aggregated set.
3. Copies the result into `ProcessStartInfo.EnvironmentVariables`.
4. Spawns the test host as a child process.

The new provider plugs into step 1.

### Where the section is parsed

`JsonConfigurationFileParser` already flattens the testconfig.json tree into two dictionaries:

- `_singleValueData["environmentVariables:FOO"] = "bar"`
- `_propertyToAllChildren["environmentVariables"] = "{\"FOO\":\"bar\"}"`

A new internal method `JsonConfigurationProvider.GetSection(string sectionName)` enumerates the entries with a strict schema check (see [Schema validation](#schema-validation)). `AggregatedConfiguration.GetTestConfigJsonSection` exposes this to internal callers.

### Registration

`TestHostBuilder.SetupCommonServicesAsync` registers the provider immediately after the configuration is built and inserts it at the front of the controller ordering so later user/VSTest providers can still override:

```csharp
if (!OperatingSystem.IsBrowser())
{
    // Internal API: inserts the provider at the front of the controller ordering so any
    // later user-supplied or VSTest-bridge provider registered via the public
    // TestHostControllers.AddEnvironmentVariableProvider(...) still wins for the same key.
    testHostControllersManager.AddEnvironmentVariableProviderFirst(
        sp => new TestConfigurationEnvironmentVariableProvider(sp.GetConfiguration()));
}
```

It is registered **first** in the controller ordering so any user-supplied or extension-supplied provider runs after it and can override the testconfig.json values. This ordering choice has a concrete consequence — see [Precedence](#precedence-and-ordering).

### Provider behavior

```csharp
internal sealed class TestConfigurationEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    public Task<bool> IsEnabledAsync()
    {
        var entries = configuration.GetTestConfigJsonSection("environmentVariables");
        if (entries.Count == 0) return Task.FromResult(false); // section absent or empty
        foreach (var entry in entries) ValidateName(entry.Key); // fail fast on malformed names
        _entries = entries;
        return Task.FromResult(true);
    }

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        foreach (var entry in _entries)
        {
            environmentVariables.SetVariable(new EnvironmentVariable(
                entry.Key, entry.Value ?? string.Empty, isSecret: false, isLocked: false));
        }
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables _)
        => ValidationResult.ValidTask;
}
```

### Schema validation

The `environmentVariables` section must be a **flat JSON object whose values are scalars**. Any deviation throws `FormatException` during the build phase with a message identifying the offending key and the configuration file path. Specifically:

| JSON shape                                              | Behavior                                       |
| ------------------------------------------------------- | ---------------------------------------------- |
| `"environmentVariables": { "FOO": "bar" }`              | ✅ Sets `FOO=bar`                              |
| `"environmentVariables": { "FOO": "" }`                 | ✅ Sets `FOO=""`                               |
| `"environmentVariables": { "FOO": 42, "BAR": true }`    | ✅ Coerced to text: `FOO=42`, `BAR=True`       |
| `"environmentVariables": { "FOO": null }`               | ⚠️ Runtime-dependent (see [Edge cases](#edge-cases-and-limitations)) |
| `"environmentVariables": {}`                            | ✅ No-op; controller process model **not** triggered |
| (section absent)                                        | ✅ No-op; controller process model **not** triggered |
| `"environmentVariables": "oops"`                        | ❌ Throws – section must be a JSON object       |
| `"environmentVariables": [1, 2]`                        | ❌ Throws – section must be a JSON object       |
| `"environmentVariables": { "FOO": { "NESTED": "x" } }`  | ❌ Throws – nested objects not supported        |
| `"environmentVariables": { "FOO": [1, 2] }`             | ❌ Throws – nested arrays not supported         |
| `"environmentVariables": { "FOO": {} }`                 | ❌ Throws – nested empty objects not supported  |
| `"environmentVariables": { "": "x" }`                   | ❌ Throws – empty variable names are invalid    |
| `"environmentVariables": { "FOO=BAR": "x" }`            | ❌ Throws – `=` is not allowed in names         |

### Precedence and ordering

Within the testhost controller, providers are applied in **registration order** (`TestHostControllersManager._factoryOrdering`) and later providers can overwrite earlier ones unless the earlier entry was marked `IsLocked`. The built-in `TestConfigurationEnvironmentVariableProvider`:

- Is registered **first** (in `SetupCommonServicesAsync`, before any user code).
- Sets entries with `isLocked: false`.

Consequences:

- A user-supplied `ITestHostEnvironmentVariableProvider` registered via `builder.TestHost.AddEnvironmentVariableProvider(...)` runs **after** the built-in and can override testconfig.json values.
- The VSTest-bridge `RunSettingsEnvironmentVariableProvider` (which currently sets `isLocked: true`) wins over testconfig.json when both sources are present.

End-to-end precedence (highest wins):

1. CLI options that explicitly set the test host environment (none today, but reserved).
2. The system environment inherited by the test host parent process (the controller inherits it; values declared here override the inherited value for the child only).
3. User-registered `ITestHostEnvironmentVariableProvider` extensions (last-registered wins).
4. VSTest-bridge runsettings `<EnvironmentVariables>` (locked).
5. **`testconfig.json` `environmentVariables`** (this RFC).
6. `launchSettings.json` `environmentVariables` (in scope for the .NET 10 `dotnet test` integration, but **not implemented by this RFC**).

## Edge cases and limitations

### Discovery (`--list-tests`) does not apply env vars

`TestHostBuilder.TryBuildTestHostControllersHostAsync` returns `null` when `--list-tests` is set. As a result, testconfig.json env vars are **not** applied during discovery — discovery runs in-process. This matches existing runsettings behavior but is a divergence from execution that test framework authors should be aware of: discovery code that branches on `DOTNET_ENVIRONMENT` will not see the configured value.

### Cost of opting in

Declaring even a single entry in `environmentVariables` triggers the controller process model: the current process forks a child test host and proxies its lifecycle. We considered a fast-path that sets the variables in-process via `Environment.SetEnvironmentVariable` when no other extension requires the controller, and **rejected** it: many runtime knobs (`DOTNET_*`, profiler/loader variables, ICU/globalization settings) only take effect at process startup. Splitting behavior would create a silent foot-gun where some variables work and others do not. Users that want zero overhead should keep the section absent.

### Browser / WebAssembly

The provider is `[UnsupportedOSPlatform("browser")]` and is not registered on Wasm targets, mirroring `TestHostControllersManager.AddEnvironmentVariableProvider`. The section is silently ignored on Wasm because the controller process model does not exist there.

### Case sensitivity

`JsonConfigurationFileParser` stores keys with `StringComparer.OrdinalIgnoreCase`. As a result:

- `{"FOO": "x", "foo": "y"}` fails the JSON parse with a duplicate-key error before reaching this provider.
- Environment variable names are case-sensitive on Linux/macOS and case-insensitive on Windows. Authors targeting cross-platform should pick a single casing.

### JSON `null` values

A JSON null value is interpreted differently by the two JSON parser implementations the platform ships:

- On .NET (System.Text.Json): `{"FOO": null}` results in `FOO=""` (empty string).
- On .NET Framework / .NET Standard (Jsonite): `{"FOO": null}` results in `FOO=null` (the four-character string).

This stems from the underlying parsers and is not unique to this feature. **Recommendation:** never use JSON `null` for env var values — use an explicit empty string `""` instead.

### Secrets

Values are written to `ProcessStartInfo.EnvironmentVariables` in plain text and, when trace logging is enabled, may appear in the platform's diagnostic logs (`ConfigurationManager` traces the full config file content; `EnvironmentVariables` traces value overrides). **Do not put secrets in `testconfig.json`.** For secrets, use existing mechanisms (CI secret stores piped to the system environment, user-secrets, key vaults).

### No removal syntax

There is no way to *unset* an inherited environment variable via this section. Setting a key to `""` sets it to empty string, which is not the same as removing it. If removal becomes a desired feature in the future, we can adopt a sentinel (`null` or a documented marker) — but doing so consistently across both JSON parsers will need extra work in the configuration layer.

## Alternatives considered

### Alternative A — Read `launchSettings.json` only

Discussed in #5491. `launchSettings.json` is a development-time file; pushing test-only configuration into it ties test execution to a file that is also consulted by `dotnet run`. Furthermore, `launchSettings.json` isn't supported by `dotnet test` until .NET 10. Even when it ships, testconfig.json remains the right place to declare values that apply specifically to test execution (e.g. `HEADED=0` in CI but `HEADED=1` in launchSettings for local debugging).

### Alternative B — Always use the controller, even without env vars

We would lose the "no-cost when absent" property and force every MTP run to incur a subprocess. Rejected.

### Alternative C — Set variables in-process when no other provider requires the controller

Rejected as described in [Cost of opting in](#cost-of-opting-in) — silently dropping runtime knobs that only take effect at startup is worse than uniformly paying the controller cost.

### Alternative D — Expose enumeration on the public `IConfigurationProvider` interface

Rejected. `IConfigurationProvider` is `[Experimental("TPEXP")]` but still public; adding members to it would be a source-breaking change for third-party providers. The new enumeration lives on the internal `JsonConfigurationProvider` and is reached via `AggregatedConfiguration` casting, both of which are internal.

## Backward compatibility

- New JSON section under a previously-undefined key: no existing valid `testconfig.json` becomes invalid.
- New built-in provider in MTP: registered only when the section exists, so existing projects observe zero behavior change.
- No public API additions; no changes to `PublicAPI.Shipped.txt` or `Unshipped.txt`.

## Open questions

- Should the runsettings (VSTest bridge) provider be loosened to `isLocked: false` so that testconfig.json can also override runsettings in legacy multi-config projects? Not required by this RFC; out of scope.
- Should we expose a CLI override (e.g. `--environment KEY=VALUE`) that wins over both files? Out of scope; can be added later without breaking changes.
