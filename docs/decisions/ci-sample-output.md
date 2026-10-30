# CI sample output: research and decisions

## Problem and contract

The public sample validation step should identify what ran, whether it was a
test execution or a help-only smoke check, its exit code, and where to find its
complete output. Successful samples should not flood the CI log with telemetry
or repeated help text.

The change is limited to the repository's sample-validation script. Sample
selection, executable arguments, package versions, exit-code checks, and the
existing narrowly scoped handshake retry remain unchanged. Local invocations
retain their live output. In Azure Pipelines (`TF_BUILD=true`), each native
invocation writes separate stdout and stderr transcripts below the existing
logs artifact directory. Failed attempts replay their transcripts; successful
attempts print a compact result. Long invocations emit an attributed heartbeat.
Output capture must drain both streams and preserve nonzero exits.

Transcripts are owned by one script invocation and attempt, not shared across
concurrent runs. No environment snapshot or credentials are recorded. The
script does not publish test results or reinterpret sample test counts.

## Research and experiments

- 2026-10-30: public TestFx build
  [1655468](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1655468)
  produced 2,897 sample-step log lines for 28 invocations. The OpenTelemetry
  sample accounted for 827 lines; ten help-only checks accounted for 1,471.
  Their combined output was 79% of the log, despite successful validation.
- `eng/test-samples.ps1` already classifies runnable samples, smoke checks, and
  dependency-only projects. Preserve that inventory and retry policy rather
  than deriving success from text searches or creating another test runner.
- The pipeline already publishes `artifacts/log/<configuration>`. Put sample
  transcripts beneath the supplied `BinaryLogDirectory`, with a repository
  artifact-directory fallback, rather than adding an upload service.
- Built the unchanged `MTPOTel` and `TestingPlatformExplorer` samples with the
  repository-initialized .NET SDK 12.0.100-alpha.1.26509.101. Captured an actual
  successful CI-mode baseline invoking both through `eng/test-samples.ps1`
  at `7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc`. Both invocations exited zero.
- Environment/setup attempts are not regression evidence: `build.cmd` could
  not resolve `powershell` in this execution environment; the explicit
  PowerShell entry point then rejected a `-bl:` switch and a multi-project
  argument. Repository `InitializeDotNetCli` followed by individual builds
  succeeded, with separate binary logs. No product behavior was changed to
  work around these invocation issues.
- Windows PowerShell 5.1 initially returned an empty native exit code after
  `Start-Process` and a timed wait. Retaining the process handle before waiting
  fixes that compatibility case; both supported PowerShell hosts now preserve
  success and failure exits. Initial fixtures under the system temporary
  directory encountered cleanup permissions, so the checks use unique,
  repository-owned `artifacts/tmp` directories instead.

## Alternatives and decision

- **Keep streaming everything:** retains evidence but hides the sample-level
  outcome beneath telemetry/help output.
- **Disable exporters or skip help checks:** reduces noise by removing the
  behavior that the samples are supposed to validate; rejected.
- **Capture only stdout or buffer all output in memory:** risks losing stderr,
  blocking a child process, or losing diagnostics when a run is interrupted.
- **Capture native streams to files:** use PowerShell's existing native-process
  redirection, retain the native exit status, and replay failure output. Keep
  full transcripts for successful demonstrations instead of modifying them.

Use the existing script/helper layout and built-in PowerShell facilities.
Windows PowerShell 5.1 compatibility matters because the current sample CI
task uses that host. Native argument boundaries, including spaces, quotes,
and trailing backslashes, must survive process launch.

## Validation and demonstration

The actual same two-sample invocation fell from **816 console lines before to
5 after**, with exit zero in both states. The after run retained four transcript
files containing 809 lines. The sample executables and their resolved
`Microsoft.Testing.Platform` / `Microsoft.Testing.Extensions.OpenTelemetry`
2.5.2 packages were unchanged. The parent SDK was
12.0.100-alpha.1.26509.101.

The following commands were executed from the repository root. `$evidence`
denotes the task-owned evidence directory; its absolute machine-specific path
is intentionally omitted.

```powershell
. .\eng\common\tools.ps1
$dotnetRoot = InitializeDotNetCli -install:$false
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
& $dotnet build .\samples\public\MTPOTel\MTPOTel.csproj -c Release "-bl:$evidence\sample-otel-baseline-{}.binlog" -v:minimal
& $dotnet build .\samples\public\TestingPlatformExamples\TestingPlatformExplorer\TestingPlatformExplorer.csproj -c Release "-bl:$evidence\sample-help-baseline-{}.binlog" -v:minimal
$env:TF_BUILD = 'True'
$env:DOTNET_ROOT = Join-Path $PWD '.dotnet'
.\eng\test-samples.ps1 -Configuration Release -BinaryLogDirectory "$evidence\sample-before" -Include @('MTPOTel/*','TestingPlatformExamples/TestingPlatformExplorer/*')
.\eng\test-samples.ps1 -Configuration Release -BinaryLogDirectory "$evidence\sample-after-final" -Include @('MTPOTel/*','TestingPlatformExamples/TestingPlatformExplorer/*')
```

Both builds succeeded with zero warnings/errors. The first sample invocation
used the unchanged script at
`7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc`; the second used this change.
Readable transcripts demonstrate the output more clearly than screenshots:

**Before** (excerpts; telemetry and help bodies omitted):

```text
Validating sample: MTPOTel/MTPOTel.csproj (Run)
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
...
SUCCESS: Validated MTPOTel/MTPOTel.csproj

Validating sample: TestingPlatformExamples/TestingPlatformExplorer/TestingPlatformExplorer.csproj (Smoke)
...
SUCCESS: Validated TestingPlatformExamples/TestingPlatformExplorer/TestingPlatformExplorer.csproj

Validated 2 public sample invocation(s).
```

**After** (only transcript-root paths shortened to `<logs>`):

```text
Validating sample: MTPOTel/MTPOTel.csproj (Run)
PASS: MTPOTel/MTPOTel.csproj (Run; attempt 1/1) (4.5s, exit 0). Transcripts: <logs>\MTPOTel.MTPOTel.csproj.Run.attempt1.{stdout,stderr}.log
Validating sample: TestingPlatformExamples/TestingPlatformExplorer/TestingPlatformExplorer.csproj (Smoke; help only)
PASS: TestingPlatformExamples/TestingPlatformExplorer/TestingPlatformExplorer.csproj (Smoke; help only; attempt 1/1) (0.6s, exit 0). Transcripts: <logs>\TestingPlatformExamples.TestingPlatformExplorer.TestingPlatformExplorer.csproj.Smoke.attempt1.{stdout,stderr}.log
Validated 2 public sample invocation(s).
```

Additional executed checks:

| Requirement | Evidence |
| --- | --- |
| Quiet success without lost output or exit codes | `SuccessfulCommand_PrintsOnlyItsSummary`, `SuccessfulCommand_PreservesExitAndBothStreams` |
| Useful failure output and preserved nonzero exit | `FailedCommand_ReplaysBothStreamsAndPreservesExit` |
| Drain both redirected streams before reading them | `LargeConcurrentStreams_AreDrainedBeforeReturning`, exact 2,048-line assertions per stream |
| Preserve native argument boundaries and empty argument lists | `NativeArguments_PreserveEmptyQuotesAndTrailingBackslash`, `EmptyArgumentList_LaunchesTheCommand` |
| Preserve Unicode in both transcripts | `UnicodeOutput_IsPreservedInBothStreams` |
| Windows PowerShell 5.1 compatibility | All seven checks passed, exit zero; actual `MTPOTel` CI-mode invocation also passed, exit zero |
| Keep local live output | `TF_BUILD` absent, `TestingPlatformExplorer` emitted 139 lines including help, one normal `SUCCESS` line, and no capture-summary line; exit zero |
| Bounded progress for a quiet child | An actual child waited on a release file; the parent's `RUNNING` message at 60 seconds released it. The child exited zero and the parent reported `PASS` at 60.3 seconds. |

Additional real-sample validation at implementation commit
`cac21b0a44dbbd190c3a16fc78d955139a0d9588` exercised the VSTest command branch
and an intentional retry demonstration:

```powershell
& $dotnet build .\samples\public\DemoMSTestSdk\ProjectUsingVSTest\ProjectUsingVSTest.csproj -c Release "-bl:$evidence\sample-vstest-{}.binlog" -v:minimal
& $dotnet build .\samples\public\ReliableTestSuite\ReliableTestSuite.csproj -c Release "-bl:$evidence\sample-retry-{}.binlog" -v:minimal
.\eng\test-samples.ps1 -Configuration Release -BinaryLogDirectory "$evidence\sample-additional-after" -Include @('ReliableTestSuite/*','DemoMSTestSdk/ProjectUsingVSTest/*')
```

Both builds and both invocations exited zero. The retained VSTest transcript
contains one passed test. The retry sample retains its intended first-attempt
failure and `failed -> passed (2 attempts)` detail, followed by 10 passed,
one skipped, and zero failed tests. The CI console correctly reports successful
invocations instead of surfacing the intentional intermediate failure as a
pipeline failure.

```powershell
.\eng\test-samples-output.ps1
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -File .\eng\test-samples-output.ps1
```

Both commands executed all seven checks: **7 passed, 0 failed, 0 skipped,
exit 0**. Fixtures and native processes are scoped to each invocation; no
MSTest assembly scheduling or global machine configuration changes.

### Test-value evidence

An isolated helper copy deliberately replayed successful stdout and stderr
before its result line, restoring the unwanted verbose behavior while keeping
the new API and test setup intact. This is a **targeted feature-ablation check**,
not a pristine-baseline test run.

```powershell
.\eng\test-samples-output.ps1 -ToolsPath "$evidence\sample-counterfactual\samples-tools.ps1"
```

The same seven checks executed: **6 passed, 1 failed, 0 skipped, exit 1**.
`SuccessfulCommand_PrintsOnlyItsSummary` failed at the intended assertion:
`message count: expected '1', actual '3'`. The correct helper passes the same
assertions under both PowerShell hosts. SHA-256 identities:

- Correct `eng/samples-tools.ps1`:
  `58FA4CF218BA870A524338ADB7C6FB9D0990094885AD07A69BF13A0B8810FE76`.
- Counterfactual helper:
  `E3789EF184532BA8E534A599B8D5799C0B4573CBB3D77B3C26EDF3748476E833`.

The sample packages themselves are unchanged. This validates the sample
script against real built sample executables; it is not evidence for the
separately owned SDK/MTP reporter changes.

## Remaining validation

The complete 28-invocation Windows sample matrix, including installed UWP/WinUI
applications, was not run locally. CI retains that matrix and now runs the
focused output checks first. The command-selection branches and established
handshake retry remain unchanged in meaning; no broader retry is introduced.

## Review follow-up: host coverage and transcript handling

The three review comments were evaluated against
`e25bf807a363b8dd2dbbf9702d1efc6b3a20cf94` and accepted:

- Explicitly run the same check script in two `PowerShell@2` steps, with
  `pwsh: false` and `pwsh: true`, inside the existing `SkipTests` guard.
- Bound transcript filenames using at most 40 sanitized project-filename
  characters and 16 hexadecimal SHA-256 characters from the full
  separator-normalized relative project path, followed by mode and attempt.
  Hashing before sanitization preserves distinct directory/punctuation
  identities; the console still contains the full project path. Binlog naming
  is unchanged.
- Inspect both stdout and stderr for the existing handshake-failure marker,
  including opted-in local runs. Keep the nonzero-exit gate, single retry,
  five-second delay, and original failure exits. Ordinary stderr is not a
  reason to retry.

The longest transcript path in the current 28-invocation inventory, measured
under the reviewed hosted-agent root, decreases from **218 to 165 characters**.
The original path was below `MAX_PATH`; this is bounded robustness for longer
roots and future sample names, not an observed hosted-agent path failure.
An arbitrarily long caller-supplied log directory can still exceed platform
limits. With the configured modes and attempts, the maximum transcript
filename is 96 characters, including the stream suffix.

### Executed review validation

The parent reviewed both workers' combined changes and reran the complete
focused suite: **32 passed, 0 failed, 0 skipped, exit 0** under each of
PowerShell 7.6.9 and Windows PowerShell 5.1.26100.9711.

```powershell
& (Join-Path $PSHOME 'pwsh.exe') -NoLogo -NoProfile -File .\eng\test-samples-output.ps1
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\eng\test-samples-output.ps1
```

YAML parsing and structural assertions verify both explicit hosts, the unchanged
script and `SkipTests` gate, and no unrelated parsed pipeline changes.

| Requirement | Evidence |
| --- | --- |
| Bound names without losing identity | `TranscriptNames_PreserveDirectoryAndPunctuationIdentity`, `TranscriptNames_NormalizePathSeparators`, `TranscriptNames_DistinguishModesAndAttempts` |
| Boundary and deep-path handling | `TranscriptNames_RespectReadableNameBoundaries` (39/40/41 characters), `TranscriptNames_BoundDeepLongPaths` |
| Retry either stream once | `CapturedStdoutHandshake_RetriesOnce`, `CapturedStderrHandshake_RetriesOnce`, `CapturedBothStreamsHandshake_RetriesOnce`, `LiveStderrHandshake_RetriesOnce` |
| Preserve limits and actual failure exits | `CapturedRepeatedHandshake_StopsAfterSecondAttempt`, `CapturedRetryFailure_PreservesSecondExit`, `LiveCommandLaunchFailure_DoesNotReusePreviousExit` |
| Do not broaden retries | Success-marker, no-opt-in, ordinary-stderr, near-marker and empty-stream cases |

These tests launch real native child processes with invocation-owned state and
transcripts. Only `Start-Sleep` is replaced by a recorder; its single
five-second request and the corresponding warning are asserted. Windows
PowerShell's stderr preference adjustment is scoped and restored, and missing
executables still raise the original launch failure.

The same built `MTPOTel` and help-only `TestingPlatformExplorer` samples were
rerun through the complete script in CI mode: **2/2 invocations passed, exit 0**,
with four transcript files. Windows PowerShell 5.1 also ran the real `MTPOTel`
sample successfully. An actual shortened name from that run is:

```text
MTPOTel.csproj.be1009cff9aa6bed.Run.attempt1.stdout.log
```

### Paired regression evidence

Both counterfactuals retain the same 32 final test cases and helper signatures,
so missing APIs or compilation failures cannot manufacture the red result.
Each variant was executed under both hosts:

| Production behavior | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Correct implementation | 32 | 0 | 0 | 0 |
| Restore former full-path-flattening names | 16 | 16 | 0 | 1 |
| Restore stdout-only handshake recognition | 26 | 6 | 0 | 1 |

The naming comparison fails actual name/length assertions, including expected
`Sample.csproj.66b9b4ff875c2e4e.Run.attempt1` versus
`group.a.Sample.csproj.Run.attempt1`. The stdout-only comparison fails the
intended retry assertions: expected two attempts but observed one, or expected
success/second exit 23 but observed the first exit 17. Launch/setup failures
were excluded from these receipts.

The counterfactual commands use the same test script with `-ToolsPath` selecting
an isolated helper copy; `-OutputDirectory` and `-KeepArtifacts` retain unique
per-run evidence. Source SHA-256 bindings:

- Correct helper:
  `BA0E0AECBD450BE1D7D7FDCE9ED477A23670A56D03585CFA1065598FCE4CD7EA`.
- Identical final test file:
  `A014B3328131B0AD0F2ECCD249FDDB518833287A9230EAFFC406CBE043F2254E`.
- Old-name helper:
  `35418A89D501952A752E76AE34C5BBA67C9E721DB30B53C39168BC6EA6C70ECD`.
- Stdout-only helper:
  `A7B89357A0E387C45734C878A9C1466BEDC1617CD291435C65FB492D52BBF678`.

The earlier complete 28-sample CI run validates the original implementation,
not this review follow-up. Hosted execution of both new check steps and the
full sample matrix remains a subsequent CI check; local focused success is not
claimed as new hosted CI or release-branch validation.
