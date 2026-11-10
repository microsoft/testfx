# Test report consistency: research and decisions

## Problem and contract

The pipeline saves several representations of the same test execution. Successful
report generation alone does not establish that the reports describe the same
tests, names, outcomes, or timing.

The first implementation compares the raw Microsoft.Testing.Platform TRX, CTRF,
and JUnit reports already produced by repository test jobs. It is an engineering
validation script, not a new shipping API or a general-purpose report converter.
Reports must come from the **same test-host execution**, not separate test runs.
Comparing timings across runs would require a different, statistical contract.

The consumer is `eng/compare-test-reports.ps1`, run after report writers finish.
It reads but never changes its inputs. Success is exit 0; missing, malformed,
incomplete, ambiguous, or contradictory inputs are terminating errors/nonzero
exits. A failed or skipped test is not itself a comparison failure. Consistent
zero-result reports are valid and explicitly report zero tests; agreement must
not be mistaken for proof of execution or turn no-work modules into failures.
Cancellation stops the script; there are no services, network calls, background
workers, handshakes, or fallback publication paths.

Directory mode requires a sibling `<stem>.trx`, `<stem>.ctrf.json`, and
`<stem>.xml` for every report set in the specified directory. It does not recurse:
merged reports, retry directories, and reports from generated acceptance assets
must not be counted again alongside their inputs. Explicit-file mode supports
arbitrary filenames for one execution. The script cannot prove that a whole
module, absent from all three formats, executed; the test runner remains
responsible for selection and execution completeness.

CI checks whether any raw report exists before invoking the comparator, not just
whether the directory exists. A successful affected-test no-op can leave only a
binlog. Such a step skips comparison explicitly; a lone report of **any** of the
three formats triggers comparison and fails if its partners are missing.

Shared fields are checked using the producers' documented representations:

- Test identities and multiplicity, display names, classes, methods, outcomes,
  per-test durations, and report/suite counters.
- CTRF/JUnit UIDs correlate data rows independently of order or display names.
  GUID UIDs also correlate directly with TRX test IDs. For non-GUID UIDs, TRX
  stores a hash, so use an unambiguous class/display-name match rather than
  duplicating the producer's hash implementation. Ambiguity is an error.
- JUnit uses method names, with `original-name` and attempt suffixes for duplicate
  method labels; its `testpath` retains display names. CTRF/TRX display names
  must not be compared directly to JUnit method labels.
- Durations use milliseconds internally. TRX preserves ticks, CTRF truncates to
  whole milliseconds, and JUnit rounds seconds to three decimals. The default
  absolute tolerance is 1 ms, not a percentage of a test's runtime. Available
  per-test start/stop timestamps are checked separately; missing optional timing
  is not invented.
- CTRF/JUnit share diagnostic capture and trait semantics. Their messages,
  failure bodies, standard streams, and traits can be compared after accounting
  for XML escaping and CTRF's line-array representation.

Run wall-clock duration is deliberately not equated with the sum of parallel
test durations. TRX-specific exception/stream properties, attachment copies,
random report/execution IDs, machine metadata, and format-only hierarchy,
diagnostics, or retry history are not interchangeable. HTML and hosted
Azure DevOps/GitHub summaries are outside this first gate because these jobs do
not save equivalent raw HTML or hosted result payloads. Supporting them later
requires explicit adapters and consumer evidence, not scraping presentation.

Scope assumptions are reversible: only raw, unmerged terminal results are
accepted. Unsupported identities or report shapes fail explicitly rather than
being silently dropped. This repository-local CI gate targets `main`; it changes
no package protocol and requires no downstream rollout or servicing backport.

## Research and experiments

### 2026-11-10: inspect the existing producers and consumers

Source state: `471a807cf8182e0bcd0d9967c8dbf002c04945d2`.

- `azure-pipelines.yml` and `test/Directory.Build.targets` already enable TRX,
  CTRF, and JUnit. The direct Debug `--test-modules` command supplies its own
  aligned filenames. MSBuild-driven runs currently omit architecture from CTRF
  filenames, unlike TRX/JUnit; align the test-only naming before adding the gate.
- `TrxReportEngine.Results.cs` associates results with definitions by GUID,
  emits display names, and maps errors/timeouts/cancellation to failed results.
  `TrxReportEngine.Utilities.cs` hashes non-GUID UIDs with XxHash128.
- `CtrfReportEngine.JsonTestWriter.cs` retains the framework UID and method,
  writes display names and whole-millisecond timing, and represents streams as
  arrays of lines. `JUnitSuiteBuilder.cs` and `JUnitXmlWriter.cs` preserve UIDs
  and display-name paths while writing method labels and rounded timing.
- `TrxCompareTool.cs` compares two TRX files by name/outcome. Reusing its public
  tool would neither compare formats nor check timing or counters, and changing
  that shipping tool is unnecessary for this engineering-only requirement.
- Existing engineering validators use dependency-free PowerShell and
  self-contained fixture tests. New tests will use unique owned temporary
  directories, run sequentially in their own process, restore any changed
  culture, and leave test assembly scheduling and shared caches unchanged.

### 2026-11-10: validate real products and measure the gate

- Packed the platform, TRX abstractions, TRX, CTRF, and JUnit packages at
  `3.0.0-dev` using their unmodified shipping TFMs. An isolated `net10.0`
  consumer resolved these packages from the worktree's Release shipping feed.
  Producer, cache, and executed DLL SHA-256 values matched for all five.
  Its six distinct rows exercised passed, failed, skipped, errored, and timed-out
  states, duplicate method labels, GUID/non-GUID UIDs, escaped display names,
  nested exceptions, streams, and traits. The intentional producer exit was 2;
  the comparison exited 0 with all six results agreeing. Changing only a JUnit
  class in an owned report copy made the command exit 1 for that exact field.
- Native SDK discovery selected 1,237 tests across the two existing
  `AssertTests` containers. All 1,237 executed and passed, with no failures or
  skips. Their exact name multiplicities matched independent discovery, and the
  CI-shaped triplet agreed. This also exercised non-GUID correlation at scale
  and a project whose assembly name differs from its project name.
- The first comparison of those 1,237 results took 26.934 seconds in-process.
  Profiling measured TRX/CTRF/JUnit parsing at 1,594/4,773/1,337 ms, 1,000 XML-text
  normalizations at 826 ms, and 1,000 diagnostic renderings at 50 ms. The repeated
  PowerShell-to-regex callback construction was material overhead. Added a
  valid-text fast path before considering a compiled tool. Two interleaved
  samples per variant on identical input measured 24.578-34.986 seconds for the
  preserved baseline and 5.783-7.603 seconds with the fast path; every comparison
  agreed on all 1,237 tests. This is a small, single-machine observation, not a
  cross-platform benchmark or a CI duration guarantee. A compiled alternative
  has not been implemented or measured.
- Inspection of `eng/pipelines/steps/test-affected-tests.yml` separated execution
  validation from report consistency: the runner owns failure/zero-test exits,
  and successful suppressed/no-work invocations must not become false failures.
  The gate therefore accepts internally consistent zero-result triplets and
  skips CI comparison when the folder contains only diagnostics/binlogs. It
  still rejects a directory with no report sets when invoked explicitly and
  rejects any partial triplet. Executable probe tests cover both pipeline
  templates and all three orphan-report formats.
- The packaged producer also emitted a complete zero-result triplet (producer
  exit 8) and a six-result all-skipped triplet (producer exit 0). Both comparisons
  succeeded with their actual counts, without changing either producer exit.
- A final stream-association check found that joining CTRF lines as plain text
  equated `[]` with `[""]`, accepting no output as one blank line. Retaining the
  new `BlankOutputLinesArePreserved` assertions against that implementation
  produced 72 passes and one intended behavioral failure (73 executed).
  Canonical stream values now retain line counts as well as normalized text;
  bare carriage returns and the intentionally unrepresented final newline still
  follow the producers' formatting rules.
- The initial focused build attempts exposed environment/command issues, not
  report disagreements: semicolon project-list quoting, a 21,233-character PATH
  that `cmd.exe` could not inherit, and global TFM overrides that produced
  NU1510/NETSDK1005. A task-local solution, bounded process-only PATH, normal
  multi-TFM restore/pack, then `--no-restore -f net10.0` for the unit module
  resolved them without changing repository build logic or user-global settings. SDK 12 discovery
  also required the explicit `--list-tests json` format.

## Alternatives and decision

No change leaves inconsistent report mappings undetected. A byte/XML/JSON diff
rejects valid ordering, identity, precision, and format-specific differences.
A new .NET tool could reuse more producer code, but adds a build/package
dependency to a validation step that only needs structured file parsing.

Use PowerShell 7 with its built-in JSON, XML, and invariant numeric parsing.
Keep producer normalization local to this bounded comparison, reject ambiguity,
and test both disagreements and legitimate representation differences.
Reconsider a compiled implementation if measured CI cost or additional adapters
make the script unsuitable; no new dependencies are needed initially.

## Usage

PowerShell 7.2 or newer is required; no module installation is needed.

```powershell
pwsh -NoProfile -File .\eng\compare-test-reports.ps1 `
    -ResultsDirectory .\artifacts\TestResults\Release

pwsh -NoProfile -File .\eng\compare-test-reports.ps1 `
    -TrxPath .\results\tests.trx `
    -CtrfPath .\results\tests.json `
    -JUnitPath .\results\junit.xml
```

Use `-ToleranceMilliseconds 0` to require exact timing equality, or another
nonnegative absolute tolerance explicitly. The default 1 ms accounts for these
reporters' precision; it is not intended for comparisons across executions.
Omitted optional timestamps are counted in the output.

Keep the test runner's exit code independently. A comparison can correctly
succeed for matching failed, skipped, or zero-result reports:

```text
PASS 'real': 6 tests agree; 0 without optional timestamps.
Compared 1 report set(s), 6 tests: TRX, CTRF, and JUnit agree.
```

Directory mode is deliberately nonrecursive. Point it at one execution's raw
report directory, not an artifact tree combining raw and merged runs. Different
custom filenames require explicit-file mode.

## Validation and demonstration

| Requirement / risk | Executed automated evidence | Rendered check / gap |
| --- | --- | --- |
| Identity, ordering, names, and row association | `EquivalentRepresentationsAreAccepted`, `SwappedNamesAreRejected`, `DuplicateDisplayNamesRemainDistinct`, `NonGuidMissingTrxCorrelationIsRejected`; real six-row packaged run and 1,237 independently discovered internal tests. | Built command inspected in an actual 80 x 24 terminal; quotes, markup, and paths remained readable. |
| Durations, counters, diagnostics, and metadata | `OneMillisecondBoundaryIsAccepted`, `DurationOutsideToleranceIsRejected` (1 ms + one tick), `TimestampDriftIsRejected`, `JUnitAggregateTimingIsValidated`, `WallClockTimeIsNotSummedTestTime`, `WrongDiagnosticMessageIsRejected`, `LostTraceIsRejected`, `WrongStandardOutputIsRejected`, `BlankOutputLinesArePreserved`, `BareCarriageReturnOutputIsNormalized`, `LostTraitIsRejected`; real failure/error/timeout reports. | Actionable class mismatch was inspected in the terminal; long diagnostic bounds are asserted by `LongNamesProduceBoundedDiagnostics`. |
| Missing, malformed, and ambiguous input | `MissingTrxPathIsRejected`, `OrphanReportIsRejected`, `RepeatedCtrfUidIsRejected`, `AmbiguousNonGuidNamesAreRejected`, `MalformedJsonIsRejected`, `MalformedXmlIsRejected`, `ExternalEntitiesAreRejected`, `CommandExitsReflectAgreement`. | Real corrupted-report command exited 1 and identified the UID, expected/actual class, and files. |
| Zero results and intentional suppression | `ConsistentZeroResultReportsAreAccepted`, `ZeroAndNonzeroModulesRemainIndependent`, `NoWorkWithOnlyBinlogIsNotCompared`; real packaged zero-result and all-skipped runs. | Zero execution remains explicit, not relabeled as passed tests. |
| CI scope and exact filenames | `PipelineWiringPreservesScope`, `PipelineProbeRecognizesEveryReportFormat`; both extracted pipeline probes executed; MSBuild evaluation and the native test command produced one correctly aligned triplet. Existing rollout validator: 19 + 39 + 13 passed. | Live Azure Pipelines and Linux/macOS execution were not run locally; no hosted report UI is changed. |
| Regression sensitivity | Same 73 tests executed against four isolated faulty variants, the actual pre-blank-line-fix implementation, and the final correct comparator; results below. | Faults caused behavioral assertions, not compilation, launch, or zero-selection failures. |

### Source and evidence identity

The implementation is a local, uncommitted change based on
`471a807cf8182e0bcd0d9967c8dbf002c04945d2`. The six behavior-affecting files
(both scripts, root pipeline, both pipeline templates, and test targets) are
bound by the retained `report-comparison-source-manifest.json`, SHA-256
`C59EBE68B0478D8527CEDF631E882AE8003B0D672770E685A21D1AE6BC39BE9B`.
This decision record is excluded from that manifest to avoid a self-referential
hash. Final script identities:

| File | SHA-256 |
| --- | --- |
| `eng/compare-test-reports.ps1` | `75D3BF63F722A9C2A4D7D442CE8BF11B5799F22E072C59C45952D6693A19BA1C` |
| `eng/test-report-comparison.ps1` | `6CF9A2DE87219B67AA9259A4BECA89FF71C6EDB27C2FE323F695322C4B0C4539` |

Validation used Windows x64, PowerShell 7.6.9, the pinned
`12.0.100-alpha.1.26556.115` SDK, .NET 10.0.14, Release, and aligned MTP/report
packages `3.0.0-dev`. Binlogs, independent discovery, expected names, execution
logs, package hashes, the generated consumer, and counterfactual copies are
retained in session-owned artifact storage, not committed or published. The
commands and decisive excerpts below preserve the result without requiring
access to those local files.

### Commands and observed results

`$evidence` below denotes the session artifact directory; `$consumer` is its
`report-consumer` child. `$results` is the worktree's
`artifacts\TestResults\ReportComparison` directory. The five-project
`report-producers.slnx` selects the platform and the four report packages listed
above. Repository-local build scripts initialized the pinned toolchain first.

| Command | Observed result |
| --- | --- |
| `pwsh -NoProfile -File .\eng\test-report-comparison.ps1` | Exit 0: **73 passed, 0 failed, 73 executed**, no skips; each case uses an owned fixture directory. |
| `.\eng\validate-affected-tests.ps1 -SelfTest` | Exit 0: 19 collection, 39 non-Windows, and 13 CTS checks passed. |
| `.\eng\build.ps1 -restore -build -pack -c Release -msbuildEngine dotnet -projects "$evidence\report-producers.slnx" -bl` | Exit 0: all five packages packed across their shipping TFMs; package metadata/content validation passed; 0 warnings/errors. A bounded process-only PATH was used for the bootstrap commands. |
| `.\eng\build.ps1 -restore -c Release -msbuildEngine dotnet -projects test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -bl -p:EnableCodeCoverage=false` | Exit 0: normal multi-TFM restore. |
| `.\.dotnet\dotnet.exe build test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -c Release -f net10.0 --no-restore -p:EnableCodeCoverage=false "-bl:$evidence\internal-runner-build.binlog" --nologo -v minimal` | Exit 0: 0 warnings/errors. |
| `.\.dotnet\dotnet.exe test --project test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -c Release -f net10.0 --no-build -p:EnableCodeCoverage=false -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= --list-tests json --treenode-filter "/*/*/AssertTests/*" --no-ansi "-bl:$evidence\internal-discovery.binlog"` | Exit 0: 1,237 selected identities in the two existing `AssertTests` containers. |
| `.\.dotnet\dotnet.exe test --project test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -c Release -f net10.0 --no-build -p:EnableCodeCoverage=false -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" --treenode-filter "/*/*/AssertTests/*" --minimum-expected-tests 1237 --no-ansi "-bl:$evidence\internal-execution.binlog"` | Exit 0: **1,237 executed/passed, 0 failed, 0 skipped**; exact reported-name multiplicities reconciled with discovery. |
| `pwsh -NoProfile -File .\eng\compare-test-reports.ps1 -ResultsDirectory $results` | Exit 0: all 1,237 results agreed in `TestFramework.UnitTests_net10.0_x64.{trx,ctrf.json,xml}`. |
| `.\.dotnet\dotnet.exe build "$consumer\ReportConsumer.csproj" -c Release "-bl:$consumer\build.binlog" --nologo -v minimal` | Exit 0: restored from explicit shipping/public feeds into the consumer's isolated cache; producer/cache/output DLL hashes matched. |

The generated consumer registered the real TRX, CTRF, and JUnit providers with a
small test framework. Its distinguishable rows were `Row "one" <&> / alpha`,
`Row two`, `Skipped row`, `Failure <&> "details"`, `Error row`, and `Timeout row`.
It used both GUID and textual UIDs; the first two rows shared method `Data`.
The normal command was:

```powershell
.\.dotnet\dotnet.exe "$consumer\bin\Release\net10.0\ReportConsumer.dll" `
    --report-trx --report-trx-filename real.trx `
    --report-ctrf --report-ctrf-filename real.ctrf.json `
    --report-junit --report-junit-filename real.xml `
    --results-directory "$consumer\results" --no-ansi --no-progress
```

Observed producer exit **2** with two passed, three failed (including error and
timeout), and one skipped result; comparison exited **0** with six agreeing
results. The deprecated `--no-progress` emitted a warning; subsequent executions
used `--progress off`.

The same built shipping layout with fixture-only `REPORT_CASE_MODE=empty` and
`REPORT_CASE_MODE=skipped` emitted separate `empty` and `skipped` filename stems
under their own results directories. The former exited **8** with zero results;
the latter exited **0** with six skipped results. Both comparison commands
exited **0** with the correct counts. Environment overrides were restored in
`finally`. Changing only one copied JUnit `classname` to `Other.Tests` produced
exit **1**:

```text
JUnit class: expected "Example.Tests"; got "Other.Tests".
```

### Test-value evidence

For each comparator variant, the exact command was
`pwsh -NoProfile -File .\eng\test-report-comparison.ps1 -ComparatorScript <variant.ps1>`.
For the CI probe variant, the same test script, comparator, two templates, and
test targets were copied into an isolated session-owned tree; only the indicated
template assignment changed, and that copy's test script ran with `pwsh -NoProfile -File`.
No assertions or case selection changed between variants and the final green run.

| Contract / named case | Targeted counterfactual | Without behavior | Final correct implementation |
| --- | --- | --- | --- |
| Timing / `DurationOutsideToleranceIsRejected`, `TimestampDriftIsRejected` | Replace `Assert-ReportTiming`'s comparison condition with `$false`. | Executed: detected; exit 1, **65 passed / 8 failed / 73 executed**. Wrong durations/timestamps were accepted and the unchanged assertions failed. | Exit 0, **73 passed / 0 failed / 73 executed**. |
| Fields and completeness / `SwappedNamesAreRejected`, `WrongOutcomeIsRejected`, `MissingResultIsRejected`, counter/diagnostic cases | Replace `Assert-ReportEqual`'s comparison condition with `$false`. | Executed: detected; exit 1, **58 passed / 15 failed / 73 executed**. | Same final green run. |
| Zero-result compatibility / `ConsistentZeroResultReportsAreAccepted`, `ZeroAndNonzeroModulesRemainIndependent` | Reintroduce rejection of `ctrf.Count -eq 0` after reading all three reports. | Executed: detected; exit 1, **71 passed / 2 failed / 73 executed**. | Same final green run. |
| Blank-line regression / `BlankOutputLinesArePreserved` | Actual pre-correction comparator: joined streams lose the distinction between no lines and one blank line. | Executed: detected; exit 1, **72 passed / 1 failed / 73 executed**. The unchanged assertion observed an inconsistent report being accepted. | Same final green run; bare-CR normalization also remains accepted. |
| CI no-work behavior / `NoWorkWithOnlyBinlogIsNotCompared` | Initialize the copied Windows probe's `$hasTestReports` to `'true'`. | Executed: detected; exit 1, **72 passed / 1 failed / 73 executed**; the actual extracted probe incorrectly opted into comparison. | Same final green run; both actual probes preserve the no-work skip. |

Counterfactual SHA-256 identities, in table order:

```text
87B66CB1E32D33E3E39BAC97BC4576478F3A714D24A9DA0D7D955463082CC5B6
F1E8B2EDDA40AA44A82501DCDD85FB0C0C030FB8C9C2BAAB9924C74D473E227E
A79FFBFF196D853880485C8F590D988119812703BA7BF4595BC33D748C9636FC
91D3DCE45803E491F223E92975BDDCD2A940EE460F96262DA934E6CC3CAE5B52
2C85F3E50E5EC29D7E53CC5D0321D92E3BC756BDBE6A61B661543A669D06F05B
```

The timing fast path also preserved agreement on all 1,237 real results before
and after the optimization. These are bounded fault-sensitivity experiments,
not an exhaustive mutation score or evidence of live CI execution.

### Cleanup and remaining gaps

Inputs remained unchanged. Later fixture directories under `artifacts/tmp`
were removed in `finally`; normal build outputs and session-owned evidence were
retained intentionally. The known initial OS-temp fixture root and SDK installer
archive were removed by exact path. Initial Windows-temp cleanup also reported
access denied for another fixture run whose path was not returned; the parent
directory cannot be enumerated, so cleanup of that earlier root is unverified.
No global settings were changed, no shared cache was cleared, and no unrelated
process was stopped.

No Azure pipeline was triggered and Linux/macOS were not executed locally.
There is no report-layout change or hosted-UI claim. The actual plain-text
success/mismatch surface was inspected at 80 x 24; another viewport size was not
available through the terminal canvas's actions. No screenshots, logs, or GitHub
metadata were published.

## Open questions

Supporting independently merged/retried reports, foreign reporter dialects,
HTML, and hosted CI payloads is intentionally not claimed. Neither report
agreement nor a matching total proves that all expected modules ran.
The initial index requires unique per-file UIDs/IDs; repeated identities are
rejected explicitly rather than guessed into matching rows or retry histories.
