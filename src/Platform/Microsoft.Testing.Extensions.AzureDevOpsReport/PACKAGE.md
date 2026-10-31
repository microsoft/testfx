# Microsoft.Testing.Extensions.AzureDevOpsReport

Microsoft.Testing.Extensions.AzureDevOpsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that reports individual test failures as errors or warnings in Azure DevOps CI builds, with file/line annotations when available.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureDevOpsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureDevOpsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Azure DevOps reporting**: emits individual CI errors/warnings for each test failure via the Azure DevOps logging commands
- **Configurable severity**: supports `--report-azdo-severity` (`error` or `warning`)
- **CI auto-detection**: detects Azure DevOps environments through the `TF_BUILD` variable
- **Live publishing**: streams test results to the Azure DevOps Tests tab while the run is still in progress (`--publish-azdo-test-results`)
- **Automatic attachments**: for failed tests, attaches stdout/stderr and any `FileArtifactProperty` data (dumps, screenshots) to the matching result in the Tests tab; uploads `*.coverage`, `*.cobertura.xml`, and `*.opencover.xml` artifacts as run-level attachments
- **Extensions-tab summary**: `--report-azdo-summary` writes and uploads a compact Markdown dashboard with a friendly title, test totals, coverage, failures, and slowest tests. With an SDK that supports required artifact post-processing, a multi-module `dotnet test` invocation uploads one authoritative overall summary with a module overview. Older SDKs preserve per-assembly summaries. This does not change live publishing to the Azure DevOps Tests tab.

The Markdown summary is independent from the self-contained HTML report produced by `--report-html`. Both options can be enabled together: Azure DevOps renders the Markdown in the Extensions tab, while the HTML remains a report file that can be published as a build artifact or displayed by a separately installed Azure DevOps web extension.

The aggregate contains one top-ten slowest-test list across modules, including failed tests and their outcomes, module names, and target frameworks. **Elapsed** is the SDK-reported run duration; each module's **test time** is the sum of reported test durations and may exceed elapsed time when tests run concurrently. Missing duration history is explicitly unavailable, not a baseline for comparison.

When the SDK supplies an authoritative final invocation exit, the aggregate shows it separately as **Runner policy**, including exit `0`. A policy such as `--ignore-exit-code 2` can accept an invocation that still contains failed tests: the test-result indicator, failed counts and failure details remain unchanged. Partial runs keep their warning; older fragments without authoritative context do not invent a policy verdict.

Skipped tests are grouped by their reported reason and execution platform, with module/target-framework context and at most ten groups shown. Issue references in reasons are retained; skipped test names are not dumped into the summary. Omitted groups are accounted for by a skipped-test count. Older summary fragments remain supported and label unavailable outcomes, skip reasons, and platform metadata explicitly.

## Usage

Enable Azure DevOps reporting with the `--report-azdo` command line option.

### Live publishing with parallel test modules

Modules using `--publish-azdo-test-results` and the same results directory can share a test run. The owner waits for registered modules to finish publishing before completing the run. Modules scheduled after completion acquire a new run, so a solution invocation can produce several runs; all modules in a shared directory must use the same extension version.

Data-driven rows use fully qualified automated test names that include the distinguishing display-name suffix, preserving each row's own display name and duration in the Tests tab. Non-parameterized tests with the default display name keep their existing automated identity. The publisher also records versioned canonical-name metadata in the result comment after creation so history-based annotations and slow-test thresholds continue to aggregate by the fully qualified method name without guessing from display names. Results without this metadata retain exact-name history matching for compatibility. Duration samples are retained in the order returned by history loading, across both rows and legacy method results, before the per-method sample cap is applied.

If shutdown interrupts a metadata update after Azure DevOps accepts a result, the final flush retries only the result ID and canonical comment. It does not republish the result or change its outcome, diagnostics, or retry attempts. Unsaved metadata is reported as a warning.

Coordination uses an exclusive handle to an `azdo-runid.<buildId>.lock` file. This file remains in the results directory, but its existence does not indicate an active run: the operating system releases the handle when the owning process exits. The file also records pending cleanup so later modules retry failed coordination-file deletions before acquiring a new run, rather than joining a completed run or waiting for its owner lease to expire. Do not remove coordination files while modules are running. Per-project results directories disable sharing between projects.

Attachments up to 75 MiB per file are supported. This limit applies to the raw file; base64 encoding expands a 75 MiB attachment to 100 MiB, matching the Azure Pipelines agent's attachment guards. Attachments larger than 75 MiB or files that cannot be read are reported as upload failures without failing otherwise successful tests.

Under native `dotnet test`, live-publishing warnings require an SDK that negotiates Microsoft.Testing.Platform protocol 1.3.0 or later. Older SDKs, including 10.0.102, do not display these host warnings. Use `--diagnostic` and preserve the diagnostic logs to investigate publishing failures with those SDKs.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport): TRX report generation for standardized test result files

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#azure-devops-reports>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
