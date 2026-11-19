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
Older peers may omit the property. No protocol change is planned.

The SDK is responsible for serializing any final Azure DevOps groups after
hosts finish; live per-host groups can overlap. Normal, no-handshake,
cancellation, partial, incompatible and failed-host paths must preserve their
existing exit and fallback semantics. Do not buffer whole host output or derive
aggregate coverage percentages from incompatible measurements.

## Research and experiments

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

| Risk | Planned evidence | Rendered check / gap |
| --- | --- | --- |
| Parallel module identity and test outcome | SDK and packed-host mixed-module execution, plus focused reporter cases | Inspect actual terminal; hosted Azure DevOps is separate |
| Missing TFM, runtime difference, legacy peers | Handshake/identity and compatibility cases | Verify labels do not invent a TFM |
| Command fidelity, failure and cancellation | SDK composed tests, including nonzero and no-handshake paths | Preserve original command bytes; inspect final verdict ordering |

## Open questions

The coordinating parent owns SDK dependency flow, shipping-layout validation,
rollout, release-branch and servicing decisions. Package versions and exact
shipping branches have not yet been established. Publication is held until the
parent confirms paired validation.
