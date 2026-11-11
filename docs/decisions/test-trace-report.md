# Test trace report: research and decisions

## Problem and contract

Produce a useful first report from the traces already retained by TestFx before
adding native MSTest instrumentation or scheduling a weekly agent. The report
must identify actual fixture and test-occurrence costs, demonstrate whether the
existing detail is useful, and distinguish missing evidence from measured zero.

The producer is the repository-only `testfx-otel-jsonl` schema 1 exporter merged
in #12325. The consumer is an offline report utility and its generated report.
No shipping package, instrumentation, telemetry destination, CI permissions,
retention settings or scheduled workflow changes are in scope.

Input artifacts are untrusted data, never executable code. Bind observations to
their archive, module, framework, environment, CI run, source revision and attempt.
Validate archive completion and span identity before including an execution in
rankings. Controllers, informative/tool operations and nested hosts created by
tests are not independent test-suite executions. Preserve incomplete input as an
explicit coverage limitation rather than silently treating it as a healthy run.

Report elapsed test-occurrence time, shared fixture invocations and reported
TestContainer steps separately. Do not sum overlapping spans into run wall time,
subtract fixture/child durations to invent a native body-only timer, or equate
test-case span duration with CPU or reported per-result duration.

## Research and experiments

### 2026-11-11

- Implementation starts from `7d5d0508fd6984bf37d0eee47ba7dfc2443febbe`, the
  #12325 merge, fast-forwarded into this task's clean, isolated worktree.
- A bounded Azure build query found one completed #12325 PR build: `1673649`,
  source merge revision `3a1134ae9b2d32ee8a65b89075b130e8f7f7c195`, with PR
  head `daa3867dae22571095d307540db3e840c4cdb5d9`. One build cannot establish a
  longitudinal trend or distinguish runner load from a product regression.
- Downloaded its Windows Release, Windows Debug and Linux Release test artifacts
  using the existing bounded downloader. The direct file-container listing
  required sign-in; the public build-artifact ZIP endpoint worked. A `subPath`
  query did not narrow the ZIP. Read only trace members without extracting or
  executing other artifact content.
- The Linux archive has 29 trace files. It contains controller-only files,
  tools-only files, and executing workers; the extensions unit-test worker also
  contains nested hosts and an intentionally emitted synthetic test span.
  Counting all files, all `TestHost` spans or all `test.case.id` attributes would
  overstate suite coverage. Attribute/trace identity alone is insufficient:
  use the parent graph to isolate the outer host and its test frameworks.
- Existing fixture data already identifies individual acceptance fixture
  initialization intervals longer than 30 seconds. This is measured attribution,
  not proof that those fixtures regressed or are avoidably slow.
- Existing Python report/normalizer utilities under `.github/scripts` use the
  standard library and `unittest`. Reuse that execution model for ZIP streaming,
  typed JSON and large input; PowerShell performance/semantic parity for this
  processing has not been established. No new dependency is needed.
- Cross-checking the root CTRF reports found 137,818 published results, versus
  137,832 observed OTel occurrences. Inspection identified fourteen additional
  single-test subprocess observations: trace-bridge and isolated WinUI startup
  tests re-execute themselves. Some inherit the controlled-host protocol, so
  `HostType` alone cannot distinguish suite coverage from auxiliary processes.
  Keep both observations and the independent CTRF counts visible, rather than
  deleting genuine execution data or claiming fourteen additional suite tests.
- Portable and Windows-specific targets can export identical
  `TargetFrameworkAttribute` labels despite different test selections. Keep
  binary MVIDs separate in this snapshot; a stable full target-platform label
  is needed before constructing cross-build cohorts. No exporter change is
  included in this report-first work.

## Alternatives and decision

| Approach | Decision |
| --- | --- |
| Add a weekly agent immediately | Deferred: first demonstrate actual value and input coverage, without giving an agent responsibility for arithmetic or unproven regression claims. |
| Add body timing first | Deferred by the user; native fixture spans and TestContainer steps can already answer useful questions. |
| Compare raw Windows/Linux or Debug/Release durations as trends | Rejected: these are different environments, not repeated comparable observations. |
| Offline deterministic report from existing archives | Chosen: directly inspect measured costs, preserve provenance and uncertainty, and make the result reproducible without a service or scheduled job. |

The initial output is a descriptive snapshot, not an alerting system. Cohorts
remain separate by module, TFM, OS, configuration, architecture, runtime, SDK,
processor count, binary MVID and available image/collection metadata. MVIDs are
snapshot provenance, not stable cross-build cohort keys. Missing load, coverage
and parallelism metadata remains visible; environment labels do not establish
equal machine load. The HTML view puts ranked costs first and keeps the full
worker/environment tables in native expandable sections.

## Validation and demonstration

Executed on Windows with Python `3.12.10`; no packages were installed and no .NET
shipping code changed. The actual producer/consumer check reads the CI-exported
ZIP files, not a mock of the exporter.

| Requirement | Evidence |
| --- | --- |
| Attribute tests to their outer execution and count fixture invocations once | `test_outer_execution_owns_counts_and_overlapping_fixture_intervals` excludes two nested/probe tests, preserves two distinct outcomes, and independently expects 6,000 ms of fixture work but a 5,000 ms interval union. |
| Preserve exact timing units and distinguish duration statistics | `test_exact_timestamp_ticks_and_percentiles_preserve_units` checks 1,234,567 ticks, hand-calculated median/p95 and overlapping intervals. |
| Keep controllers, informative operations and copied archives out of execution totals | `test_zip_controller_informative_and_duplicate_files_are_not_extra_runs` reads a real ZIP with independent CTRF results and checks exact role/count associations. |
| Do not invent extra builds or merge platform variants | `test_configuration_cohorts_are_not_independent_build_samples` and `test_binary_variants_and_standalone_processes_do_not_merge_into_suite_coverage` keep one build distinct from multiple configurations, binary MVIDs and protocols. |
| Missing phases are unknown, not zero | `test_missing_fixture_and_step_timings_are_unknown_not_zero` requires null fixture timing, absent step observations and explicit presentation text. |
| Reject malformed/incomplete/incompatible evidence explicitly | `test_incomplete_incompatible_and_invalid_inputs_remain_explicit_gaps` covers missing completion, unsupported schema, wrong counts, export failure, duplicate/cyclic identity, inconsistent timestamps and invalid timing/identity/count types. |
| Bound inputs including rejected files | `test_input_budget_counts_rejected_archives_and_invalid_ctrf_fails_explicitly` verifies aggregate size enforcement and explicit invalid CTRF errors. |
| Retain result/name associations and escape report content | `test_rendered_tables_keep_identity_and_escape_untrusted_names` checks long and escaped labels, proper Markdown table shape and focusable horizontal scrolling. |
| Deliver the real CLI outputs and preserve partial-input failure status | `test_cli_writes_real_outputs_and_reports_partial_input_with_nonzero_exit` launches the actual script, reads JSON/HTML, and requires exit 2 with the rejected archive retained in coverage. |

Final command: `python .github\scripts\test_report_test_traces.py -v`.
Result: **10 tests passed, zero skipped, exit 0**. The first run exposed invalid
Markdown table spacing, which was fixed rather than weakening the assertion.

Generate the actual report from the retained evidence with:

```powershell
python .github\scripts\report_test_traces.py `
    --input C:\evidence\1673649\TestResults_Linux_Release_Attempt1.zip `
    --input C:\evidence\1673649\TestResults_Windows_Release_Attempt1.zip `
    --input C:\evidence\1673649\TestResults_Windows_Debug_Attempt1.zip `
    --output C:\evidence\report\testfx-trace-value
```

Executed with the same filenames under this session's task-owned `trace-report`
artifact directory: **exit 0**, 271 complete archives, no rejected archives,
114 executing processes and 137,832 attributed occurrences. The 100 root CTRF
reports independently contain 137,818 results: 137,054 passed and 764 skipped.
All worker executed/skipped counts reconcile with their corresponding outer
host. The fourteen additional occurrences were traced to self-executing
trace-bridge and isolated-startup tests, not reported as additional suite tests.
There are 30,780 occurrences with reported steps (22.3%). There is no macOS
artifact in this selected build.

The report already exposes useful investigation targets without new
instrumentation. Windows Release `ClassInitialize` takes 149.70 seconds for
`DynamicExtensionIsolationTests`, 119.52 seconds for
`AssemblyFixtureProviderNativeAotTests`, and 114.70 seconds for `AbortionTests`.
At least one fixture is active during 2,052.42 seconds of the 4,184.99-second
MSTest acceptance worker, and 1,647.95 seconds of the 3,431.89-second MTP
acceptance worker. These overlapping intervals are **not exclusive overhead,
avoidable cost, a critical path or evidence of regression**.

The HTML report was inspected in the browser at 1280x721 and in a real
480x760 iframe viewport. At the constrained width, the first layout wrapped
names excessively; the final layout keeps readable 900-pixel tables inside
focusable horizontal-scroll regions. Document width remains 465 pixels at
the 480-pixel viewport, and all columns remain reachable by scrolling.
Expandable worker/step/cohort sections open and close correctly.
Separate, explicitly synthetic presentation fixtures exercise long labels,
literal `<script>` text, a Unicode label and the empty/partial-input state;
names remain associated with their outcomes, no script element is created,
rows do not overlap, and missing/error evidence stays visible. These fixtures
are presentation checks, not measured CI results.

The focused tests use Python `unittest` with per-test temporary directories and
joined, bounded child processes. They do not mutate environment variables,
install listeners, change .NET test scheduling, or share output paths between
test methods. No MSTest assembly or cross-test resource declaration changes.

### Test-value evidence

Base: `7d5d0508fd6984bf37d0eee47ba7dfc2443febbe`. The final local commit binds
these source hashes; the report JSON SHA-256 is
`0C898A58E38522224C4299FD99C82BBA5C6407911B406908F11589C09380A15E`.

| Source | SHA-256 |
| --- | --- |
| `.github/scripts/report_test_traces.py` | `B3D207CD223175CC2F9CA0AA7C1E7726739E554E4E5BC720DA3ECB88F8FD5B1E` |
| `.github/scripts/test_report_test_traces.py` | `2F8DCD767366B83670F7C50DE0F9117B577313C0B536BE8EB2F66623CCB8032E` |

The same ten tests and assertions were retained in two isolated session-artifact
directories. Only the indicated production expression changed. Both execute
normally and fail at the intended behavioral assertions; neither comparison
relies on a syntax, import, setup or launch failure.

| Contract / counterfactual | Without behavior | Correct implementation |
| --- | --- | --- |
| Remove framework ownership filtering, counting all `test.case.id` spans | 10 executed, five assertion failures, exit 1; expected two occurrences but observed four. | 10 passed, exit 0. |
| Replace the fixture interval union with a sum | 10 executed, one assertion failure, exit 1; expected 5,000 ms but observed 6,000 ms. | 10 passed, exit 0. |

Comparison command for each variant:
`python C:\evidence\counterfactual\<variant>\test_report_test_traces.py -v`.
The copies were run in this session's artifact directory with byte-identical
test files. The unchanged repository implementation was rerun afterward:
ten passed, zero skips, exit 0.

| Variant | Mutated source SHA-256 | One-hunk patch SHA-256 |
| --- | --- | --- |
| `ownership` | `3713368A578A1FADC26DFE5D6A184F5422CB70A75F334F0ADB79BC321AB83001` | `3C1144E42B73202A12E2FDA1545D00646899932EF8C159EF3D2DC85940A30B02` |
| `overlap` | `8D1FF8A9EFB7295DEA75AA41619FFA62C818EA142F06D3D4EFC7F7346D9DF2CB` | `F26AD9BBFFD3B20A2B304ED88854374C94D5A408E3BF63A39043C632D85DDEB0` |

Exact source/artifact identities, downloaded ZIP hashes and sizes are in the
task artifact `trace-report/1673649/input-manifest.json`; test logs and comparison
patches are retained beside the report. Nothing was published to GitHub or to
an external telemetry service.

### Review follow-up: input layouts, partial ZIP data and literal labels

The requested review batch is scoped to three concrete report-consumer defects,
without changing telemetry production or the report's intended UX:

- Root CTRF selection must mean the same thing for wrapped/unwrapped ZIPs and
  their extracted directory equivalents. A one-level child or merged report is
  not an independent root report.
- A CRC failure in one JSONL ZIP member must name that member in rejected
  coverage while preserving healthy observations and the existing partial-report
  exit code.
- Artifact-derived labels and provenance must remain literal in Markdown as
  well as HTML, including introductions and rejection diagnostics. Escaping
  must not change raw JSON identities or the report's intentional formatting.

The pre-fix report from `d004eedc201e17e7a73dc3113a11f1d8534ad0d3` provides
the real-input baseline: 271 complete archives, 114 executing processes,
137,832 attributed occurrences and 137,818 published CTRF results. Its JSON
SHA-256 is recorded above. Two parallel workers own the ingestion and
presentation corrections; the parent reviewed their combined diff and owns
validation and publication.

All three comments were accepted. ZIP and directory processing now share a
single common-wrapper rule and select CTRF reports only directly under that
artifact root. Member-level `BadZipFile` errors use the existing rejected-input
path. Dynamic presentation values are escaped before composition for each
output context, leaving JSON identities and intentional headings/emphasis
unchanged.

| Comment / requirement | Exact regression evidence |
| --- | --- |
| Comment 4536620612: equivalent artifact roots | `test_ctrf_coverage_uses_same_artifact_root_in_zip_and_directory_layouts` expects two root reports with three associated outcomes in four layouts, excluding distinct child and merged reports. The pre-fix unwrapped ZIP inflates the counts, and the wrapped directory misses them. |
| Comment 4536620700: recover healthy members after a CRC error | `test_cli_retains_healthy_members_and_names_crc_damaged_trace` changes a real stored member's trailing JSON whitespace without changing its recorded CRC. The healthy member and all three report files survive; the exact damaged member is rejected and CLI exit remains 2. |
| Comment 4536620757: preserve literal Markdown/HTML labels | `test_rendered_tables_keep_identity_and_escape_untrusted_names`, `test_introduction_escapes_dynamic_labels_before_intentional_formatting`, and `test_rejected_inputs_render_literal_source_and_reason` cover markup delimiters, introduction provenance, literal `**`, diagnostic labels, unchanged JSON and intended report formatting. |

Parent integration corrected the CRC test's Markdown diagnostic expectation to
the newly escaped literal filename, while retaining the exact raw identity in
JSON and HTML. An initial combined run also encountered the existing 15-second
child-process timeout under machine load. The three CLI launches now share a
named 60-second startup/I/O/teardown bound; this is not a performance assertion,
and no result assertion was removed.

Final validation on Python 3.12.10:

```powershell
python -B .github\scripts\test_report_test_traces.py -v
```

Result: **14 executed, 14 passed, zero skipped, exit 0**. The parent reran the
same five regression methods against an isolated copy of the exact
`d004eedc201e17e7a73dc3113a11f1d8534ad0d3` production file, then the corrected
file. Test bytes and assertions were identical:

| State | Executed methods | Result |
| --- | --- | --- |
| Pre-fix source | 5 | Eight intended assertion failures across layout, CRC output and literal-rendering contracts; zero setup errors/skips, exit 1. |
| Corrected source | 5 | Five passed, zero skipped, exit 0. |
| Combined final suite | 14 | Fourteen passed, zero skipped, exit 0. |

The paired selector is:

```powershell
python -B .github\scripts\test_report_test_traces.py `
    ReportTestTracesTests.test_ctrf_coverage_uses_same_artifact_root_in_zip_and_directory_layouts `
    ReportTestTracesTests.test_cli_retains_healthy_members_and_names_crc_damaged_trace `
    ReportTestTracesTests.test_rendered_tables_keep_identity_and_escape_untrusted_names `
    ReportTestTracesTests.test_introduction_escapes_dynamic_labels_before_intentional_formatting `
    ReportTestTracesTests.test_rejected_inputs_render_literal_source_and_reason -v
```

The red command uses the same test file in the task-owned
`trace-report/review-batch/baseline` directory. Relevant source identities:

| File/state | SHA-256 |
| --- | --- |
| Exact pre-fix production snapshot | `4124A9005969CC2AA48E0902A0E7F0B808DC1E2D3DDA8B8A54A03827FF1ACC41` |
| Corrected `report_test_traces.py` | `9BDD1035CEC2833A085EC58DF005170BF7D5ECD66E60D1CB48E2331C1F95064C` |
| Final regression test file, identical in both states | `B765809691840C7F6CBFA437D97B49BC456FEF627D2A663374AE377B3AA109C8` |

The real three-artifact CLI command above was rerun with the combined fixes:
exit 0, no rejected archives, and **byte-equivalent analytical JSON** with the
same SHA-256, counts, identities and largest fixture timings. Corrected HTML
and rendered Markdown retain the measured table values; escaping changes
presentation serialization, not analytical data. Focused literal-label/empty
rendering was inspected locally at 1280/480-pixel widths, including disclosures
and keyboard scrolling. Hosted GitHub page CSS remains an explicit gap.

## Open questions

- No multi-week history, effective server-side retention period, CPU/load
  measurements or native body-only spans have been established.
- This first sample is a PR build, not a representative history of full main
  runs. The report must not extrapolate its coverage to unobserved platforms or
  claim missing modules/tests were skipped by the product.
