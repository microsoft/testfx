# CTRF 0.2 attempt identity

## Problem and contract

The experimental `Microsoft.Testing.Extensions.CtrfReport` producer and merger
must preserve the identity of each physical attempt without confusing it with
the execution lifecycle shared by coordinated retries.

New reports declare `specVersion: "0.2.0"` and put the final attempt's UUID in
`results.tests[].attemptId`, including tests that were never retried. Earlier
attempts retain their distinct `retryAttempts[].attemptId` values. Neither
`testId`, `executionId` nor retry orchestration changes. Schema-promoted merged
documents receive a distinct deterministic `reportId`; other report identities
retain their existing behavior.

The shipping boundary is the CTRF NuGet package consumed by generated test
applications, together with the aligned Microsoft.Testing.Platform and Retry
packages. The existing host owns execution, cancellation and cleanup; this
change introduces no handshake or public API. A failing test application still
fails, a successful retry still succeeds, and disabled reporting stays disabled.

## Research and compatibility decision

On 2026-10-15, the [owner's reply in ctrf-io/ctrf#66][owner] and the
[tagged v0.1.0-to-v0.2.0 comparison][comparison] established that v0.2.0 adds only
the optional final Test `attemptId`. The old schema rejects that property.
The owner accepts `extra.mtpAttemptId` as a temporary bridge and omission of
unknown legacy attempt identities.

Keep native `attemptId` precedence over the old extension key when promoting a
test into retry history. Preserve legacy final rows as supplied, and never
invent an attempt ID from `executionId`.

When any accepted input declares 0.2.0, the merged report also declares 0.2.0,
regardless of input order. Compatible older inputs are the reporter's historical
0.0.0 documents and the identical upstream 0.0.4/0.1.0 schema family. Reject a
mixture of 0.2.0 with other or undeclared versions rather than claiming an
unreviewed schema conversion. Rejection happens before writing an output, so
original artifacts remain available through the existing failure path. Merges
without a 0.2.0 input retain their existing version behavior.

Keeping only the bridge would remain valid but would withhold the standardized
identity from consumers. Taking the first input's version is unsafe for mixed
0.1.0/0.2.0 reports. Choosing an arbitrary highest version would assume
compatibility across unreviewed pre-1.0 releases.

The final identity trace found that the old deterministic merger salt would
reuse a report ID even when schema promotion changed the output document. The
old packaged merger returned `21ab26fc-87e8-35ee-96b9-f1774469577c` for the fixed
legacy-first fixture. The new regression failed against that behavior before
the correction. Schema-promoted outputs now use a separate format
discriminator. Native-first and legacy-only documents retain their old
identities, and repeated merges remain idempotent.

## Validation and demonstration

The baseline source is `3b60b9f7a9ccaf7b23486cbe96c21837a740ddcb`.
The final source/test/shipping-readme patch has SHA-256
`6BBCB2F09B8CAF6B809C0D967AA076D3CC860F3C0808DEB164ABAFAAB7D71676`.
The repository-pinned SDK is `12.0.100-alpha.1.26509.101`; builds and packs used
Debug, and generated package consumers built Release for net10.0 and net462.
The outer acceptance host targets net11.0 and ran with
`DOTNET_ROLL_FORWARD=Major`. MSTest is 4.6.0-dev, MTP/Retry are 2.6.0-dev, and
the CTRF package is 1.0.0-dev.

| Executed check | Result |
| --- | --- |
| Final platform build/pack, including all CTRF target frameworks | Exit 0; no warnings or errors; package content/metadata validation passed. |
| MSTest build/pack | Exit 0; aligned framework, adapter and source-generation packages produced. |
| Applicable CTRF unit suite, net8.0 / net9.0 / net462 | 210 / 210 / 207 passed; no failed or skipped selected cases. |
| Final focused regression selection | 37 discovered, selected, executed and passed; exit 0. |
| Packed MTP consumers | 25 discovered, selected, executed and passed; exit 0. |
| Packed MSTest consumers, including `[Retry]` | 3 discovered, selected, executed and passed; exit 0. |
| Immutable schemas, including format checks | All 41 final-variant reports passed the schema matching their declared 0.1.0 or 0.2.0 version. |

The baseline suite had one Windows-inapplicable, case-sensitive-filesystem test.
That existing case was explicitly excluded from the applicable unit selection;
it is not reported as executed. This was not a full repository test run.

### Test-value evidence

| Contract and named cases | Without the behavior | Correct implementation |
| --- | --- | --- |
| Native output and retry identity: `GenerateReportAsync_WritesValidCtrfJson_WithRequiredTopLevelFields`, `GenerateReportAsync_ExecutionIdentity_UsesOnlyCoordinatedRetryScope`, `GenerateReportAsync_CollapsesExplicitRetryAttemptsAndFlagsFlaky` | Executed against baseline with the same retained assertions: missing native identity / wrong version detected. | Passed in the final focused selection. |
| Version compatibility and input preservation: `Merge_CompatibleSpecVersions_UsesVersion02`, `Merge_Version02WithUnreviewedSpecVersion_Throws`, `MergeToFileAsync_IncompatibleSpecVersions_PreservesInputsAndExistingOutput` | The original 35-case selection executed 24 intended assertion failures and 11 passes; exit 2, no skips. | The same 35 cases passed, also within the final 37-case selection. |
| Promoted document identity: `Merge_PromotedSpecVersion_DoesNotReuseThePreviousReportId` | Executed before the identity correction: expected a different report ID, got `21ab26fc-87e8-35ee-96b9-f1774469577c`; exit 2. | Passed, including repeat-merge idempotency. Native-first identity preservation also passed. |
| Packed mixed-version merge: `Ctrf_MergingLegacyAndCurrentReports_PreservesVersionAndAttemptIdentities` | A separately packed native-writer/old-merger counterfactual executed both cases: legacy-first failed with expected `0.2.0`, actual `0.1.0`; native-first passed; exit 2. | Both cases passed; they were re-executed in the final 25-case package selection. |

The retained baseline unit-test patch has SHA-256
`FA06D89A16D7D0D141E4D5C51B2948CF8655AE4600D9A780223020EDDD26BC91`.
The intermediate state proving the report-ID defect has patch SHA-256
`9ABB2903D07D027589975ACBBCA9CAD665BC665545F39A23A5C45E0685F449AE`.
Red phases were assertion failures, not compilation, restore, launch or
zero-selection failures.

Package provenance was checked against `project.assets.json` package SHA-512
values, effective feeds/cache folders and the runtime DLL payloads in generated
consumer outputs. The final CTRF nupkg has SHA-256
`03DF2024F0DD64EF946A592BD444FA02422357B257108EA01B0AE942176637D9`;
the old-merger counterfactual has
`249D74B94291338F7D8882A7B10E49D2B60F0499B73F2B7F61743778EDEF6001`.
Both use version 1.0.0-dev but have distinct runtime payloads. The counterfactual
produced the expected schema-invalid legacy-first aggregate; its other five
reports were valid. Report copies, TRX, binlogs, patches and the provenance
manifest are retained in session artifacts, not published. The task-owned
comparison checkout and generated consumer fixtures were removed after capture.

The legacy inputs are schema-valid fixtures derived from real raw reports,
not executions of an older serviced producer binary. Source-generation fixture
builds also succeeded, but the selected consumer executions use reflection
metadata; no NativeAOT execution is claimed.

The first comparison build was nested under the main worktree and failed
because both ancestor `.globalconfig` files were imported. Relocating the
task-owned comparison outside the ancestor fixed setup; that build failure was
not counted as regression evidence. A full MSTest pack also encountered the
existing parallel `ReproNugetPackageLayout` copy contention; the serial
`/m:1` retry passed without changing unrelated build targets.

Reproduction commands, from the repository root after setting `DOTNET_ROOT` to
the repo-local `.dotnet`, prepending it to `PATH` and setting
`DOTNET_ROLL_FORWARD=Major`:

```powershell
.\eng\build.ps1 -restore -build -pack -projects Microsoft.Testing.Platform.slnf -msbuildEngine vs -nodeReuse:$false -binaryLog /m:1 /p:FastAcceptanceTest=true /p:EnableCodeCoverage=false
.\eng\build.ps1 -restore -build -pack -projects MSTest.slnf -msbuildEngine vs -nodeReuse:$false -binaryLog /m:1 /p:FastAcceptanceTest=true /p:EnableCodeCoverage=false

$unitFilter = 'FullyQualifiedName~Ctrf&FullyQualifiedName!~MergeToFileAsync_WhenOutputDiffersByCaseOnly_IsAllowedOnCaseSensitiveFilesystem'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net8.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net9.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net462\Microsoft.Testing.Extensions.UnitTests.exe --filter $unitFilter

.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Debug\net11.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~CtrfReportTests|FullyQualifiedName~RetryFailedTests_Ctrf'
.\.dotnet\dotnet.exe exec artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net11.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportTests|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportRetryAttributeTests'
```

All five final execution commands above passed. Recorded runs additionally used
`--report-trx --report-trx-filename <run>.trx --results-directory <owned-directory>`.
Source/data-row plans were reconciled with independent discovery and fresh TRX
results using the repository's acceptance execution verifier. The packed cases
assert child exits as well: ordinary failing hosts exit 2, successful retry and
merge commands exit 0, exhausted retries exit 2, and crash recovery asserts
`TestHostProcessExitedNonGracefully`.

JSON transcripts and schema validation communicate this report-only change
better than screenshots. CI wiring was inspected: the cases remain in the
existing Windows Release solution selection without new categories or gates.
No CI run or servicing branch validation was performed by this task.

## Rollout and open questions

This change targets main and its next experimental CTRF package. The existing
0.1.0 bridge remains valid on servicing branches; no format-changing backport or
downstream rollout is included. The CTRF extension's owner retains delivery
responsibility.

The stability/omission policy for arbitrary framework UIDs remains unresolved
upstream. Documenting row-order limitations does not establish stable case
identity. This change does not hash parameter values, alter MSTest UIDs or
introduce a blanket `testId` omission rule.

[owner]: https://github.com/ctrf-io/ctrf/issues/66#issuecomment-6065839895
[comparison]: https://github.com/ctrf-io/ctrf/compare/v0.1.0...v0.2.0
