# GitHub Actions test attachments: research and decisions

## Problem and contract

[Issue #12131](https://github.com/microsoft/testfx/issues/12131) requests automatic
publication of failed-test attachments and links from the test report. This is a
new reporting feature, not a regression in an existing upload implementation.

The producer is `Microsoft.Testing.Extensions.GitHubActionsReport`, developed on
`main` for the issue's next open milestone, MSTest 5.0 / MTP 3.0. The current
checkout's build versions remain MSTest 4.6 / MTP 2.6 and are identified
separately in the validation evidence below. MSTest and VSTestBridge already
publish `FileArtifactProperty` values on test nodes. The consumer is the official
GitHub Actions artifact uploader, followed by a workflow-owned summary step.
No new core MTP API, transport protocol, GitHub token in the test process,
companion action, or service dependency is required.

The opt-in contract is `--report-gh --report-gh-attachments <absolute-directory>`.
Without the attachment option, existing report behavior remains unchanged.
Only terminal failed tests are collected, including error and timeout outcomes;
superseded retry results and session-level artifacts such as dumps are excluded.
Each process/session owns a unique subdirectory and a versioned manifest mapping
test UID, names, module, target framework, architecture, and retry attempt to
staged files. Attachments are copied as the failed result is consumed, rather
than waiting for session-end rendering. The producer must keep the original
available until consumption; a file removed earlier is reported unavailable.
Once copied, later cleanup cannot remove the staged copy. Files and retained
metadata are bounded.

The extension stages only explicitly declared regular files using generated,
artifact-safe destination names. Missing, unreadable, or over-budget attachments
produce warnings and manifest status entries, not successful file paths. These
failures do not change the test-process verdict. Cancellation propagates and
must not leave a success entry for a partial copy. The workflow owns retention
and removal of its staging directory.

The ordinary test summary remains available even when upload is skipped or
fails. Failure details list archive-relative paths, not fabricated download URLs.
The workflow publishes a real artifact URL in supplementary per-test details
only after a successful upload. The documented post-upload script is exercised
verbatim by the hosted smoke workflow; no separately shipped action is needed. One
archive contains all modules' attachments and their manifests. GitHub does not
provide supported links to individual files inside that archive.

## Research and experiments

### 2026-10-29: feasibility investigation

- At `8d3c9e82aca134b6d85ad583cc618c71dbc2af6e`, traced MSTest and VSTestBridge
  attachment production, GitHub Actions summary rendering, TRX consumption, and
  Azure DevOps publishing. Attachment data exists but GitHubActionsReport does
  not retain or publish it.
- The [artifact REST API](https://docs.github.com/en/rest/actions/artifacts)
  provides list/get/download/delete, not an upload endpoint.
- The [official artifact toolkit](https://github.com/actions/toolkit/tree/main/packages/artifact)
  uses `ACTIONS_RUNTIME_TOKEN` and `ACTIONS_RESULTS_URL`. The runner injects these
  for JavaScript actions, not ordinary shell steps.
- [Completed step summaries cannot be modified by later steps](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands#removing-job-summaries).
  Deferred rendering was initially proposed to acquire URLs before rendering.

### 2026-11-03: multi-model plan review

- Updated the clean task branch to `3a95626573dd6932505a860cbd059dae4f79a81d`.
- GPT-6.1 Sol and independent Claude Opus 5.5 reviewed the plan. Both support
  opt-in staging and workflow-owned upload. The independent review recommended
  preserving the existing summary and dropping a companion action.
- Adopted that simpler design: a later supplementary summary holds the real
  download link. Losing an upload must not hide the original failure diagnostics.
- A companion action was considered but not implemented: it would add release,
  pinning, Node-runtime, and GHES ownership without being needed for the upload.
- Initial baseline attempts exposed a local missing `powershell` launcher in
  `PATH`, before product compilation. These are tooling failures, not red-phase
  evidence for the feature.
- Independent code-review round 1 found a failure-isolation bug: unexpected
  collector errors could discard a result before it reached the summary. Capture
  now has its own logged failure boundary, retaining the underlying result.
- Required absolute staging paths so separate test-host working directories
  cannot scatter an invocation's attachments. Expanded the smoke workflow's
  triggers to its producer and build inputs, and removed unnecessary Actions
  permission.
- Confirmed consumer serialization in `AsyncConsumerDataProcessor.net.cs` and
  `BlockingConsumerDataProcessor.cs`; `CommonTestHost.SessionNotifications.cs`
  drains consumption before consumer session-finish callbacks. No additional
  collector lock is required. This is source-backed ordering evidence, not a
  concurrency stress-test result.
- Independent code-review round 2 found that the smoke script's package-prefix
  glob also matched sibling packages. Selection now requires the exact package
  ID followed by a numeric version and excludes symbol packages.
- Retained manifest associations before copying so a warning-delivery failure
  cannot orphan an earlier staged file. Unexpected attachment failures also
  attempt a guarded user-visible warning, without discarding test results.
- The post-upload sample now reports truncated manifests as well as truncated
  display groups. Its workflow also watches the MSBuild and TRX consumers.
- Independent round 3 was clean at
  `6b9bbb4b0f956a32f1920e2b8e53410fa85e8da2`.
- The first executable ablation exposed nullable test inputs being dereferenced
  before an assertion. Those exceptions were not counted as sensitivity proof.
  Added explicit non-null contract assertions, retained them in both variants,
  and repeated the same two tests green/red/green successfully.
- Real native `dotnet test` produced two module TRX files plus one merged TRX.
  The smoke originally counted the merged report as a third module. Corrected
  the harness to validate each module separately and validate optional merged
  totals without double-counting executions. The repeated composed smoke passed.
- While implementation was running, the 4.6 / 2.6 milestone was closed and the
  issue was advanced to MSTest 5.0 / MTP 3.0. Read back the live issue and preserved
  that concurrent maintainer decision rather than restoring the obsolete target.

## Alternatives and decision

1. **No change / upload the whole results directory:** preserves today's report,
   but loses per-test association and may miss attachments outside that directory.
2. **Upload in the test process:** rejected because it needs artifact-service
   credentials and backend integration unavailable in a normal `run:` step.
3. **Companion action and deferred primary summary:** rejected because it adds a
   shipping component and makes existing failure diagnostics depend on upload.
4. **Chosen: bounded staging, manifest, and official workflow upload:** preserves
   the existing summary, requires no additional credentials, and supports all
   existing extension target frameworks. Workflow configuration is required;
   this is not a promise of zero-configuration upload.

Attachment metadata in summary fragments is additive and optional so older
fragments remain readable. The shared summary models also compile into the
Azure DevOps extension; they must remain inert there. Retry final-failure
selection and multi-module aggregation must retain the association.

## Validation and demonstration

The first implementation compiles on `netstandard2.0`, its oldest supported
target, using the repository-bootstrapped SDK `12.0.100-alpha.1.26509.101`:

```powershell
.\.dotnet\dotnet.exe build src\Platform\Microsoft.Testing.Extensions.GitHubActionsReport\Microsoft.Testing.Extensions.GitHubActionsReport.csproj -f netstandard2.0 -p:TreatWarningsAsErrors=true -p:UseSharedCompilation=false -nodeReuse:false -bl:artifacts\log\Debug\attachments-netstandard.binlog -v:quiet
```

Result: exit 0, zero warnings and errors. Resources were regenerated using the
owning project's `UpdateXlf` target. `check_localization.py` and
`check_source_bom.py` passed. These checks do not prove staging or upload.

The shipping smoke script and workflow exercise freshly packed MSTest and
reporting packages through direct test-host execution and real multi-module
`dotnet test`, then download the uploaded artifact and compare its files and
association manifests. Until those commands execute, they are proposed
validation, not evidence. Focused test, counterfactual, package-provenance, and
hosted-run results will be added as obtained.

| Requirement | Evidence |
| --- | --- |
| Collection, limits, missing input, cancellation | Executed: `CaptureAsync_StagesExactBytesAndWritesManifest`, size/count boundary cases, `CaptureAsync_CanceledDuringCopy_PropagatesTokenAndDeletesPartialFile`, and warning-failure association cases. |
| Direct and aggregate failure details | Executed: `BuildMarkdown_WithAttachments_RendersExactArchiveRelativePaths`, `CreateModule_FragmentRoundTrip_PreservesAttachmentsOnlyOnFailures`, legacy-fragment and retry cases. |
| Packed consumer and real framework attachment | Executed shipping smoke: four direct host runs plus real multi-module `dotnet test`, 12 executions and five intentionally failed results, parent smoke exit 0. All eight packed acceptance cases and three net8 help/info cases passed (11/11, no skips). |
| Real GitHub Actions archive and download link | Executed: [hosted run 41629546864](https://github.com/microsoft/testfx/actions/runs/41629546864) passed at `dd69b2f47dc2ab1f4b9df23c8d2d3451f21ec1cd`; actual archive uploaded, downloaded and verified. |

Focused unit validation compiled current production and test sources with the
pinned SDK, `net8.0`, code coverage disabled, and warnings treated as errors.
Selection:

```text
FullyQualifiedName~GitHubActionsAttachmentCollectorTests|FullyQualifiedName~GitHubActionsSummaryReporterTests|FullyQualifiedName~GitHubActionsCommandLineProviderTests
```

Result: **269 passed, 0 failed, 0 skipped; exit 0**, including all **70 added
cases**. TRX, JUnit and CTRF exports were parsed and reconciled. This is executed
unit evidence. The same selection also passed **269/269** on the `net462`
target, running on the installed `net481` runtime and exercising the extension's
`netstandard2.0` implementation on Windows.

### Executed feature-ablation evidence

Base implementation: `6b9bbb4b0f956a32f1920e2b8e53410fa85e8da2`, with the two
explicit non-null test assertions retained in both variants. The support patch's
SHA-256 is `2DF6A32E9C2267B4B1B60A0C9265070280AB88AD375655F47F7F91E1C0A620C5`.
The buildable production ablation replaces only attachment extraction with an
empty array:

```diff
- FileArtifactProperty[] files = [.. testNode.Properties.OfType<FileArtifactProperty>().Take(MaxFilesPerTest + 1)];
+ FileArtifactProperty[] files = [];
```

The production-only patch SHA-256 is
`95A07BC3991A486811747B55BFBD51A89074FCB107C78DECA3E74F0224EA470B`.
An isolated task-owned worktree and its own outputs were used; no primary
checkout, other session, or shared package cache was reverted or cleared.

| Contract / test case | Without collection | Correct implementation |
| --- | --- | --- |
| `CaptureAsync_StagesExactBytesAndWritesManifest` | Executed: assertion failed, `A declared attachment must produce staged metadata.` | Executed: passed before and after ablation. |
| `SessionFinishing_DeferredFragment_RetainsAttachmentsForAggregateRendering` | Executed: assertion failed, `Deferred failure fragments must retain attachment metadata.` | Executed: passed before and after ablation. |

Each run selected/executed exactly two tests, with no skips. Green exits were 0
(2 passed); the ablated exit was 2 (2 intended assertion failures). The same
source test bytes were verified by SHA-256 in the primary and comparison
worktrees. All variants were rebuilt with the pinned SDK, Debug/net8.0,
`EnableCodeCoverage=false`, `UseSharedCompilation=false`, and
`TreatWarningsAsErrors=true`.

The identical selector was:

```text
FullyQualifiedName~GitHubActionsAttachmentCollectorTests.CaptureAsync_StagesExactBytesAndWritesManifest|FullyQualifiedName~GitHubActionsSummaryReporterTests.SessionFinishing_DeferredFragment_RetainsAttachmentsForAggregateRendering
```

### Executed packed-product smoke

```powershell
.\.github\scripts\test-gh-attachments.ps1 -WorkDirectory <fresh-task-owned-directory>
```

Executed on Windows with freshly packed `MSTest.TestFramework` and
`MSTest.TestAdapter` **4.6.0-dev**, and `Microsoft.Testing.Platform`,
`Microsoft.Testing.Platform.MSBuild`, `Microsoft.Testing.Extensions.TrxReport`
and `Microsoft.Testing.Extensions.GitHubActionsReport` **2.6.0-dev**.
The script checked each restored package's SHA-256 against its packed nupkg,
checked the selected runtime DLLs against the executable layout, and used a
fresh isolated NuGet cache.

Observed: opt-out and passing runs created no attachment staging; intentional
failed hosts exited 2, the passing host exited 0, and native multi-module
`dotnet test` exited 2 with exactly four executions (2 passed/2 failed).
All four session manifests retained the right module/test/file association and
actual bytes. Both module reports and the authoritative 4-result merged TRX
were checked. The outer smoke exited 0.

**Local packaging limitation:** the Release build produced 32 shipping packages
and compiled all selected source targets with zero warnings. The Windows-only
post-pack verifier expects legacy UAP payloads omitted by the required local
`SkipUapTargetFramework=true` dotnet build (MSTest/adapter/framework counts).
That full verifier exited 1; it was not weakened or called successful. The
Linux hosted workflow runs the repository's standard `build.sh -pack` path.
### Executed hosted upload/download

[Run 41629546864](https://github.com/microsoft/testfx/actions/runs/41629546864)
passed at `dd69b2f47dc2ab1f4b9df23c8d2d3451f21ec1cd` on ubuntu-latest.
The standard `./build.sh -pack -c Release -bl` step passed, followed by the
12-execution composed smoke, official upload, the exact documented post-upload
script, real archive download, and byte/association verification.

- [Failed-test attachments](https://github.com/microsoft/testfx/actions/runs/41629546864/artifacts/13227777694):
  four staged files and four manifests, all eight downloaded files verified.
- [Reproducible validation evidence](https://github.com/microsoft/testfx/actions/runs/41629546864/artifacts/13226914918):
  binlogs, actual package versions/hashes, TRX, source-resolved assets and summary
  transcripts.
- Artifact digest:
  `sha256:48579a59a0b7a0ae7c46596aca5982b220c20e9a53e8d9b31d6f8fe5fbfdddeb`.
- The verifier matched all four supplementary test-details blocks to their
  module/test/file and the actual returned archive URL. This proves generated
  output and real download behavior, not authenticated browser rendering.

The public browser showed the successful job and both artifacts. The available
browser is signed out and GitHub requires sign-in for job logs/summaries, so the
expanded per-test hosted UI, wrapping and controls were not visually inspected.
No substitute rendering or fabricated screenshot is claimed. The actual saved
Markdown and console transcript are the accessible demonstration.

The additional acceptance run compiled the Release/net12.0 outer host with
warnings treated as errors and executed net8.0 consumers. It selected exactly
eight `GitHubActionsAttachmentTests` cases plus the net8 rows of
`Help_WithAllExtensionsRegistered_OutputFullHelpContent`,
`HelpShortName_WithAllExtensionsRegistered_OutputFullHelpContent`, and
`Info_WithAllExtensionsRegistered_OutputFullInfoContent`. Exit 0: 11 passed,
zero failed/skipped. TRX/JUnit/CTRF counts agreed. Restored package SHA-512 and
deployed net8 DLL SHA-256 values matched the freshly packed artifacts.

## Open questions

- This feature targets the next open version, MSTest 5.0 / MTP 3.0. No servicing
  backport is planned: existing behavior is not a regression.
- `upload-artifact` v4+ does not support GitHub Enterprise Server. There the
  workflow must choose a supported uploader; staging and failure paths remain
  useful without claiming a downloadable URL.
- Attachments can contain sensitive test data and are readable by people with
  repository read access. Collection is opt-in; callers must review their
  artifacts and retention policy.
- The implementing owner retains responsibility through package release and
  workflow rollout. CI/local evidence must distinguish upload success from
  staging success and explicitly identify unavailable hosted-UI checks.
