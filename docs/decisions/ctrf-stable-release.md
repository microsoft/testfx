# CTRF stable release

## Problem and contract

Graduate `Microsoft.Testing.Extensions.CtrfReport` from its forced alpha package
line and experimental registration API without changing report generation,
merging, retry identity, CLI options, or SDK opt-in defaults. This follows the
[CTRF 0.2 identity decision](ctrf-02-attempt-identity.md), not a new identity model.

Stable 2.x commits to compatible public registration APIs, existing CLI options,
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

The shipping boundary is CTRF and MTP with the same 2.6.0 version, and
MSTest.Sdk 4.6.0 when opted in. CTRF inherits the platform version instead of
maintaining a separate prefix or SDK template calculation. Normal developer,
CI, and official preview builds retain their normal suffixes. Only the normal
final release build produces stable packages; this change publishes no package.
JUnit and VideoRecorder remain alpha.

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

The initial implementation retained 1.x and shared Arcade's `VersionSuffix`.
Its local package and consumer results are preserved in the
[initial implementation record][initial-record]. That approach worked but kept
an unnecessary independent version line.

On 2026-10-15 the user directed: "No let's keep versions numbers aligned, it's
easier for maintenance". Align CTRF with MTP, not with MSTest's distinct version
family. The existing `src/Platform/Directory.Build.props` already supplies the
platform prefix, and stable SDK extensions default their version to
`MicrosoftTestingExtensionsCommonVersion`, itself defaulted to the platform
version. Reuse those mechanisms instead of calculating the same number twice.

Chosen: graduate the reviewed 0.2.0 producer with the bounded compatibility
contract above and the normal platform version. Preserve reporting behavior,
SDK version overrides and opt-in defaults. Revisit the contract if upstream
resolves UID semantics incompatibly; do not silently reinterpret existing IDs
within the stable package major.

## Validation and demonstration

Alignment was validated against parent
`a5d567ee14422f86882e81f3e0d4708dbd81fafc`, not inferred from the superseded 1.x
package results. The final production/test patch has SHA-256
`A38C72F5ACF72800039A939422B1398DB352B2C7559D4958B50DED3D31B4A699`.
The retained `SdkTests.cs` has SHA-256
`E8D34D2255D2FB7AF6CA26569C87B5122F59C64FF79D31AD6E0287FAFDF8B886`
in both comparison states. Subsequent changes only finish this decision record.

### Test-value evidence

| Contract / case | Independently versioned parent packages | Aligned implementation |
| --- | --- | --- |
| `CtrfReport_UsesPlatformVersion_WhenOptedIn`, with/without central package management | Both cases built and executed their version assertion: expected `2.6.0-dev`, actual `1.0.0-dev`; 2 intended failures, exit 2, no skips. | Same assertions passed; both SDK-generated applications executed one passing test and reported `generatedBy: Microsoft.Testing.Extensions.CtrfReport@2.6.0-dev`. |
| Existing MSTest mixed outcomes and `[Retry]` reporting | Previous-revision evidence is linked above. | 3 packed cases passed on net10.0/net462; no skips. |
| `Ctrf_MergingLegacyAndCurrentReports_PreservesVersionAndAttemptIdentities` | Previous-revision evidence is linked above. | Both input orders passed using the aligned package; no skips. |

All seven selected acceptance cases passed, exit 0. Independent discovery and
fresh TRX were reconciled with the repository execution verifier. The regression
uses the actual packed SDK, resolved package version and real report, not an
in-repository reference or a reflection-only proxy. Repacking and fresh
per-assembly caches prevented the unchanged SDK package ID/version from reusing
the earlier template.

The Debug pack/build succeeded for MTP, CTRF and MSTest.Sdk, and the affected
acceptance runner built successfully. Consumer asset SHA-512 values matched the
new CTRF package; eight resolved asset files and twelve runtime DLLs matched
the current payload. The developer CTRF package has SHA-256
`2793167BDBC66B145776421427D8BD5A99E876FEA7D06CB77B2C29D4B179A680`.

### Actual release packages and SDK defaults

Nine actual Release packs succeeded, not only property evaluations. Each SDK
was restored into its own cache and its effective properties evaluated from the
packed SDK. The common extension version and CTRF default both equaled MTP:

| Selection | CTRF / MTP | MSTest.Sdk | Effective SDK CTRF version |
| --- | --- | --- | --- |
| Official preview flags, build ID `20261015.1` | `2.6.0-preview.26515.1` | `4.6.0-preview.26515.1` | `2.6.0-preview.26515.1` |
| `DotNetFinalVersionKind=prerelease` | `2.6.0-preview.final` | `4.6.0-preview.final` | `2.6.0-preview.final` |
| `DotNetFinalVersionKind=release` | `2.6.0` | `4.6.0` | `2.6.0` |

Developer/CI evaluations also matched: `2.6.0-dev` / `2.6.0-ci`. The final CTRF
package depends on `[2.6.0, 3.0.0)` and has no prerelease dependencies. Its SHA-256
is `C120664B9203E3316E3A7C183401C6692BB332F18DB4EB094F54C76629DF58E9`.
These are local simulations of release selections, not signed or published
packages.

Unsuppressed C# 12 consumers of stable CTRF/MTP 2.6.0 built and ran on net8.0 and
net462. On each target, enabled/pass, disabled/pass, enabled/fail and enabled/empty
produced exits `0/0/2/8`, test counts `1/1/1/0`, and report counts `1/0/1/1`.
Actual loaded paths and DLL bytes were checked against the shipping packages.
All seventeen reports from the aligned acceptance/stable-consumer runs passed
their immutable schemas with format checks: two legacy 0.1.0 and fifteen 0.2.0.

### Reproduction

The pinned SDK is `12.0.100-alpha.1.26509.101`; commands ran on Windows x64.
Developer packs used the following command for the MTP, CTRF and MSTest.Sdk
projects, in that order, each exiting 0:

```powershell
.\eng\common\build.ps1 -restore -build -pack -projects <project> -msbuildEngine vs -nodeReuse:$false -binaryLogName <unique-binlog> /m:1 /p:FastAcceptanceTest=true /p:EnableCodeCoverage=false
```

The acceptance runner was built with the same command without `-restore -pack`.
With the repository `.dotnet` first on `PATH` and `DOTNET_ROLL_FORWARD=Major`:

```powershell
.\.dotnet\dotnet.exe exec artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net11.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~SdkTests.CtrfReport_UsesPlatformVersion_WhenOptedIn|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportTests|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CtrfReportRetryAttributeTests'
.\.dotnet\dotnet.exe exec artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Debug\net11.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~CtrfReportTests.Ctrf_MergingLegacyAndCurrentReports_PreservesVersionAndAttemptIdentities'
```

Those executions passed 5 and 2 cases respectively, exit 0. Each also used
`--report-trx --report-trx-filename aligned.trx --results-directory <owned-directory>`.
The red phase selected only the new SDK method, kept its assertions unchanged,
and consumed the preserved parent packages. No compilation or setup failure is
counted as regression proof.

Release variants used `.\eng\common\dotnet.ps1 pack <project> -c Release
/nr:false -bl:<unique-binlog> /p:ArtifactsDir=<variant-directory>
/p:OfficialBuild=true /p:OfficialBuildId=20261015.1 /p:EnableCodeCoverage=false
/m:1`, plus the final-version kind shown above. MTP/CTRF also used
`/p:BuildProjectReferences=false`; the SDK retained its dependency graph.
Packed SDK defaults were queried with `dotnet msbuild <probe-project>
-getProperty:MicrosoftTestingPlatformVersion,MicrosoftTestingExtensionsCommonVersion,MicrosoftTestingExtensionsCtrfReportVersion
-bl:<unique-binlog>`. All packs and evaluations exited 0.

Parallel-safety preflight: the SDK cases each own a randomly generated asset,
including their bin/obj and result directories. They use the assembly's existing
per-run package cache without mutating it during execution. Method-level
parallelism and the existing CTRF acceptance-fixture exclusion are unchanged.

Text transcripts and package metadata communicate this nonvisual change better
than screenshots. The initial API-marker red/green proof and 630 unit / 28
acceptance / 73 schema-valid report results belong to the linked initial
revision, not automatically to this alignment. No fresh cross-platform,
NativeAOT, packaged-app activation or servicing-branch validation is claimed.

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
[initial-record]: https://github.com/microsoft/testfx/blob/a5d567ee14422f86882e81f3e0d4708dbd81fafc/docs/decisions/ctrf-stable-release.md
