# Parallel test output: research and decisions

## Problem and contract

Parallel `dotnet test` hosts share a log stream. Readers need the originating
assembly and declared target framework for lifecycle and test output without
confusing per-test skips with modules omitted by affected-test selection. A
target framework is not necessarily the executing runtime. Keep existing test
selection, result counts, exit codes, report identities, and standalone output.

The SDK owns child processes, their pipe identities, and the shared output
writer. TestFx owns the source-shared terminal reporter and the Azure DevOps
extension. Protocol 1.2 forwards Azure DevOps output with execution and instance
IDs; 1.3 forwards other display messages with those IDs. Both streams can contain
commands as well as ordinary text. Preserve logging commands verbatim, including
their syntax; do not assume every forwarded message is safe to prefix. A missing
declared TFM is unknown, not the runtime TFM. Handshake property 21 is optional
and carries the declared TFM; `Framework` describes the executing runtime.
Older peers may omit the property. The later progress-forwarding investigation
required a separate optional capability, documented below.

The SDK is responsible for serializing any final Azure DevOps groups after
hosts finish; live per-host groups can overlap. Normal, no-handshake,
cancellation, partial, incompatible and failed-host paths must preserve their
existing exit and fallback semantics. Do not buffer whole host output or derive
aggregate coverage percentages from incompatible measurements.

## Research and experiments

- 2026-11-19: The SDK's composed preview run displayed module heartbeats but
  no slow-test names. A diagnostic run of the same packaged MSTest host
  confirmed it publishes an in-progress node before executing the test.
  `DotnetTestDataConsumer` constructs the corresponding pipe message for a
  non-IDE consumer, but `DotnetTestConnection.SendMessageAsync` has no dispatch
  case for that message, so it is dropped. The SDK confirmed that older peers
  can negotiate protocol 1.0 without recognizing this message. Dispatch it
  only when the SDK explicitly advertises support in its handshake; missing,
  false, invalid, or incompatible capabilities retain the previous no-progress
  fallback. IDE progress continues through test-result messages. This
  optional SDK reply property (`TestInProgressSupported`, byte 22, boolean
  `true`) changes the original no-wire-change plan because a base-version
  check cannot distinguish older 1.0 SDKs without the serializer and handler.
  The paired SDK owns advertising support only on accepted handshakes; the
  TestFx host owns dispatch. The source-only ServerMode client links JSON-RPC
  sources, not this dotnet-test pipe handshake. No backport is planned.
- 2026-11-19: The coordinator identified three gaps against the approved
  mockup: host-owned error annotations need an assembly identity, module labels
  need optional non-outcome colors, and artifact paths need their own counted
  end-of-run disclosure. The existing artifact list contains file artifacts
  rather than necessarily only per-test attachments, so its count must be
  labeled as artifacts. The SDK owns coverage aggregation and its final output;
  the shared reporter cannot synthesize a combined percentage.
- An interim SDK composed run showed that printing "0 skipped (No affected
  tests selected)" for a normal run implies selection took place. Omit that
  clause when no modules were unselected; keep it when selection did omit any.
- A rejected handshake can be a retry of an already registered execution, so
  adding handshake failures to failed-module totals can count the same module
  twice. Omitting contextual module totals whenever a handshake failure is
  recorded avoids asserting an unknowable count. Preserve the failure recap,
  existing error counters, exit policy, and any known unselected-module count.
- 2026-11-19: The approved design is an illustrative mockup, not product
  evidence. The predecessor PR #12537 was merged; this work starts at
  `3cfabcf45c89a2d35079e589b7318f62a7091f83` on a new branch.
- The source-shared reporter already keeps per-execution state and the actual
  target/runtime distinction. `AzureDevOpsLogMessage` includes ordinary slow
  notices as well as commands; generic `DisplayMessage` can carry GitHub Actions
  commands. Message type alone is not a command discriminator.
- `TerminalReporterContract.props` shares terminal source with downstream
  consumers; dotnet/sdk separately vendors its files. The SDK must adopt the
  exact reviewed reporter revision before enabling changed presentation.
- The agent implementation parses `##vso[` within a line, so the mockup's claim
  that every prefixed command is ignored is too strong. The contract still
  preserves command-bearing lines, including GitHub workflow commands.
- `--report-azdo-groups on` is owned by each host, so parallel group boundaries
  cannot safely be suppressed by matching arbitrary user output. The SDK sets
  `TESTINGPLATFORM_DOTNETTEST_DISABLE_AZDO_HOST_GROUPS=1` only for this mode;
  aligned hosts disable their own group reporter. Older hosts ignore the
  override, and their original commands must pass through unchanged, so their
  groups can still overlap.

## Alternatives and decision

Leaving all output unchanged preserves compatibility but does not solve
attribution. Whole-host buffering delays failures and complicates cancellation.
Prefer a separately gated contextual presentation, with live output and a
single SDK-owned writer for end-of-run groups. The SDK enables it only on an
Azure Pipelines run with multiple parallel modules, including affected-test
runs. A presentation-only selector may shorten paths only when identities remain
distinguishable. Keep existing behavior when the new mode is not selected.
The host-message writer requires the SDK to frame partial reads and emits a
newline for a final unterminated line. A command-bearing line gets a preceding
newline after a color reset so an earlier ANSI reset cannot displace the command
from column zero; adjacent command lines in the same payload need no additional
separator. The command-bearing lines themselves are unchanged.

Use stable, presentation-only colors derived from the module identity rather
than outcome colors; reset them before writing severity and never color command
lines. For contextual summaries, close the module group before opening a
separate counted artifact group. Retain the normal artifact output for
standalone and suppressed retry summaries, and omit the group when empty.
For host-owned failure annotations, include the full module identity only in
contextual mode. Escape percent in that identity as `%AZP25` before applying
the existing command escapes: the Azure agent decodes `%3B` and other command
escapes before decoding `%AZP25`. With `DECODE_PERCENTS=false`, the agent
leaves `%AZP25` visible; that limitation avoids conflating a literal `%3B`
with a semicolon. Standalone escaping remains unchanged.

## Validation and demonstration

Planned: focused source-linked reporter tests for identity, outcomes, retries,
selection, and concurrent lifecycle; real aligned packed-host and SDK composed
execution with distinct modules and outcomes; terminal rendering at normal and
constrained widths. Compare the same tests against a buildable behavior-removal
counterfactual. Record exact package hashes, commands, results, and remaining
gaps before delivery. Hosted Azure DevOps rendering cannot be inferred from a
local terminal capture.

The initial source build raced in-flight edits and failed; it is not behavioral
evidence. The extension and platform subsequently built for netstandard2.0,
net10.0 and net11.0 using `.\build.cmd -projects
src\MTP\Microsoft.Testing.Extensions.AzureDevOpsReport\Microsoft.Testing.Extensions.AzureDevOpsReport.csproj
-c Debug -bl` (exit 0). The reporter unit-test project built across its target
frameworks using `.\build.cmd -projects
test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj
-c Debug -bl` (exit 0). Following test-only corrections, the command
`.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests
-f net10.0 -c Debug --no-build -- --filter
'FullyQualifiedName~TerminalTestReporterTests|FullyQualifiedName~SilenceDrivenHeartbeatRendererTests'`
passed 270 tests, with none skipped (exit 0). This run is not red-phase
evidence. The extension tests built using `.\build.cmd -projects
test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj
-c Debug -bl`, and `.\.dotnet\dotnet.exe run --project
test\UnitTests\Microsoft.Testing.Extensions.UnitTests -f net10.0 -c Debug
--no-build -- --filter 'FullyQualifiedName~AzureDevOpsLogGroupReporterTests'`
passed 14 tests with none skipped (both exit 0).

Review round 1 identified ordinary lines containing `global::` being mistaken
for GitHub workflow commands; detection now requires the GitHub marker at the
start of a line after leading whitespace, matching
[the runner parser](https://github.com/actions/runner/blob/main/src/Runner.Common/ActionCommand.cs),
while Azure `##vso[` is still recognized anywhere. The
independent review also raised an internal API tracking question, which is
not a build failure: the pinned analyzer excludes `[Embedded]` types. Shared
reporter inventory still follows the repository's manual baseline convention.
Review round 2 found that suppressed retry summaries still emitted subset
module counts; the contextual group now respects `ShowRunSummary=false`.
The follow-up review found that a literal `%3B` in a module path collided with
a semicolon after Azure command decoding. The corrected `%AZP25` ordering was
reviewed against the agent's implementation; the opt-out decoding limitation
above remains.

After the summary and color changes, the focused reporter and heartbeat tests
passed 272/272. After the escaping correction, the extension tests built with
`.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Extensions.UnitTests
-f net10.0 -c Debug --no-restore -v:q`; the selection
`.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Extensions.UnitTests
-f net10.0 -c Debug --no-build -- --filter
'FullyQualifiedName~AzureDevOpsTests|FullyQualifiedName~CiReporterLifecycleBehaviorTests'`
passed 40/40 with no skips (both exit 0). A previous run failed two new
assertions because raw string source line endings were CRLF while the decoded
message uses LF; the expectations now normalize both operands. The initial
normalization used `ReplaceLineEndings`, which does not compile for the test
project's net462 and net472 targets; it was replaced with `Replace`.

The aligned preview pack from source revision
`ea45e5221eccb3cfb738b27c264f680b773cfbc6` succeeded with zero warnings and
errors using `.\build.cmd -pack -c Release /p:VersionSuffix=preview.26599.1`.
The flat feed is `artifacts\packages\Release\Shipping`. Its
`Microsoft.Testing.Platform.3.0.0-preview.26599.1.nupkg` SHA-256 is
`823D53907949B43E6F358258558175136C66DE2032BAFA2D801D018DC5DF703B`,
and `Microsoft.Testing.Extensions.AzureDevOpsReport.3.0.0-preview.26599.1.nupkg`
SHA-256 is `D63CBEE1FA9F4394CFF82C00EEB4F9A6DC5318B8EB59B53C6BD3F45C475B05A3`.
The MSTest family is version `5.0.0-preview.26599.1`. The Azure DevOps
extension's nuspec requires Platform `[3.0.0-preview.26599.1, 4.0.0)`.
The feed also contains earlier `-dev` packages, so the acceptance harness
must use a preview-only copy to avoid its single-version check.

For the progress-forwarding fix, the reviewed product source is
`817bcecb31d6827b228ce5b103ef340009388d93`. A distinct Release pack,
`.\build.cmd -pack -c Release /p:VersionSuffix=preview.26599.4 -bl`, succeeded
with zero warnings and errors. The Platform and Azure DevOps packages are
`3.0.0-preview.26599.4`; MSTest packages are `5.0.0-preview.26599.4`. Platform
package SHA-256 is
`079535B06BC2DBF49471AF51768AAE578AF00E70BF29850439939A11EB4AB3FB`;
Azure DevOps package SHA-256 is
`F52B2C05969E2E269F5E73E32F2DD17082D57AE51240FC582FE8587B793B866F`.
The Azure DevOps package requires Platform
`[3.0.0-preview.26599.4, 4.0.0)` and the MSTest package requires the matching
adapter and framework. The previously shared `.1` packages were not changed.

The same four `DotnetTestPipe_ProgressHonorsPeerCapability` cases ran against
isolated `.1` and `.4` package feeds, each with a fresh generated-consumer
cache. In the `.1` run, `ProgressCapableSdk` failed because the expected
progress frame was absent; the legacy, explicit-false, and IDE cases passed
(3 passed, 1 failed, 0 skipped; exit 2). In the `.4` run all four passed
(4 passed, 0 failed or skipped; exit 0); the capable case verifies execution,
instance, and test identities and ordering before the final result. Both runs
asserted that the cached and deployed Platform DLL matched their packed
payload. The `.1` and `.4` net10.0 payload SHA-256 values were respectively
`7CC9B46A8921556C5A60065B2FE3B775C9FCD79ADA120291C2B0C2D75E388AA1` and
`EF7D980849C2BD064291DFE5616AA1B98C77EF112A2473C609BAFD29CE03DF80`.
The fixed-host command was:

```
X:\progress-acceptance\.dotnet\dotnet.exe X:\progress-acceptance\artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net12.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~DotnetTestPipe_ProgressHonorsPeerCapability' --report-trx --trx-mode in-process --results-directory X:\progress-acceptance-evidence --report-trx-filename progress-wire.trx --show-test-results all
```

Independent discovery found four cases and the acceptance execution verifier
confirmed four selected, executed and passed. On the same `.4` feed,
`DotnetTestPipeAzureDevOpsForwardingTests` passed four of four (exit 0), also
verified against its discovery plan and TRX. The Release/net10.0 capability
unit selection passed 13 of 13 (exit 0), including malformed or incompatible
capabilities and renegotiation. These fake-SDK wire tests do not replace the
paired real SDK run.

For the rejected-handshake tally, the same source-linked reporter tests and
assertions were run before and after the summary fix, in the task-owned
worktree with Release/net10.0. Test file blob:
`d922773690c54f088f6db27ef895455bb5adea2b`. The pre-fix product source was
`817bcecb31d6827b228ce5b103ef340009388d93`; all three cases executed and failed
at the expected group-header equality (exit 2): `RejectedHost`,
`RejectedRetry`, and `RejectedRetryWithUnselectedModule`. The corrected
`TerminalTestReporter.Summary.cs` blob
`911f21aed12dc878f6c2247ce9dced3fec8f26de` passed those same three cases
(exit 0, no skips). The tests also check that the handshake recap and failure
verdict remain visible. Both runs rebuilt the project with
the following commands, respectively:

```
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Release -f net10.0 --no-restore '-bl:artifacts\log\Release\ambiguous-summary-red.binlog' -v:q
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Release -f net10.0 --no-restore '-bl:artifacts\log\Release\ambiguous-summary-green.binlog' -v:q
.\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\net10.0\Microsoft.Testing.Platform.UnitTests.dll --filter 'FullyQualifiedName~TestExecutionCompleted_ContextualSummaryOmitsAmbiguousModuleTotals' --report-trx --trx-mode in-process --results-directory <session-files>\summary-parity-red --report-trx-filename summary-red.trx --show-test-results all
.\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\net10.0\Microsoft.Testing.Platform.UnitTests.dll --filter 'FullyQualifiedName~TestExecutionCompleted_ContextualSummaryOmitsAmbiguousModuleTotals' --report-trx --trx-mode in-process --results-directory <session-files>\summary-parity-green --report-trx-filename summary-green.trx --show-test-results all
```

`<session-files>` replaces the local session's private absolute path. The
separate red and green TRX files contain three executed results each. This
is a source-linked unit comparison; the paired SDK must still exercise its
real orchestration and published reporter output.

On the corrected source, the Release/net10.0 reporter, heartbeat, connection,
and data-consumer unit selection passed 410/410, exit 0, with method-level
parallelism. The new cases use per-test reporter and console instances and do
not mutate process-wide state. The selector was
`FullyQualifiedName~TerminalTestReporterTests|FullyQualifiedName~SilenceDrivenHeartbeatRendererTests|FullyQualifiedName~DotnetTestConnectionTests|FullyQualifiedName~DotnetTestDataConsumerTests`.

A focused independent review of the progress capability delta
`d00503457a00fe2b86e6483f0dc1437e59b7e7a0..817bcecb31d6827b228ce5b103ef340009388d93`
found no remaining concrete compatibility defect after retracting an assertion
argument-order concern: MSTest `IsLessThan` takes the upper bound first, and
the `.4` wire case passed. The review did not execute tests and identified the
real shipping SDK handshake as requiring paired composed validation.

On the earlier `.1` source revision `ea45e5221eccb3cfb738b27c264f680b773cfbc6`,
Release/net10.0 reporter and heartbeat tests passed 272/272,
and the Azure-selected extension run passed 637/637, with no skips:
`.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests
-f net10.0 -c Release --no-build -- --filter
'FullyQualifiedName~TerminalTestReporterTests|FullyQualifiedName~SilenceDrivenHeartbeatRendererTests'`
and `.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Extensions.UnitTests
-f net10.0 -c Release --no-build -- --filter 'FullyQualifiedName~AzureDevOps'`
(both exit 0).

The final packed-host pipe selection passed 4/4 with no skips (exit 0) in an
isolated `ea45e5221eccb3cfb738b27c264f680b773cfbc6` worktree. Its
preview-only shipping and nonshipping feeds were copied from the pack above;
Platform, Azure DevOps extension and MSTest framework package SHA-256 values
matched the originals. `AcceptanceFixture` selects a fresh per-run NuGet cache.
A temporary `X:` drive mapped the session `files`
directory to avoid Windows long paths and ancestor analyzer configuration:

```
X:\parallel-output-preview-acceptance\.dotnet\dotnet.exe X:\parallel-output-preview-acceptance\artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net12.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~DotnetTestPipeAzureDevOpsForwardingTests'
```

Initial attempts with the mixed-version feed, a long executable path, and a
nested alias failed during fixture setup; none are behavioral failures.
This fake-SDK pipe test does not replace the real SDK shipping-layout run.

The corrected Release pack (`.\build.cmd -pack -c Release -bl`) succeeded.
After repacking, the reporter and heartbeat selection passed 271 tests on
net10.0, with none skipped. A packed-host run of
`DotnetTestPipeAzureDevOpsForwardingTests` passed four cases on net12.0,
including the host-group override and legacy 1.1 peer:
`.\.dotnet\dotnet.exe run --project
test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests
-c Release --no-build -- --filter
'FullyQualifiedName~DotnetTestPipeAzureDevOpsForwardingTests'` (exit 0).
This fake-SDK pipe test does not replace the real SDK shipping-layout
validation. Other behavior sensitivity, resolved-package provenance, and
rendered-surface validation remain pending.

An isolated, buildable mutation tested the command-classification regression:
the corrected `TrimStart().StartsWith("::", ...)` was replaced with an
anywhere `IndexOf("::", ...)` while retaining the same tests. Against base
`3cfabcf45c89a2d35079e589b7318f62a7091f83` and source/test patch SHA-256
`3fe4a55db57d812a699b575dbdc5e155a2c56d4ba534800e1ed7f74160295324`,
both Debug/net10.0 variants built with SDK `12.0.100-alpha.1.26562.108`.
The same `ErrorSeverity` and `WarningSeverity` cases passed 2/2 on the
corrected variant (exit 0) and failed 2/2 on the mutant (exit 2), with no
skips. Their exact-output assertions showed that `global::Example.Type` lost
its module identity and severity color. The later retry-summary guard does
not alter the tested method or these assertions. This is a targeted mutation,
not a pristine-baseline run or proof of the other changed behaviors.

Additional isolated, buildable mutations retained the final tests but changed
module label colors, removed the separate counted artifact group while keeping
its contents, and removed contextual percent escaping. The mutant was based on
`7af3b1fe530272d3b3f2ea0da02ed165745e85ac` with the source/test patch
SHA-256 `D3C46D43B873610A3D3F84CF29CFDA35B2CD4E7A1BE7854147662108ED7CF0FB`.
The corrected revision's Release runs above passed the same three targeted
tests. In the mutant's Debug/net10.0 build, the following commands executed
with no skips and exited 2:

```
.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 -c Debug --no-build -- --filter 'FullyQualifiedName~TestCompleted_ContextualOutput_UsesSelectorToDisambiguateDuplicateModuleNames|FullyQualifiedName~TestExecutionCompleted_ContextualSummary_OrdersExitDescriptionAndSeparatesSelectionSkips'
.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Extensions.UnitTests -f net10.0 -c Debug --no-build -- --filter 'FullyQualifiedName~EmitsMessageOnlyAnnotation_EscapesLiteralPercentSequenceInAssemblyIdentity'
```

The two reporter tests failed their color and missing-group assertions (2/2);
the annotation test failed its expected `%AZP25` command assertion (1/1).
These mutations establish sensitivity to those specific behaviors, not to
the entire feature.

| Risk | Planned evidence | Rendered check / gap |
| --- | --- | --- |
| Parallel module identity and test outcome | SDK and packed-host mixed-module execution, plus focused reporter cases | Inspect actual terminal; hosted Azure DevOps is separate |
| Missing TFM, runtime difference, legacy peers | Handshake/identity and compatibility cases | Verify labels do not invent a TFM |
| Command fidelity, failure and cancellation | SDK composed tests, including nonzero and no-handshake paths | Preserve original command bytes; inspect final verdict ordering |

## Rollout

This presentation feature targets main alongside the paired SDK main change;
no servicing backport or downstream release enablement is part of this work.
Aligned hosts are required to suppress host-owned groups; legacy hosts can still
emit overlapping groups. The coordinating parent owns paired shipping-layout
validation and the publication gate.
