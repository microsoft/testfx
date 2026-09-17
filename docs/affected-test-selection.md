# Affected-test selection rollout

This repository uses the experimental affected-test workflow from
[dotnet/sdk#55574](https://github.com/dotnet/sdk/pull/55574). The workflow is Microsoft.Testing.Platform-only and
builds on the composable filter-provider support from
[testfx#10235](https://github.com/microsoft/testfx/pull/10235).

The repository consumes `Microsoft.Testing.Extensions.AffectedTests` from the `test-tools` feed at the same version as
`Microsoft.Testing.Extensions.CodeCoverage`. The extension's local storage keeps mapping shards under
`.cts/mappings`, and Azure Pipelines `Cache@2` transfers that directory between trusted main builds and PR builds.
Ordinary test commands remain unchanged outside the affected-test CI path.

## CI layout

- `global.json` defines the repository-specific `test.affectedTests` change policy and local storage.
- The trusted main-branch Windows Release test runs `--collect-test-map`.
- The Windows Release PR test runs `--affected-tests`.
- The package targets .NET 8 and later, so .NET Framework test modules continue to run in full before the affected-test
  step. Collection, selection, and fallback commands are scoped to .NETCoreApp modules.
- The shared Windows test call site selects the mode from the source branch, restores the map through Azure Pipelines
  `Cache@2`, and sets `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for the affected-test commands.
- `eng/validate-affected-tests.ps1` verifies the SDK gate, package reference, storage configuration, cache wiring,
  command names, and fallbacks.

`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension authorization marker. Repository scripts and pipeline
definitions must not set it.

## Storage design

The map uses the extension's local-filesystem provider rooted at `.cts/mappings`. Azure Pipelines `Cache@2` transfers
that directory between runs without credentials:

- trusted main builds can restore the previous map and publish a new immutable cache entry;
- PR and fork-PR builds can read the target branch's cache scope but cannot write to it;
- the cache prefix includes its manual compatibility version, OS, architecture, and configuration;
- the unique build ID suffix lets every successful main collection publish a new map;
- prefix restore selects the newest compatible map.

Azure Pipelines caches expire after seven days without activity. A cache miss is therefore an expected state, not a
test failure: the PR lane runs the unchanged full test command. The same fallback runs when the extension rejects a
missing, stale, or incompatible map, and scheduled or manual builds always keep full validation.

Selected-test runs do not publish their partial coverage as the repository coverage report. Collection and full
fallback runs still publish complete coverage.

Pipeline artifacts should contain only non-secret diagnostics or a mapping snapshot suitable for troubleshooting.
They are not the cross-run source of truth because artifact lookup and retention are tied to individual builds.

Update `affectedTestsCacheVersion` whenever the persisted map format or its compatibility dimensions change. The
one-switch rollback remains setting `enableAffectedTests` to `false`, which keeps the package and dormant storage
configuration in place while restoring the ordinary full-test command.
