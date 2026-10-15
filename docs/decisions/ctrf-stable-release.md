# CTRF stable release

## Problem and contract

Graduate `Microsoft.Testing.Extensions.CtrfReport` from its forced alpha package
line and experimental registration API without changing report generation,
merging, retry identity, CLI options, or SDK opt-in defaults. This follows the
[CTRF 0.2 identity decision](ctrf-02-attempt-identity.md), not a new identity model.

Stable 1.x commits to compatible public registration APIs, existing CLI options,
and the documented CTRF 0.2.0 output semantics. The producer stays pinned to the
immutable [v0.2.0 schema][schema]. An incompatible schema or identity change
requires a package major version or a separately explicit opt-in; a new upstream
pre-1.0 specification is not adopted automatically. Additive fields must remain
valid for the declared schema. This does not make the upstream draft immutable.

Framework UIDs retain their existing stability scope. In particular, reordering
MSTest data rows can reuse an ID for a different case, and arbitrary frameworks
may supply run-local UIDs. Graduation does not establish cross-run case identity,
infer keys from parameter values, or resolve the upstream omission policy.
Producer-specific `extra` metadata is diagnostic extension data, not a stable
cross-producer protocol or proof that all expected inputs were present.

The shipping boundary is CTRF 1.0.0 with the aligned MTP 2.6.0 release family,
and MSTest.Sdk 4.6.0 when opted in. Normal developer, CI, and official preview
builds must retain their normal suffixes. Only the normal final release build
may produce CTRF 1.0.0, with stable MTP dependencies; this change publishes no
package. JUnit and VideoRecorder remain alpha.

The existing host owns registration, execution, cancellation, and cleanup.
There is no new handshake, launcher, fallback, or orchestration protocol.
Disabled reporting remains disabled; failing tests still fail and retry/merge
behavior is unchanged.

## Research and experiments

2026-10-15, baseline `4bb976b571a50c0811671a87044435d6e9198ed7`:

- [#11953][alignment] is merged. Its previous unit, packed-consumer, schema, and
  CI evidence supports the readiness assessment but is not validation of this
  follow-up revision.
- The tagged CTRF 0.2.0 specification is a working draft. Pinning a producer to
  a reviewed immutable schema is a narrower, supportable promise than following
  every future pre-1.0 schema.
- The [upstream identity discussion][identity] leaves arbitrary framework UID
  stability and omission unresolved. Existing package guidance describes those
  limitations; no stronger identity promise is needed for this graduation.
- Arcade's pinned `Version.BeforeCommonTargets.targets` computes `VersionSuffix`
  for developer, CI, preview, prerelease-final, and final-release builds.
  At baseline, CTRF overrode the label and suppressed final versions.
  MSTest.Sdk independently reconstructs CTRF's alpha suffix, so removing the
  project flags alone would leave SDK consumers requesting the wrong package.
- Baseline build/pack of the CTRF project succeeded for netstandard2.0, net8.0,
  and net9.0, producing `1.0.0-dev`. This is not final-release eligibility proof.
- Baseline final-release evaluation with `OfficialBuild=true`,
  `OfficialBuildId=20261015.1`, and `DotNetFinalVersionKind=release` still produced
  `1.0.0-alpha.26515.1`, confirming the final-version suppression.
- The retained `RegistrationApi_IsNotExperimental` regression executed against
  unchanged baseline production: its reflection assertion detected the
  experimental attribute. All 210 other selected CTRF cases passed; exit 2,
  211 executed, no skips. Harness compilation failures before that executed run
  are not counted as red-phase evidence.

## Alternatives and decision

Keep alpha until upstream 1.0: safe, but unnecessarily ties the producer API's
support status to future upstream specification work.

Remove only `[Experimental]` or only the alpha label: rejected because that
would leave contradictory API, release, SDK, and documentation contracts.

Adopt the MTP version number for CTRF: unneeded version-line change. Retain
1.0.0 and reuse the ordinary Arcade `VersionSuffix` in both the package and SDK.
This preserves the established developer/CI cadence and honors final-release
selection without special stable-version overrides.

Chosen: graduate the reviewed 0.2.0 producer with the bounded compatibility
contract above. Preserve all reporting behavior and opt-in defaults. Revisit
the contract if upstream resolves UID semantics incompatibly; do not silently
reinterpret existing IDs in stable 1.x.

## Validation and demonstration

The tested production/existing-test patch, relative to the baseline above, has
SHA-256 `91A0EA50E9ECD7748125A8A35D1B5B0DA9361FACD2D1295045A9625A843068B9`.
The retained new `CtrfReportExtensionsTests.cs` has SHA-256
`8061361E174EB1949A8858A7F9D8F4D7DC139E953940FD9E942D57F5B82CC775` in
both states. Subsequent edits only finish this decision record. The pinned SDK
is `12.0.100-alpha.1.26509.101`; validation ran on Windows x64.

| Executed check | Result |
| --- | --- |
| Focused shipping-source and affected-test build/pack, Debug | Exit 0; normal packages and package content/metadata validation passed. |
| CTRF unit selection, net8.0 / net9.0 / net462 | 211 / 211 / 208 passed; 630 executions, no failures or skips. |
| Packed MTP acceptance selection, outer net11.0 host | 25 passed, including disabled/default options, mixed-version merges, retries, multi-module exits and crash recovery; exit 0. |
| Packed MSTest acceptance selection, outer net11.0 host | 3 passed, including `[Retry]`; exit 0. |
| C# 12 consumer of the old developer package, no TPEXP suppression | Compiler harness detected the intended `error TPEXP`; build exit 1. This is a diagnostic assertion, not a runtime test pass. |
| C# 12 consumers of the new developer package, net8.0, and stable package, net8.0 / net462 | All built without TPEXP suppression. Each ran enabled/pass, disabled/pass, enabled/fail and enabled/empty cases: child exits 0 / 0 / 2 / 8, test counts 1 / 1 / 1 / 0, report counts 1 / 0 / 1 / 1. |
| Real packed MSTest.Sdk 4.6.0-dev, net10.0 | Enabled and disabled consumers each executed one passing test. Only opt-in resolved CTRF 1.0.0-dev and emitted its report; the disabled consumer rejected `--report-ctrf` with exit 5. |
| Immutable schemas, with format checking | All 73 accepted reports validated: 2 legacy 0.1.0 and 71 native 0.2.0. |

Independent discovery was reconciled with fresh TRX results using the repository's
execution verifier: planned, selected, executed and passed counts matched for
each of the five unit/acceptance runs. The existing Windows-inapplicable
case-sensitive-filesystem case was excluded explicitly, not counted as executed.

### Actual release packages

All three projects were packed in each Release variant below, not merely
evaluated. Every command exited 0. CTRF contains all three target frameworks:
netstandard2.0, net8.0 and net9.0. These are local simulations of the official
release selections, not signed or published release builds.

| Build selection | CTRF | MTP dependency | MSTest.Sdk / seeded CTRF |
| --- | --- | --- | --- |
| Official preview, build ID `20261015.1` | `1.0.0-preview.26515.1` | `[2.6.0-preview.26515.1, 3.0.0)` | `4.6.0-preview.26515.1` / `1.0.0-preview.26515.1` |
| Same build, `DotNetFinalVersionKind=prerelease` | `1.0.0-preview.final` | `[2.6.0-preview.final, 3.0.0)` | `4.6.0-preview.final` / `1.0.0-preview.final` |
| Same build, `DotNetFinalVersionKind=release` | `1.0.0` | `[2.6.0, 3.0.0)` | `4.6.0` / `1.0.0` |

Developer and CI evaluations separately produced `1.0.0-dev` and `1.0.0-ci`.
Final-release evaluations of JUnit and VideoRecorder still produced
`1.0.0-alpha.26515.1`. No stable CTRF dependency was prerelease. The packed
SDK templates exactly matched the corresponding CTRF versions, and retained no
profile that enables CTRF automatically.

The normal CTRF package has SHA-256
`D4824C651C6F50574B8B2FC7DC3BB2506F322B6F16BE449583F84986C71C5354`;
the stable package has
`9228B4D64DFBB27C0A0EF62FE5A9D3C59742E357026638F4328258C2C0813C4D`.
Consumer `project.assets.json` SHA-512 values matched the current nupkgs.
The acceptance capture included 16 resolved asset files and 22 runtime DLLs
matching package payloads. Direct consumers also verified actual loaded MTP and
CTRF assembly paths and bytes. SDK consumers verified the cached SDK template
against the packed template. Visual Studio's unrelated fallback folder was
present, but the effective restore path and verified payloads were task-owned
caches; no shared cache was cleared.

### Test-value evidence

| Contract / case | Without the change | Correct implementation |
| --- | --- | --- |
| `RegistrationApi_IsNotExperimental` | Executed: detected the real baseline attribute; 1 intended assertion failure, 210 existing passes, exit 2. | The same 211 net8.0 cases passed; the API test also passed on net9.0 and net462. |
| Unsuppressed packed registration, C# 12 | Executed: compiler harness asserted the old package's exact TPEXP diagnostic. | New developer and stable consumers compiled; effective `NoWarn` was only `1701;1702`, and actual shipping payloads executed. |
| Final-version eligibility and SDK alignment | Baseline final-version evaluation remained alpha; no baseline final package or SDK-only mutation run is claimed. | Executed: nine real preview/prerelease-final/final packs, dependency checks and matching SDK template checks passed. |
| Reporting behavior | Existing net8.0 CTRF cases passed on the baseline. | Those same cases remained green; packed acceptance cases and stable consumers preserved outputs and exits. |

### Commands and reproduction

All commands run from the worktree root, with its `.dotnet` first on `PATH`.
The focused build used a generated
`artifacts\ctrf-graduation-validation.slnf`: the unique `src\` projects in
`Microsoft.Testing.Platform.slnf` and `MSTest.slnf`, plus
`Microsoft.Testing.Extensions.UnitTests` and the two acceptance projects.
Its solution path was `..\TestFx.slnx`. This excludes unrelated test suites and
samples while packing the feeds the acceptance fixture requires.

```powershell
.\eng\common\build.ps1 -restore -build -pack -projects artifacts\ctrf-graduation-validation.slnf -msbuildEngine vs -nodeReuse:$false -binaryLogName <unique-binlog> /m:1 /p:FastAcceptanceTest=true /p:EnableCodeCoverage=false

$unitFilter = 'FullyQualifiedName~Ctrf&FullyQualifiedName!~MergeToFileAsync_WhenOutputDiffersByCaseOnly_IsAllowedOnCaseSensitiveFilesystem'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net8.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net9.0\Microsoft.Testing.Extensions.UnitTests.dll --filter $unitFilter
.\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net462\Microsoft.Testing.Extensions.UnitTests.exe --filter $unitFilter

$env:DOTNET_ROLL_FORWARD = 'Major'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Debug\net11.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~CtrfReportTests|FullyQualifiedName~RetryFailedTests_Ctrf'
.\.dotnet\dotnet.exe exec artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net11.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportTests|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportRetryAttributeTests'
```

The build and five execution commands above exited 0. Each execution also used
`--report-trx --report-trx-filename head.trx --results-directory <owned-directory>`;
separate `--list-tests` calls used the same filters.

For each release variant, the exact pack command shape was:

```powershell
.\eng\common\dotnet.ps1 pack <project> -c Release /nr:false -bl:<unique-binlog> /p:ArtifactsDir=<variant-directory> /p:OfficialBuild=true /p:OfficialBuildId=20261015.1 /p:EnableCodeCoverage=false /m:1
```

`<project>` was, in order, the MTP, CTRF, then MSTest.Sdk csproj. The first two
also used `/p:BuildProjectReferences=false`; the SDK retained its real dependency
graph. Preview omitted `DotNetFinalVersionKind`; the other variants added the
value shown in the table. All nine packs exited 0.

Stable consumers used `dotnet build Consumer.csproj -c Release
-p:CtrfVersion=<version> -p:ConsumerTargetFramework=<tfm> -bl:<unique-binlog>`
with the pinned dotnet, an isolated cache and variant feed, then the built DLL
via `dotnet exec` or its net462 executable. The earlier baseline/developer net8.0
checks used `-p:TargetFramework=net8.0` with that same default in the project.
The registration call was direct, without pragmas or TPEXP suppression. SDK consumers used
`dotnet build SdkConsumer.csproj -c Release -p:CtrfEnabled=<true-or-false>
-bl:<unique-binlog>`, then the actual net10.0 host.

Validation setup corrections are not product regressions: a combined project
switch needed the generated filter; the common post-solution package verifier
assumes the root artifact path, so isolated variants used the pinned dotnet
wrapper; disabling SDK project references incorrectly requested `GetTargetPath`
from an outer multi-targeting dependency. The corrected SDK graph packed
successfully. The private consumer harness also needed to account for Visual
Studio's fallback folder and use a restore-visible target-framework property.
Only the corrected successful runs are counted above.

The API check only reads immutable reflection metadata and introduces no shared
mutable state. Existing unit/acceptance assemblies use method-level parallelism
with CPU-count workers. The CTRF acceptance fixture retains its existing
class-level exclusion for its generated mutable test asset. No scheduling or
resource declarations change.

Text transcripts and package metadata communicate this nonvisual change better
than screenshots. Binlogs, fresh TRX, source patches, package/consumer manifests
and schema-validated report copies are retained in session artifacts, not
published. This is not a full-repository test run. No fresh Linux/macOS,
NativeAOT, packaged-app activation, servicing-branch or new-head CI execution is
claimed. No new OS-specific runtime or packaged-app behavior is added.

## Rollout and open questions

Target `main` and its next normal release; do not backport graduation to existing
alpha servicing packages or enable it in downstream profiles. The CTRF owner
retains responsibility through normal package release and SDK dependency flow.
There is no new temporary flag or denylist to remove.

The unresolved upstream UID omission policy remains a documented limitation,
not a claim that framework IDs are stable. No release-branch execution or
cross-platform CI is claimed by the local assessment.

[schema]: https://github.com/ctrf-io/ctrf/blob/v0.2.0/schema/ctrf.schema.json
[alignment]: https://github.com/microsoft/testfx/pull/11953
[identity]: https://github.com/ctrf-io/ctrf/issues/66#issuecomment-6065839895
