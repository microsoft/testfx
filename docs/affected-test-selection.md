# Affected-test selection rollout

This repository is wired for the experimental affected-test workflow from
[dotnet/sdk#55574](https://github.com/dotnet/sdk/pull/55574). The workflow is Microsoft.Testing.Platform-only and
builds on the composable filter-provider support from
[testfx#10235](https://github.com/microsoft/testfx/pull/10235).

The repository consumes `Microsoft.Testing.Extensions.AffectedTests` from the `test-tools` feed at the same version as
`Microsoft.Testing.Extensions.CodeCoverage`, plus the provider-specific
`Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps` package. The provider publishes versioned per-module
mapping shards to the `TestFx_AffectedTestsMaps` artifact produced by pipeline definition 209. Its builder hook is
registered by the repository's hand-authored MTP entry points.

Selection remains disabled at the shared pipeline call site. Package
`18.12.0-preview.26466.2` starts collection, but its internal `--list-tests` discovery child exits successfully before
connecting to the extension's discovery pipe, so the parent reports
`Test discovery child exited with code 0 before connecting.` and fails the run. Ordinary full-test CI remains active
until a package containing a working discovery handshake is available.

## CI layout

- `global.json` defines the repository-specific `test.affectedTests` change policy and selects Azure DevOps artifact
  storage for the public `microsoft.testfx` pipeline.
- Once enabled, the trusted main-branch Windows Release test runs `--collect-test-map`.
- Once enabled, the Windows Release PR test runs `--affected-tests`.
- The package targets .NET 8 and later, so .NET Framework test modules continue to run in full before the affected-test
  step. Collection, selection, and fallback commands are scoped to .NETCoreApp modules.
- The shared Windows test call site selects the mode from the source branch, supplies Azure DevOps build identity and
  the scoped system access token, and sets `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for affected-test commands.
- `eng/validate-affected-tests.ps1` verifies the SDK gate, package reference, storage configuration, pipeline wiring,
  command names, and fallbacks.

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

Selected-test runs do not publish their partial coverage as the repository coverage report. Collection and full
fallback runs still publish complete coverage.

The one-switch rollback remains setting `enableAffectedTests` to `false`, which keeps the package and dormant storage
configuration in place while restoring the ordinary full-test command.
