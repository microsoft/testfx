# CTRF 0.3 assertion failures

## Problem and contract

The next major version of `Microsoft.Testing.Extensions.CtrfReport` adopts the
CTRF 0.3.0 format by default. This is a user-directed next-major change, not a
format change to the stable 2.x contract described in
[the stable-release decision](ctrf-stable-release.md).

Capture the single comparison available from MTP's `AssertionFailureProperty`
and emit one `assertionFailures` entry on the failed attempt that owns it.
The explicit property is authoritative; only its absence permits the legacy
`Exception.Data["assert.expected"]` / `["assert.actual"]` string fallback.
Unavailable sides are omitted, while empty strings remain present. Do not parse
messages, invent typed values, or attach combined diagnostics to an individual
assertion. Existing result-level `message` and `trace` remain unchanged.

The capture and crash-recovery journal retain bounded comparison text. Normal
reports, recovered reports, in-process retry history, and cross-process retry
merges preserve its association with the correct test and attempt. A later
passing result does not inherit an earlier failure's comparison.

The existing MTP host owns execution, cancellation, disposal, and recovery.
There is no new handshake, CLI switch, registration API, or dependency.
Reporting remains opt-in; enabling it does not change test outcomes or exits.
Multiple-assertion transport is outside this change: MSTest continues to omit
an ambiguous result-level pair when more than one comparison exists.

## Research and experiments

2026-11-10, baseline `32a6ed6e1511ae9b61c9ce307e22ed5594b239cc`:

- The [maintainer's reply][reply] specifies optional, non-empty
  `assertionFailures` collections on final results and earlier attempts, with
  producer-formatted strings and independently optional comparison sides.
- [ctrf-io/ctrf#87][proposal] is open, not a released specification. The reviewed
  immutable proposal snapshot is
  `c109525a7567a629667ebbcdce1c03bdf7c79ab7`; upstream main and its latest tag
  still identify 0.2.0. This implementation targets that proposed 0.3.0 contract.
  Final upstream schema alignment is a release dependency, not claimed complete.
- A recursive schema comparison against v0.2.0
  (`e325eb33c7662a208bfa4d093a41cc55439d6e6a`), excluding descriptive text,
  found only the new optional assertion collections on Test and retry entries.
  Both object schemas reject unknown properties, so adding the field while
  continuing to declare 0.2.0 would be invalid.
- The current CTRF capture DTO and writer discard comparison data. The retry
  merger's field whitelist would also discard the new collection when a final
  result or an existing nested attempt is projected into earlier history.
- MSTest already supplies an unambiguous single comparison through MTP.
  Its multiple-comparison suppression is deliberate and need not change.
- The pinned SDK bootstrap and existing CTRF unit baseline succeeded:
  212 executed and passed on net10.0, with 32 method-level workers and no skips.
- The implementation built for netstandard2.0, net10.0 and net11.0 with no
  warnings or errors. A subsequent all-source build/pack succeeded, including
  package content and metadata validation. The aligned local package versions
  are MTP/CTRF/Retry 3.0.0-dev and MSTest 5.0.0-dev.
- The isolated comparison is outside the repository tree and retains the new
  DTO fields/API entries and 0.3.0 header while using the original capture,
  writer and merger behavior. It is a buildable behavior-removal comparison,
  not a pristine-baseline run. Its first pack failed because the local clone's
  filesystem origin supplied no package project URL. Setting only that
  comparison clone's origin to the real repository fixed package metadata;
  repacking succeeded. That setup failure is not red-phase evidence.

## Alternatives and decision

Keep the current output: valid 0.2.0, but loses structured comparisons.
Use producer-specific `extra`: preserves data without the new schema, but
creates the interoperability fragmentation this upstream change resolves.
Add a format opt-in: unnecessary because the user confirmed that adoption is
for the next major package version. Chosen: native 0.3.0 by default.

Retain only the available single comparison, rather than introducing a new
public collection API or trying to reconstruct the original exception tree.
Leave combined message/trace diagnostics on the enclosing result because they
may describe other failures as well as that comparison.

The merger promotes reviewed 0.0.0/0.0.4/0.1.0/0.2.0 inputs when a 0.3.0 input
is present, independent of input order. Unknown or undeclared versions cannot
be mixed with a reviewed 0.2.0/0.3.0 report. Rejection preserves inputs and an
existing destination. Legacy-only version handling and 0.2.0 report identities
remain unchanged. The new 0.3.0 projection gets its own deterministic report-ID
discriminator so changed retry contents cannot reuse the old merger's identity.

## Validation and demonstration

The tested source is baseline
`32a6ed6e1511ae9b61c9ce307e22ed5594b239cc` plus the production/test patch with
SHA-256 `E1CFF3A03467ADBA174C0B269448E7B177F5F965E221019972ACD5E43FEC73EE`.
The identical retained test patch in both variants has SHA-256
`7103DF337AC49EF696903A3A52FDF92070AA26636AAE1C350A719DE63E55481C`.
The comparison's DTO/header support patch has SHA-256
`27475C6D1448C83BBA407FA2B94A584B5CDD629AEF1A14AB4661B128CC8B39A1`.

The pinned SDK is `12.0.100-alpha.1.26556.115`; builds and packs use Debug.
The outer acceptance hosts target net12.0 through Arcade's `NetCurrent`.
Generated shipping-package consumers build Release and execute net11.0 and
net462 with `FastAcceptanceTest=true`. Source-generation variants also build
successfully; the selected consumer executions use reflection metadata.

| Executed check | Correct implementation | Behavior-removal comparison |
| --- | --- | --- |
| Applicable CTRF units, net10.0 | 281 executed/passed; exit 0 | Same 281 executed: 54 intended failures, 227 passed; exit 2 |
| Applicable CTRF units, net11.0 | 281 executed/passed; exit 0 | Not repeated on this TFM |
| Applicable CTRF units, net462 / net472 | 278 / 278 executed/passed; exits 0 | Not repeated on these TFMs |
| Packed MTP CTRF consumers | 25 executed/passed; exit 0 | Same 25 executed: 12 intended failures, 13 passed; exit 2 |
| Packed MSTest consumers | 3 executed/passed; exit 0 | Same 3 executed: 3 intended failures; exit 2 |
| Immutable schemas with format checks | All 62 retained reports passed their declared 0.1.0, 0.2.0 or reviewed 0.3.0 schema | 54 reports retained; the legacy-first aggregate also demonstrated one schema-invalid output |

Every selected case executed, with no skips. Source/data-row plans and separate
discovery were reconciled with fresh TRX records using the repository execution
verifier. The existing Windows-inapplicable
`MergeToFileAsync_WhenOutputDiffersByCaseOnly_IsAllowedOnCaseSensitiveFilesystem`
was explicitly excluded from the applicable unit selection. This was not a full
repository or cross-platform run.

Both variants used independently packed CTRF binaries with version 3.0.0-dev:
the correct package has SHA-256
`33CBD56B508FAA7FA399D889221AEB1C3557FF9BA06C9615BBB8B1FC3C8BC97D`;
the comparison has
`F7342F5E9E38D96F8043BB6E59E94495C1A220DFFC8820979F8CCDCED0AEA97C`.
Each variant's 42 resolved package references matched the current package
SHA-512 values in `project.assets.json`, and 60 generated-consumer output DLLs
matched the corresponding packed payloads. Effective feeds, marked per-variant
package caches, executable locations, reports, manifests, binlogs and TRX were
retained in session artifacts. The concise results here are the published
evidence; raw local logs are not claimed to be publicly accessible.
The isolated comparison source and outputs are retained locally for replay;
its borrowed-toolchain junction was removed after execution without changing the
original toolchain. All environment overrides were scoped to their commands.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Explicit comparisons, absent/empty sides and correct result identities | `Ctrf_WhenReportCtrfIsSpecified_CtrfReportIsGeneratedInDefaultLocation`, `Ctrf_WhenReportCtrfFilenameIsSpecified_CtrfReportIsGeneratedWithThatName`, and `CtrfReport_WhenTestsArePassingAndFailing_CtrfFileIsGeneratedWithBothOutcomes` passed. Assertions associate data with each named result and outcome, not only text presence. | Nonvisual JSON contract; no hosted dashboard rendering change. |
| Completed comparisons survive a host crash | `Ctrf_WhenTestHostCrashes_RecoversCompletedResultsAndMarksReportIncomplete` passed with the expected failed-child/crash exit and recovered pair. | Same JSON contract. |
| Earlier failures do not leak onto a passing final result | `CtrfReport_WithMSTestRetryAttribute_MarksRetriedTestAsFlaky` and `RetryFailedTests_CtrfReports_UseLogicalRunIdentityAndRecordLineage` passed, inspecting physical attempts, prior history and final results separately. | Same JSON contract. |
| Mixed schema versions and nested retry history remain correct | `Ctrf_MergingLegacyAndCurrentReports_PreservesVersionAndAttemptIdentities` passed all four .1/.2 input-order cases; unit cases below preserve ordered foreign arrays and nested attempts. | All actual green report documents passed the matching immutable schema. |

### Test-value evidence

| Contract / named cases | Without the behavior | Correct implementation |
| --- | --- | --- |
| Explicit/legacy precedence, null/empty semantics: `TestResultCapture_ExplicitComparison_RoundTripsOnlyKnownSides`, `TestResultCapture_ExplicitComparison_TakesPrecedenceOverLegacyData`, `TestResultCapture_LegacyComparison_UsesOnlyStringValues` | Executed: missing comparison assertions detected the original capture/writer behavior. | Same cases passed. |
| Failed-only capture, escaping, bounded strings: `TestResultCapture_NonFailedStates_OmitComparisonMetadata`, `TestResultCapture_ComparisonText_PreservesEscapedMultilineValues`, `TestResultCapture_ComparisonLimits_PreserveBoundaryAndSurrogates` | Executed: missing-data faults detected; intentional omission cases remained green. | Same cases passed, including independently bounded sides and surrogate boundaries. |
| Journal and in-process ownership: `ControllerChild_AssertionComparisonsSurviveJournalRoundTripAsync`, `TestResultCapture_InProcessRetries_KeepComparisonsWithOwningAttempt` | Executed: explicit assertionFailures presence assertions failed after the retained journal/retry setup ran. | Same cases passed through source-generated journal deserialization and ordered retry history. |
| Foreign arrays: `Merge_CollapseRetryAttempts_PreservesOrderedAssertionFailuresAndNestedHistory` | Executed: missing whitelist data detected in both final-pass/final-failure cases. | Both passed with ordered multi-entry/nested arrays and distinct final data. |
| Version/identity/file safety: `Merge_CompatibleSpecVersions_UsesVersion03`, `Merge_Version03WithUnreviewedSpecVersion_Throws`, `Merge_Version03_UsesDistinctDeterministicFormatIdentity`, `MergeToFileAsync_IncompatibleSpecVersions_PreservesInputsAndExistingOutput` | Executed: old negotiation, identity reuse and failure-to-reject were detected. | Same cases passed; existing exact 0.2.0 identity assertions also passed. |
| Real packaged report boundaries: the named E2E cases in the matrix above | Executed: 12 MTP and 3 MSTest outer assertions failed for absent comparisons, lost comparisons in recovery/retries, or incorrect mixed-version behavior. | All 28 outer cases passed. Multiple-comparison suppression remains unchanged and is additionally checked in the real MSTest consumer. |

These are behavior-removal results, not compilation/setup failures. Initial
setup attempts caught a semicolon-list wrapper argument issue, a test-helper
parameter name triggering MSTEST0017, and an incorrect assumption that the outer
acceptance host targeted net11.0. Those were corrected before the successful
runs and are not counted as red proof. No analyzer suppression or production
workaround was introduced.

Reproduction from the repository root, with the repo-local `.dotnet` prepended
to `PATH` and `DOTNET_ROOT`, and an owned `$evidence` directory:

```powershell
.\eng\build.ps1 -restore -build -pack -projects 'src\**\*.csproj' -binaryLog '/bl:{}' /p:EnableCodeCoverage=false /p:FastAcceptanceTest=true

$projects = @(
    'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj',
    'test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj',
    'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
)
foreach ($project in $projects) {
    .\eng\build.ps1 -restore -build -projects $project -verbosity quiet -binaryLog '/bl:{}' /p:EnableCodeCoverage=false /p:FastAcceptanceTest=true
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}

$unitFilter = 'FullyQualifiedName~Ctrf&FullyQualifiedName!~MergeToFileAsync_WhenOutputDiffersByCaseOnly_IsAllowedOnCaseSensitiveFilesystem'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net10.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net11.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net462\Microsoft.Testing.Extensions.UnitTests.exe --filter $unitFilter
.\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net472\Microsoft.Testing.Extensions.UnitTests.exe --filter $unitFilter

$mtpFilter = 'FullyQualifiedName~Microsoft.Testing.Platform.Acceptance.IntegrationTests.CtrfReportTests|FullyQualifiedName~RetryFailedTestsTests.RetryFailedTests_CtrfReports_UseLogicalRunIdentityAndRecordLineage'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Debug\net12.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter $mtpFilter

$mstestFilter = 'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportTests|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportRetryAttributeTests'
.\.dotnet\dotnet.exe exec artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter $mstestFilter
```

All six execution commands passed. Recorded runs also supplied
`--report-trx --report-trx-filename <variant>.trx --results-directory <owned-directory>`;
matching `--list-tests` commands passed before acceptance execution. Build and
pack commands used unique named binlogs. The same unit and acceptance selectors
ran against the separately built/packed comparison. The acceptance helpers
record their child build binlogs and assert expected child exits; a deliberately
failing test host exits 2, retry success and merge commands exit 0, and crash
recovery asserts `TestHostProcessExitedNonGracefully`.

The actual packed MSTest report now associates this entry with `FailingTest`:

```json
{
  "name": "FailingTest",
  "status": "failed",
  "assertionFailures": [
    { "expected": "1", "actual": "2" }
  ]
}
```

The same comparison report lacks `assertionFailures`; both retain the enclosing
failure message and trace. This JSON transcript communicates the changed
nonvisual interchange contract better than a screenshot. No downstream
dashboard rendering or live CI run is claimed.

Unit and acceptance assemblies retain method-level scheduling with CPU-count
workers (32 locally). New unit state is per-test mocks/memory or GUID-owned
files; journal workers are drained before disposal. The existing shared-asset
acceptance exclusions remain, and the MSTest CTRF asset class now declares the
same exclusion. CI selection was traced through the Windows Release
`TestFx.slnx` test step under `SkipTests=false`; no new category or execution
gate is required. CI execution itself remains unproven until the PR checks run.

## Rollout and open questions

Target the next major package only; no servicing backport or 2.x default-output
change is included. The CTRF extension owner remains responsible for final
upstream 0.3.0 alignment before release. Multiple-assertion transport remains a
separate enhancement. This change does not promise downstream dashboard support.

[reply]: https://github.com/ctrf-io/ctrf/issues/67#issuecomment-6402490127
[proposal]: https://github.com/ctrf-io/ctrf/pull/87
