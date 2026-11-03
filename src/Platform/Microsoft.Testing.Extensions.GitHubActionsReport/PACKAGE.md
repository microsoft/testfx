# Microsoft.Testing.Extensions.GitHubActionsReport

Microsoft.Testing.Extensions.GitHubActionsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that emits GitHub Actions-native workflow commands so test runs on GitHub Actions produce a first-class experience.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.GitHubActionsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.GitHubActionsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Per-assembly log groups**: emits `::group::` / `::endgroup::` workflow commands so each test assembly's output is collapsed by default in the runner UI
- **Failure annotations**: emits an `::error` workflow command for each failing test so failures appear in the workflow Annotations tab and, when the source location can be resolved, on the pull request's "Files changed" diff gutter. Skipped tests are surfaced as `::warning` annotations so they are visible in the Annotations tab too. When the test session completes with a non-test-result failure — a `--minimum-expected-tests` violation, a run that discovered zero tests, a `--maximum-failed-tests` stop, a deadline-triggered early stop, or a test-adapter session failure — a single run-level `::error` is emitted describing the [Microsoft.Testing.Platform exit code](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes)
- **Job summary**: writes one markdown roll-up (totals, failures, coverage, slowest tests) to the file pointed to by `GITHUB_STEP_SUMMARY`, which GitHub renders on the workflow run summary page. Use `--report-gh-step-summary-sections` to select `test-results`, `coverage`, `slow-tests`, or any combination; `all` keeps every currently supported section and is the default. With an SDK that supports required artifact post-processing, multi-module `dotnet test` runs produce one authoritative overall section using the SDK's outer duration and exit verdict, with deterministic per-assembly details underneath. Older SDKs preserve the per-assembly sections. A non-test-result failure exit code is called out so a failure is not hidden behind a green ✅
- **Slow-test notices**: emits a `::notice` workflow command for any test still running past a threshold (default 60 seconds)
- **Test history**: reads and updates a bounded JSON snapshot so failures can include prior pass/fail context. GitHub Actions artifact download/upload remains a workflow responsibility, keeping GitHub credentials out of the test process

Job-summary test names retain their fully qualified method names and include framework-provided data-row arguments or custom display names, distinguishing individual cases in the failures, flaky tests, and slowest tests sections.

> [!NOTE]
> The exit-code callout and run-level annotation only cover outcomes the extension can observe once the in-process test session has finished. Those are: `ZeroTests` (8), `MinimumExpectedTestsPolicyViolation` (9), `TestAdapterTestSessionFailure` (10), `TestExecutionStoppedForMaxFailedTests` (13), and `TestExecutionStoppedAtDeadline` (15). `AtLeastOneTestFailed` (2) is already conveyed by the per-test failures, so it gets no separate callout. A hard abort/cancellation (`TestSessionAborted`, 3) short-circuits end-of-session reporting, and codes raised before or after the session — e.g. `InvalidCommandLine` (5) or `TestHostProcessExitedNonGracefully` (7) — occur outside the extension's reach, so none of those are surfaced here.
>
> Cross-module aggregation is negotiated with `dotnet test`. If the SDK does not provide the authoritative run-summary context, the extension keeps its standalone behavior; a manually invoked post-processor labels totals as observed and leaves overall duration and exit verdict unavailable rather than reconstructing them.

When authoritative context is available, the aggregate labels the final invocation outcome and exit separately as **Runner policy**, including exit `0`. Exit-code policies do not rewrite failed test results or remove their details. The existing `on-failure` behavior retains visibility for actual failed tests and partial runs even when runner policy accepts the invocation; it is not an exit-code-only suppression switch.

### Running through `dotnet test`

Log groups, failure and skip annotations, and slow-test notices are forwarded unchanged through the SDK's informational display-message channel. This requires an SDK that negotiates Microsoft.Testing.Platform protocol 1.3.0 or later. The Markdown job summary is written independently and does not require that channel.

Older SDKs, including .NET SDK 10.0.402 (protocol 1.0.0), cannot receive these workflow commands. Use a newer SDK that supports protocol 1.3.0 or run the built test executable directly to get groups, annotations, and notices. The extension does not send unsupported messages to an older SDK.

### How the annotation source location is resolved

An annotation is pinned to `file:line` using, in order:

1. The first frame of the failure's exception stack trace that resolves to an existing file under the workspace (`GITHUB_WORKSPACE`, or the enclosing git repository when that variable is absent). This pinpoints the failing statement, and requires the test assembly to be built with debug symbols available at run time.
2. The location the test framework reported for the test itself (the platform's `TestFileLocationProperty`). This is used when no stack frame resolves — including for skipped tests, which never carry a stack trace — and pins the annotation to the test's declaration instead.

If neither is available, a title-only annotation is emitted; it still shows in the workflow Annotations tab, just not on the diff gutter. The second source is populated by the MSTest adapter and by the VSTest bridge (from `TestCase.CodeFilePath`), so xUnit, NUnit and other bridged frameworks are covered whenever they supply source information for their tests.

The extension activates when the test run is on GitHub Actions (`GITHUB_ACTIONS=true`) and the `--report-gh` switch is passed; it no-ops otherwise. When active, each feature is enabled by default and can be toggled individually:

| Option | Description | Default |
| --- | --- | --- |
| `--report-gh` | Master switch that turns the extension on (required, in addition to running on GitHub Actions) | off |
| `--report-gh-groups on\|off` | Per-assembly log groups | on |
| `--report-gh-annotations on\|off` | Failure and skip annotations | on |
| `--report-gh-attachments <directory>` | Stage failed-test attachments and an association manifest for workflow upload | off |
| `--report-gh-step-summary on\|off\|on-failure` | Markdown job summary; `on-failure` writes it only when the test invocation fails | on |
| `--report-gh-step-summary-sections <section>...` | Job-summary content: `test-results`, `coverage`, `slow-tests`, or `all`; accepts repeated, space-separated, and comma-separated values | `all` |
| `--report-gh-failure-details on\|off` | Expand each failed test in the job summary into a collapsible section carrying its failure message, exception type, source location and stack trace | on |
| `--report-gh-history <path>` | Read and update the test history snapshot at the specified local path | off |
| `--report-gh-history-window <days>` | Retain and use 1–90 days of history | 30 |
| `--report-gh-slow-test-notices on\|off` | Slow-test notices | on |
| `--report-gh-slow-test-threshold <duration>` | Time before a slow-test notice is emitted; accepts a bare number of seconds or a unit suffix such as `90s`, `2m`, `1.5h` | 60s |

### Publish failed-test attachments

Pass `--report-gh-attachments <directory>` with an **absolute directory path** to copy attachments declared by failed
tests into a staging directory. This works with MSTest's `TestContext.AddResultFile`
and frameworks using the VSTest bridge when they supply attachments. Passing,
skipped, discovered, and superseded in-process retry results are not collected.
Session-level artifacts, including crash and hang dumps, are outside this option.

The extension does not authenticate to GitHub or upload files. Use the official
artifact action, followed by a summary step rendering per-test details with its
actual `artifact-url`.
The original test summary remains available if either step is skipped or fails.
Failure details list paths inside the archive; GitHub does not provide supported
links to individual archive members.

The post-upload example below adds a separate attachment-details section with
download links for up to 20 failed-test groups and a bounded summary size. Each
link downloads the archive; the adjacent paths identify that test's files. The
full manifests retain associations beyond the displayed groups.

> [!WARNING]
> Attachments can contain sensitive data. Opt in only after reviewing what your
> tests attach: uploaded artifacts are accessible to people with repository read
> access. Choose an appropriate retention period.

```yaml
steps:
  - name: Run tests
    run: dotnet test --report-gh --report-gh-attachments "${{ runner.temp }}/mtp-attachments"

  - name: Upload failed-test attachments
    id: attachments
    if: always() && !cancelled()
    uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
    with:
      name: test-attachments-${{ github.job }}-${{ strategy.job-index }}-${{ github.run_attempt }}
      path: ${{ runner.temp }}/mtp-attachments
      if-no-files-found: ignore
      retention-days: 7

  - name: Link failed-test attachments
    if: always() && steps.attachments.outputs.artifact-url != ''
    shell: pwsh
    env:
      ATTACHMENT_URL: ${{ steps.attachments.outputs.artifact-url }}
      ATTACHMENT_ROOT: ${{ runner.temp }}/mtp-attachments
    run: |
      $summary = [Text.StringBuilder]::new()
      [void] $summary.Append("## Failed-test attachments`n`n[Download archive and full manifests]($env:ATTACHMENT_URL)`n`n")
      $shown = 0
      $omitted = 0
      foreach ($file in Get-ChildItem -LiteralPath $env:ATTACHMENT_ROOT -Filter attachments.json -Recurse -File | Sort-Object FullName) {
        $module = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        foreach ($test in $module.tests) {
          $attachments = @($test.attachments | Where-Object status -CEQ staged)
          if ($attachments.Count -eq 0) { continue }
          $name = "$($module.assemblyName) ($($module.targetFramework)/$($module.architecture), attempt $($module.attemptNumber)): $($test.fullyQualifiedName) - $($test.displayName)"
          if ($name.Length -gt 512) { $name = $name.Substring(0, 509) + '...' }
          $name = [Net.WebUtility]::HtmlEncode($name)
          $entry = "<details>`n<summary><code>$name</code></summary>`n`n[Download attachments]($env:ATTACHMENT_URL)`n`n"
          foreach ($attachment in $attachments) {
            $path = [Net.WebUtility]::HtmlEncode($attachment.relativePath)
            $entry += "- <code>$path</code> ($($attachment.sizeBytes) bytes)`n"
          }
          $entry += "`n</details>`n`n"
          if ($shown -ge 20 -or [Text.Encoding]::UTF8.GetByteCount($summary.ToString() + $entry) -gt 400KB) {
            $omitted++
            continue
          }
          [void] $summary.Append($entry)
          $shown++
        }
      }
      if ($omitted -gt 0) { [void] $summary.Append("$omitted additional attachment groups are indexed in the manifests.`n") }
      [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $summary.ToString())
```

For .NET SDK 8 or 9, add `--` before the platform options. The summary step above
uses PowerShell 7, which is available on GitHub-hosted runners; choose an installed
shell on self-hosted runners. `upload-artifact` v4+ is not supported on GitHub
Enterprise Server; use an uploader supported by that server and only publish a
download link when that uploader supplies one.

Each test session writes a unique directory containing staged files and an
`attachments.json` manifest (schema version 1). The manifest identifies the
assembly, target framework, architecture, session, retry attempt, and each
test's UID, fully qualified name, and display name. Each attachment records its
sanitized file name, archive-relative path, byte size, and status. Only `staged`
entries have a path. Missing or unreadable files and count/size limits are reported
as warnings without changing the test verdict. Successful copies survive cleanup
of the original files.

Staging is limited to 25 MiB per file, 250 MiB and 1,000 attachment entries per
module/session, and 10 files per failed test. Unavailable entries and per-test
overflow markers also consume the entry budget, bounding retained diagnostics
even when no source files are readable. After that budget is exhausted, later
tests are omitted from the manifest and its `truncated` flag is set.
Files are staged independently of
the summary's 20-failure display limit; consult the manifests for failures not
expanded in the summary. The manifest is the authoritative index: archive paths
appear only for expanded failures whose details fit the summary byte budget.
Earlier process-retry attempts can leave their own
files and manifests in the archive; final failure details only reference the
final attempt. Disabling failure details hides archive paths in the summary but
does not disable staging.

Use a fresh staging directory for each workflow run. Multiple modules and TFMs
can share it, but upload it once per job rather than repeatedly updating the same
immutable artifact. The workflow owns staging-directory cleanup and retention.
Cancellation propagates; a hard-killed test host may leave partial output without
a complete manifest.

### Persist test history with workflow artifacts

The extension intentionally reads and writes only a local snapshot. The workflow owns GitHub authentication and artifact transfer. This keeps `GITHUB_TOKEN` out of the test process and allows pull request runs to consume history without granting them write access. Snapshots retain at most 1,000 samples per test and the latest 10,000 samples overall, in addition to the configured age window.

The following steps restore the newest default-branch snapshot, run tests, and publish the updated snapshot only from the default branch:

```yaml
permissions:
  actions: read
  contents: read

concurrency:
  group: mtp-test-history-${{ github.event_name == 'push' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch) && 'writer' || github.run_id }}
  cancel-in-progress: false

steps:
  - id: test-history
    name: Find latest test history
    shell: bash
    env:
      GH_TOKEN: ${{ github.token }}
      DEFAULT_BRANCH: ${{ github.event.repository.default_branch }}
    run: |
      run_id="$(gh api \
        "repos/${GITHUB_REPOSITORY}/actions/artifacts?name=mtp-test-history&per_page=100" \
        --jq '[.artifacts[] | select(.expired == false and .workflow_run.head_branch == env.DEFAULT_BRANCH)] | first | .workflow_run.id // empty')"
      echo "run-id=${run_id}" >> "${GITHUB_OUTPUT}"

  - name: Restore test history
    if: steps.test-history.outputs.run-id != ''
    uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
    with:
      name: mtp-test-history
      path: .test-history
      run-id: ${{ steps.test-history.outputs.run-id }}
      github-token: ${{ github.token }}

  - name: Run tests
    run: dotnet test --report-gh --report-gh-history .test-history/history.json

  - name: Publish test history
    if: always() && github.event_name == 'push' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch)
    uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
    with:
      name: mtp-test-history
      path: .test-history/history.json
      if-no-files-found: ignore
      retention-days: 90
```

For .NET SDK 8 or 9, add the `--` separator before the Microsoft.Testing.Platform options.

## Related packages

- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps reporting

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
