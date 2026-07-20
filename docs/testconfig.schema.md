# testconfig.json JSON schema

[`testconfig.schema.json`](./testconfig.schema.json) is the JSON Schema (draft-07) for the
`testconfig.json` configuration file consumed by Microsoft.Testing.Platform (MTP) and MSTest.

It covers:

- the MSTest section (`mstest:*` — `timeout`, `execution` (including `orderTestsByNameInClass`),
  `parallelism`, `output`, `deployment`, `assemblyResolution`);
- the Microsoft.Testing.Platform host section (`platformOptions:*`);
- the `environmentVariables` section.

The schema is the source for IDE auto-completion and validation when authoring `testconfig.json`.

## Referencing the schema

Editors that honor [SchemaStore](https://www.schemastore.org) (Visual Studio, VS Code, Rider, …)
pick the schema up automatically once the SchemaStore catalog entry below is published, because the
file is named `testconfig.json` (or `<AssemblyName>.testconfig.json`).

You can also opt in explicitly by adding a `$schema` property to your file:

```json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testconfig.schema.json",
  "mstest": {
    "parallelism": {
      "enabled": true,
      "scope": "method"
    }
  }
}
```

## Publishing to SchemaStore

SchemaStore does not need to host the schema body — it can reference a schema hosted in this
repository. To register the schema, open a PR against
[`SchemaStore/schemastore`](https://github.com/SchemaStore/schemastore) that adds the following
entry to the `schemas` array in
[`src/api/json/catalog.json`](https://github.com/SchemaStore/schemastore/blob/master/src/api/json/catalog.json)
(entries are kept alphabetically by `name`):

```json
{
  "name": "MSTest testconfig.json",
  "description": "Configuration file for MSTest and Microsoft.Testing.Platform.",
  "fileMatch": ["testconfig.json", "*.testconfig.json"],
  "url": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testconfig.schema.json"
}
```

SchemaStore also runs the schema through its own test suite, so keep the file valid draft-07 and
make sure the sample `testconfig.json` files in the repo continue to validate.

## How versioning works

There are two layers of versioning to keep in mind:

1. **The schema dialect** — declared by the top-level `$schema` keyword inside
   `testconfig.schema.json` (currently `http://json-schema.org/draft-07/schema#`). SchemaStore
   recommends draft-07 for the broadest editor support; only change it if every consuming editor
   supports the newer dialect.

2. **The schema content** — `testconfig.json` is **not** itself versioned (there is no `version`
   key in the file). Because the URL referenced by SchemaStore points at the `main` branch, the
   schema is **evolved in place**: when a new `mstest:*` or `platformOptions:*` key ships, update
   `testconfig.schema.json` in the same PR and the change is live for everyone as soon as it merges
   to `main`. No SchemaStore change is required for content updates — only the one-time catalog
   registration above.

   This "rolling latest" model is the norm for configuration schemas whose options only grow and
   stay backward compatible. If we ever needed to expose multiple frozen versions side by side,
   SchemaStore supports a `versions` object in the catalog entry that maps a version string to a
   pinned schema URL, for example:

   ```json
   "versions": {
     "v4": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testconfig.schema.json"
   }
   ```

   We do not use that today; the single rolling `main` URL is sufficient.

## Keeping the schema in sync

The schema must track the keys actually read by the configuration parsers. The source of truth is:

- `MSTestSettings.SetSettingsFromConfig` and the `Parse*Setting` helpers in
  [`src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs`](../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs);
- `MSTestAdapterSettings.ToSettings(IConfiguration)` and `ReadAssemblyResolutionPath` in
  [`src/Adapter/MSTestAdapter.PlatformServices/Services/MSTestAdapterSettings.cs`](../src/Adapter/MSTestAdapter.PlatformServices/Services/MSTestAdapterSettings.cs);
- `RunConfigurationSettings.SetRunConfigurationSettingsFromConfig` in
  [`src/Adapter/MSTestAdapter.PlatformServices/RunConfigurationSettings.cs`](../src/Adapter/MSTestAdapter.PlatformServices/RunConfigurationSettings.cs);
- `PlatformConfigurationConstants` in
  [`src/Platform/Microsoft.Testing.Platform/Configurations/PlatformConfigurationConstants.cs`](../src/Platform/Microsoft.Testing.Platform/Configurations/PlatformConfigurationConstants.cs).

When you add, rename, or remove a configuration key in any of those, update
`testconfig.schema.json` in the same change.

## Value constraints worth calling out

### `timeout:*` keys must be strictly positive

The `mstest:timeout:*` keys (`test`, `assemblyInitialize`, `assemblyCleanup`, `classInitialize`,
`classCleanup`, `testInitialize`, `testCleanup`, `globalTestInitialize`, `globalTestCleanup`) are
timeouts expressed in **milliseconds** and must
be a **strictly positive integer** (`>= 1`). This is enforced identically by both the
`testconfig.json`/JSON parser (`ParseTimeoutSetting`) and the legacy `.runsettings` XML parser, so
behavior is the same across formats and matches the schema's `"minimum": 1`.

A value of `0` (or any negative number) is **not** a valid timeout. It does not mean "disable the
timeout" — instead it is rejected, a warning is logged, and the key is ignored:

```text
Invalid value '0' for runsettings entry 'timeout:test'. The timeout must be a strictly positive
integer (in milliseconds); a value of 0 or less is not allowed. Omit the entry to use the default
(no timeout). The setting will be ignored.
```

> **Note on the warning text:** when the value comes from `testconfig.json`, the warning currently
> reports the key **without** the `mstest:` prefix (for example `timeout:test`, not
> `mstest:timeout:test`). When correlating a warning with your `testconfig.json`, mentally prepend
> `mstest:` — the JSON key you actually set is `mstest:timeout:test`. The `.runsettings` XML parser
> reports the corresponding element name instead (for example `TestTimeout`).

To run without a timeout, **omit the key** rather than setting it to `0`; absence of the key already
means "no timeout" (the internal default). Because an explicit `0` would be redundant with omitting
the key, it is treated as invalid input to surface likely mistakes (for example, a templated value
that resolved to `0`).

### Global test fixture timeouts fall back to the per-test keys

`[GlobalTestInitialize]` and `[GlobalTestCleanup]` methods honor their own dedicated
`timeout:globalTestInitialize` / `timeout:globalTestCleanup` keys (RunSettings XML:
`GlobalTestInitializeTimeout` / `GlobalTestCleanupTimeout`). When a dedicated key is **not** set,
they fall back to the corresponding per-test `timeout:testInitialize` / `timeout:testCleanup` key so
existing configurations keep applying. As always, a method-level `[Timeout(...)]` attribute takes
precedence over any config value.
