# Affected-test selection rollout

This repository is prepared to adopt the experimental affected-test workflow from
[dotnet/sdk#55574](https://github.com/dotnet/sdk/pull/55574). The workflow is Microsoft.Testing.Platform-only and
builds on the composable filter-provider support from
[testfx#10235](https://github.com/microsoft/testfx/pull/10235).

The rollout is intentionally disabled. SDK `11.0.100-rc.1.26406.108` contains the affected-test commands but fails
`dotnet tool restore` on clean agents because it predates
[dotnet/sdk#55595](https://github.com/dotnet/sdk/pull/55595). The repository remains on the stable SDK until a fixed
daily is published. The affected-test extension package and its public local-filesystem storage contract are also not
available yet. Ordinary test commands therefore remain unchanged.

## Prepared layout

- `global.json` defines the repository-specific `test.affectedTests` change and instrumentation scopes.
- The trusted main-branch Windows Release test is the future `--collect-test-map` entry point.
- The Windows Release PR test is the future `--affected-tests` entry point.
- Both pipeline call sites pass `enableAffectedTests: false`. The inactive template branches restore the map through
  Azure Pipelines `Cache@2` and set `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for the affected-test commands.
- `eng/validate-affected-tests.ps1` protects the disabled state and verifies that the public SDK gate and command names
  do not drift.

`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension authorization marker. Repository scripts and pipeline
definitions must not set it.

## Storage design

The map should use the extension's local-filesystem provider rooted at
`$(Pipeline.Workspace)\affected-test-map`. Azure Pipelines `Cache@2` transfers that directory between runs without
credentials:

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

The `storage` property is deliberately absent from `test.affectedTests` until the extension package publishes the exact
local-filesystem provider schema. Adding an invented provider or path setting now would create configuration that
cannot be validated.

## Activation checklist

1. Update `global.json` to an SDK newer than `11.0.100-rc.1.26406.108` that contains dotnet/sdk#55595, then validate
   `dotnet tool restore` on a clean agent.
2. Add the publicly available affected-test extension package through `Directory.Packages.props` and the test project
   infrastructure, following the repository's normal dependency-flow and package-source policy.
3. Add `test.affectedTests.storage` using the package's published local-filesystem schema and point it at the Pipeline
   Cache directory.
4. Update `affectedTestsCacheVersion` whenever the persisted map format or its compatibility dimensions change.
5. Enable `collect` in the main-branch cache-seed call site and publish non-secret diagnostics as an Azure DevOps
   artifact.
6. After a compatible map exists, enable `run` in the PR call site. Keep the full test command available as an explicit
   rollback by setting `enableAffectedTests` back to `false`.
7. Validate a documentation-only change, a product change with a narrow affected set, a force-all change, a missing or
   incompatible map, a fork PR without secrets, and a collection failure before making selection required.
