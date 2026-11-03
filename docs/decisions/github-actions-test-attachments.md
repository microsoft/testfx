# GitHub Actions test attachments: research and decisions

## Problem and contract

[Issue #12131](https://github.com/microsoft/testfx/issues/12131) requests automatic
publication of failed-test attachments and links from the test report. This is a
new reporting feature, not a regression in an existing upload implementation.

The producer is `Microsoft.Testing.Extensions.GitHubActionsReport` (MTP 2.6,
developed on `main` for MSTest 4.6 / MTP 2.6). MSTest and VSTestBridge already
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
| Packed consumer and real framework attachment | Eight generated-consumer acceptance cases retained; package build and execution pending. |
| Real GitHub Actions archive and download link | Pending composed workflow execution. |

Focused unit validation compiled current production and test sources with the
pinned SDK, `net8.0`, code coverage disabled, and warnings treated as errors.
Selection:

```text
FullyQualifiedName~GitHubActionsAttachmentCollectorTests|FullyQualifiedName~GitHubActionsSummaryReporterTests|FullyQualifiedName~GitHubActionsCommandLineProviderTests
```

Result: **269 passed, 0 failed, 0 skipped; exit 0**, including all **70 added
cases**. TRX, JUnit and CTRF exports were parsed and reconciled. This is executed
unit evidence; feature-ablation and shipping-package evidence remain pending.

## Open questions

- This feature targets the next minor version, MSTest 4.6 / MTP 2.6. No servicing
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
