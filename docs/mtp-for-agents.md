# Microsoft.Testing.Platform for agents and CI tools

Prefer complete diagnostic capture with selective retrieval over shortening or
reformatting every result. This guide is for applications consuming
Microsoft.Testing.Platform (MTP); repository contributors should instead follow
the [contribution instructions](../.github/copilot-instructions.md) and
[development guide](dev-guide.md).

## Choose the execution path first

| Path | How it is selected | Failure details and arguments |
| --- | --- | --- |
| VSTest | A VSTest test project running in VSTest mode. | Use VSTest's options and loggers, not MTP's `--report-*` options. |
| MTP through the legacy MSBuild wrapper | `TestingPlatformDotnetTestSupport=true` redirects the VSTest target to the MTP executable. | Output is captured by default. Use `-p:TestingPlatformShowTestsFailure=true` for per-test failures, or `-p:TestingPlatformCaptureOutput=false` for full platform output. Pass test-application arguments after `--`. |
| Native MTP `dotnet test` | .NET SDK 10 or later, MTP 1.7 or later, and `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`. | Use native selectors such as `--project`. The legacy capture properties are not used. Keep `--` before test-application arguments to avoid collisions with SDK options. |

The legacy wrapper is for older SDKs; MTP 2.x rejects that path with the .NET 10
SDK or later. See [runner selection and migration](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
for supported combinations. A project's framework, registered extensions, SDK
and runner mode all affect which options it accepts. Do not assume that every
project in a solution accepts the same framework or reporter options.

## Use explicit output preferences

For an MTP application using version 2.3 or later, a native invocation can use:

```powershell
dotnet test --project .\Tests\Tests.csproj --no-ansi --no-progress -- --no-banner --show-stdout failed --show-stderr failed
```

In native mode, the SDK renders the terminal output. Its `--no-ansi` and
`--no-progress` options belong before `--`; forwarding the MTP host's `--ansi off`
and `--progress off` after the separator does not configure the SDK renderer.
See the [native SDK options](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp#options).

For a supported legacy-wrapper invocation, use:

```powershell
dotnet test .\Tests\Tests.csproj -p:TestingPlatformShowTestsFailure=true -- --ansi off --progress off --no-banner --show-stdout failed --show-stderr failed
```

Explicit preferences are more reproducible than inferring behavior from an
agent's inherited environment variables. Disabling progress also disables useful
live progress feedback; retain it when investigating slow or hung tests. For
older applications, inspect `--help`: the legacy `--no-ansi` and `--no-progress`
options predate the positive `--ansi` and `--progress` options.

MTP 3 removes the application's `--no-ansi` and `--no-progress` aliases; use
`--ansi off` and `--progress off` for direct application and legacy-wrapper
invocations. The native SDK's separately owned options above are unaffected.
Build runners must migrate their forwarded application arguments too; see the
[migration boundaries and delivery prerequisite](decisions/legacy-cli-migrations.md).

| Behavior | MTP 2.3 through 2.x | MTP 3.0 development |
| --- | --- | --- |
| Captured stdout/stderr default | `Failed` in detected agent environments; `All` otherwise. | `Failed` in all environments. |
| Agent detection | Suppresses the banner and defaults to non-ANSI output, in addition to the captured-output preference. | Still affects banner and ANSI behavior. |
| Progress | Independently controlled by progress options. | Independently controlled by progress options. |

The 3.0 column describes upcoming changes on this repository's main branch, not
an already shipped 2.x contract. Consult the [platform changelog](Changelog-Platform.md)
and the actual application's `--info`. For the MTP host, a nonempty `NO_COLOR`
disables ANSI unless explicitly overridden with `--ansi on`; it does not disable
progress.

Starting with MTP 2.4, `--show-test-results failed` selects failure blocks, and
`--output minimal` is another failure-only preset. `--show-test-results` takes
precedence over `--output`; these filters do not change execution or summary
counts. To investigate passing or skipped test output, request both the relevant
result blocks and `--show-stdout all --show-stderr all`.
See [terminal output](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-terminal-output)
and [environment configuration](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-config).

## Capture once, retrieve what matters

Keep the command's exit code and full diagnostic log before extracting a summary.
If a command tool clips its output, retrieve the log from its reported location;
do not rerun a test just to recover already captured details. A first-line
failure index is a navigation aid, not a replacement for inner exceptions,
expected/actual values, output or stack traces.

TRX and CTRF are opt-in extensions, not built-in switches. Reference and register
the corresponding package, or enable it through a supported test SDK
configuration. Only pass a report option to projects that register it. For a
native application with the TRX extension registered:

```powershell
$results = Join-Path .\TestResults ([Guid]::NewGuid().ToString('N'))
dotnet test --project .\Tests\Tests.csproj -- --report-trx --results-directory $results
```

Use a fresh results directory for each invocation so stale artifacts cannot be
mistaken for current results. For an application registering CTRF, use
`--report-ctrf` instead. CTRF is available as an alpha package in the 2.x release
period; its graduation and platform-aligned version are staged for 3.0. Consult
the [CTRF package documentation](../src/Platform/Microsoft.Testing.Extensions.CtrfReport/PACKAGE.md)
for the schema and version-specific identity/retry guarantees.

When consuming a CTRF document, select failures before sending data to a model.
For example, with `$reportPath` pointing to the chosen final report:

```powershell
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$report.results.tests |
    Where-Object { $_.status -eq 'failed' } |
    Select-Object testId, executionId, name, status, message, trace |
    ConvertTo-Json -Depth 10
```

Retain the original document and terminal log for drill-down. Reports can bound
diagnostic strings or omit information that a framework did not provide.
Preserve module/TFM, framework UID, data-row and attempt identity when associating
results. A display name is not necessarily an FQN, a stable cross-run identity or
a rerun filter. Read the final consolidated retry report rather than counting
both it and its input attempt reports. Do not treat an earlier failed attempt as
a final failure after a successful retry.

Use the actual application's discovery and help output when choosing rerun
filters. `--list-tests json` provides structured discovery, not execution
results. A direct MTP executable reserves stdout for that document; an outer
build command or multi-module runner can add other output. Framework-specific
filters such as MSTest's `--filter` are not universal.

## Use OTel for live observability

The [OpenTelemetry extension](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-open-telemetry)
exports completed test spans while a suite continues, subject to exporter
batching. It can correlate test failures with application HTTP/SQL traces. An
application-owned exporter or local Collector does not require a hosted service.
Server-mode JSON-RPC is another option for tools that own interactive execution;
see the [client documentation](../src/Platform/Microsoft.Testing.Platform.ServerMode.Client.Sources/PACKAGE.md).

Normal span export does not immediately publish test starts or events added to a
still-running span. Telemetry is not automatically a complete results ledger:
sampling, saturated queues, attribute limits and interrupted flushing can lose
information. Use persisted reports and process/completion status where
completeness matters.

Review exporter destinations and sensitive fields before enabling export.
`TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT=0` omits captured stdout/stderr; it does
not redact test names, UIDs, paths, metadata or exceptions.
`TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT` bounds selected diagnostic
strings to 8,192 characters by default, not bytes and not every attribute.
Truncation is a size guard, not redaction. Apply appropriate filtering in the
application or Collector.

## Keep failure and completeness signals

MTP already has [coarse exit codes](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes):
for example, `2` for failed tests, `5` for invalid arguments, `8` for a zero-test
verdict, `9` for an unmet minimum, and `10` for framework infrastructure failure.
An outer SDK/MSBuild command can map child exits to its own result; retain both
the outer status and child diagnostic where available.

Do not infer success from an absent failure block, a truncated report, or exit
zero alone. Check that the intended tests actually executed, that the invocation
completed, and that reported counts and identities match the selection. Avoid
ignoring failure/zero-test exit codes merely to make an agent loop continue.
Keep cancellation, crashes, missing modules and skipped tests distinct from
passing tests.
