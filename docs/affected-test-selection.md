# Affected-test selection rollout

This repository is wired for the experimental affected-test workflow from
[dotnet/sdk#55574](https://github.com/dotnet/sdk/pull/55574). The workflow is Microsoft.Testing.Platform-only and
builds on the composable filter-provider support from
[testfx#10235](https://github.com/microsoft/testfx/pull/10235).

The repository consumes `Microsoft.Testing.Extensions.AffectedTests` from the `test-tools` feed together with the
provider-specific `Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps` package. Their shared version is
pinned separately from `Microsoft.Testing.Extensions.CodeCoverage`, allowing the shipping CodeCoverage dependency to
remain on a stable release while affected-test development continues on preview builds. Test applications that
register the affected-test provider override their direct CodeCoverage reference to that matching preview version;
the packages shipped by this repository retain the stable central version. The provider publishes versioned
per-module mapping shards to the `TestFx_AffectedTestsMaps` artifact produced by pipeline definition 209. Its builder
hook is registered by the repository's hand-authored MTP entry points.

Package `18.12.0-preview.26479.4` includes the fixed JSON discovery path and Azure DevOps upload request, and ships
both `netstandard2.0` and `net8.0` assets for the base extension, collector, CodeCoverage extension, and Azure DevOps
provider.

Affected-test execution remains temporarily disabled in the pipeline. SDK `12.0.100-alpha.1.26480.103` predates the
VMR flow containing [dotnet/sdk#56450](https://github.com/dotnet/sdk/pull/56450), so successful collection
applications can exit `0` while the parent `dotnet test` command reports handshake failures and exits `1`. That
output is indistinguishable from controller-only or partial handshake failures that must remain failures, so the
pipeline cannot safely normalize it. Keep the packages and storage configuration dormant until a newer .NET 12 SDK
containing the fix is published and verified in this repository.

## CI layout

- `global.json` defines the repository-specific `test.affectedTests` change policy and selects Azure DevOps artifact
  storage for the public `microsoft.testfx` pipeline.
- Once re-enabled, the trusted main-branch Windows Release test runs `--collect-test-map` without the normal report,
  retry, or coverage arguments, because collection owns its instrumentation and launches discovery
  children with `--list-tests`. A normal full test run follows to retain test reporting and coverage.
- Once re-enabled, the Windows Release PR test runs `--affected-tests`.
- Affected-test collection and selection cover all repository test TFMs, including .NET Framework through the
  package's `netstandard2.0` assets. The build restores the collector's required x64 native files beneath
  `runtimes/win-x64/native`, because NuGet otherwise flattens them for .NET Framework outputs.
- The shared Windows test call site selects the mode from the source branch, supplies Azure DevOps build identity and
  the scoped system access token, and sets `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for affected-test commands.
- The test infrastructure removes the ambient affected-test mode and per-module exit-code normalization from nested
  test applications launched by acceptance tests, while still allowing an individual test to opt in explicitly.
- `eng/validate-affected-tests.ps1` verifies the SDK gate, package reference, storage configuration, pipeline wiring,
  nested-process isolation, command names, and fallbacks.

`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension authorization marker. Repository scripts and pipeline
definitions must not set it.

## Storage design

The map uses the extension's Azure DevOps artifact provider:

- trusted main builds publish each module shard beneath the `TestFx_AffectedTestsMaps` artifact;
- PR builds query successful builds from definition 209 and select the newest artifact whose baseline commit is an
  ancestor of the PR checkout;
- fork PRs receive no access token, so unavailable storage safely selects all tests rather than exposing credentials;
- mapping identity includes the repository, module, target/runtime, test metadata, policy, and storage compatibility
  fields, so incompatible shards are rejected conservatively.

A missing, stale, incompatible, or inaccessible artifact is an expected state, not a partial selection: the extension
runs all tests. The pipeline's explicit full-test fallback remains for extension/process failures, while scheduled and
manual builds always keep full validation.

Selected-test runs do not publish their partial coverage as the repository coverage report. The normal full run
after collection and full fallback runs still publish complete coverage.

The one-switch rollback is currently active: `enableAffectedTests` is `false`, which keeps the package and dormant
storage configuration in place while restoring the ordinary full-test command.
