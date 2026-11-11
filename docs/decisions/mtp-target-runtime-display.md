# Native MTP target and runtime display

## Problem and contract

[microsoft/testfx#7066](https://github.com/microsoft/testfx/issues/7066) reports
`dotnet test --framework net472` identifying the assembly as `net48` or
`net481`. The assembly target and installed runtime are different facts:
.NET Framework can run a `net472` application on an installed 4.8.1 runtime.
Replacing one with the other makes the selected build appear incorrect.

The user requested that the actual target remain primary while a meaningfully
different runtime remains visible. The agreed display is:

```text
Assembly.dll (net472|x64, runtime: net481)
Assembly.dll (net10.0|x64)
Assembly.dll (x64, runtime: net481)
```

The last line deliberately does not invent a target when none is known.

| Boundary | Ownership and contract |
| --- | --- |
| Application to MTP handshake | TestFx keeps property `Framework=2` runtime-only, with `RuntimeInformation.FrameworkDescription`. Optional property `TargetFramework=21` carries the application's declared target and platform. |
| Metadata provenance | Read `TargetFrameworkAttribute.FrameworkName` from the application entry assembly, not from the MTP package assembly or the executing runtime. Its optional display name is not authoritative. Append `TargetPlatformAttribute` when available. Missing or unrecognized target metadata remains absent. Generic controller/tool entry assemblies do not advertise their own target as the test application's. |
| MTP to SDK | The SDK prefers the project TFM, otherwise the optional declared handshake target. Older producers that omit property 21 still provide known runtime/architecture; older consumers ignore the additive property. No protocol-version bump or new message serializer is required. |
| SDK to shared renderer | Target, process architecture, and compact runtime are separate values. Preserve the full project target for display. Compare its platform-free base to the compact runtime, case-insensitively. |
| Runtime equivalence | Existing runtime parsing removes modern .NET servicing details. Do not erase meaningful .NET Framework releases: `net471`, `net472`, `net48`, and `net481` remain distinct. Unrecognized runtime descriptions stay explicitly labelled, even when an unknown target string happens to match. |
| Terminal lifetime | Store runtime alongside immutable per-execution target/architecture. Existing start overloads and the six-argument state constructor remain available and forward to additive runtime-aware overloads. Completion, recap, slow-test and progress identities use that state. |
| Width | Durable output retains the secondary runtime. Cursor progress can omit it first; reserve the primary target/architecture before eliding a long filename. Count the complete parenthesized identity and keep it outside the duration column. |
| Standalone | Existing callers leave the new runtime argument null. Keep the existing separate runtime banner and assembly target display, and leave report-name fallback logic unchanged. Omitted identity components do not leave dangling separators. |
| Execution and failure | Launch, handshake compatibility, cancellation, disposal, retries, result counts, and parent/child exit semantics do not change. Missing target metadata is not a handshake failure or a successful no-work signal. |

This is a coordinated TestFx/dotnet-sdk change. TestFx is the source of truth
for producer constants, metadata parsing, and shared terminal files. SDK
consumer wiring and hard-fork adaptations belong to the SDK counterpart.
The new shared `TestFrameworkDisplay.cs` must be appended to the SDK's
`eng/vendored-files.json`; TestFx's shared contract includes it by glob.
TestFx's existing per-test result renderer has no assembly identity; the SDK
fork applies the helper to its already-existing per-test identity without
introducing an unrelated standalone layout change here.

## Research and experiments

This record was started on 2026-11-10 from the session's maintained working
journal and inspected artifacts. Dates below identify actual observations;
they do not imply that this file existed before its first commit.

### 2026-11-10: producer and consumer investigation

- Read issue 7066 and inspected its attached image. Both command and output
  path selected `net472`, but native `dotnet test` displayed the runtime as the
  target.
- The selected TestFx base was
  `c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`, initially clean. Property 21 was
  unassigned.
- Existing `GetShortTargetFrameworkIncludingPlatform` deliberately falls back
  to runtime when assembly display metadata is absent/empty. Report naming
  relies on that behavior. Reusing it unchanged for optional target metadata
  would turn unknown target into a fabricated runtime target.
- Added a metadata-only parser that reuses the existing short-framework and
  platform logic. It reads the declared framework name even when its optional
  display name is empty or disagrees.
- The shared reporter tracks internal APIs and is compiled both in MTP and
  independent source-contract tests. Existing tracked signatures were kept,
  with additions recorded only in `InternalAPI.Unshipped.txt`.
- Before source freeze, the initially proposed six-argument string-runtime
  overload was removed: alongside the existing six-argument integer-attempt
  overload, it made a positional `default` argument ambiguous. The final API
  adds only `AssemblyRunStarted(..., int? attemptNumber, string? runtimeFramework)`,
  both extra arguments required, retaining the exact old five/six-integer
  signatures. The SDK owner agreed and adapted its source fork. The
  `AssemblyRunStarted_DefaultLiteralStillSelectsExistingAttemptOverload`
  regression preserves that existing call shape and expected argument check.

### 2026-11-10: display reviews and alternatives

The SDK owner ran the requested independent reviews with Claude Sonnet 5.5
(`claude-sonnet-5.5`) and GPT-5.6 Sol (`gpt-5.6-sol`). Both reported the
parenthesized, labelled runtime form as their first choice. This session
subsequently inspected their retained independent response excerpts,
configured/returned model identities, and concrete examples. These were
presentation reviews of the same bounded alternatives and examples, not code
inspection, executable tests, or rendered-terminal validation.

The SDK research base was
`6093e45a63de08d0c6cd7e1707420b63f9a9f983`. Its dependency manifest referenced
MTP `3.0.0-preview.26559.7` and MSTest `5.0.0-preview.26559.7`, from TestFx
`cb5805342d403ed9f7e7ddd116c81fee789ef7f9`. An earlier TestFx research revision
was `471a807cf8182e0bcd0d9967c8dbf002c04945d2`; neither should be confused with
this implementation's selected base or the released-package repro.

| Reviewer | Independent ordering and useful distinction |
| --- | --- |
| Claude Sonnet 5.5 | A/C/B. A keeps one self-explanatory group and stable `target\|arch` prefix. C is shorter but ambiguous, especially when target is unknown. Suggested the truthful `(x64, runtime: net8.0)` fallback and preserving target/arch ahead of filename truncation. |
| GPT-5.6 Sol | A/B/C. A expresses authoritative identity followed by secondary runtime. B is explicit but punctuation-heavy; C erases the explicit target/runtime distinction. Suggested `target unknown` and stripping platform only for comparison, never display. |

Both suggested hiding unrecognized runtime descriptions. The owner rejected
that suggestion: unknown is not equal, and available runtime information
should remain explicitly labelled. The owner selected Claude's shorter
unknown-target omission instead of GPT's longer label, and did not add
Claude's suggested arrow-based narrow fallback. Both agreed that modern .NET
servicing details should not add noise; .NET Framework release distinctions
were explicitly retained rather than treated as generic patches.

The alternatives actually discussed were:

| Alternative | Decision and reason |
| --- | --- |
| Keep runtime in the target slot | Rejected: reproduces the reported incorrect primary identity. |
| Show target only | Corrects the target but loses the runtime distinction the user explicitly wanted. |
| Use an arrow between frameworks | Not selected. Claude ranked it second for compactness and GPT third; both noted ambiguous direction/semantics. |
| Use two bracket groups | Rejected by both reported model reviews as noisier than one labelled group. |
| Show `target unknown` | Not selected: explicitly labelling known runtime/architecture is shorter and does not manufacture target metadata. |
| `(target\|architecture, runtime: runtime)` | Selected: compact matching cases, explicit differing/unknown runtime, and one shared formatter for identity surfaces. |

The comparison rule deliberately ignores platform suffixes only for
equivalence, not for display. A Windows target running on the matching base
runtime should not show a redundant runtime annotation or lose its Windows
target identity.

### 2026-11-10: build and local regression observations

- `build.cmd` initially failed before compilation because its native child
  could not find Windows PowerShell. A direct `eng\build.ps1` bootstrap
  installed the pinned SDK but hit the same problem in runtime-install
  wrappers. Supplying a native child-process PATH prefix fixed lookup without
  changing repository or global configuration.
- Pinned SDK: `12.0.100-alpha.1.26556.115`. Supported MTP builds covered
  `net10.0`, `net11.0`, and `netstandard2.0`; unit tests additionally built and
  ran `net462`.
- The existing focused baseline executed 374 tests on net10.0, all passed.
- An initial head build exposed an incorrectly placed parser insertion; that
  was corrected before the successful build. A subsequent compiler/analyzer
  pass rejected an unused import in the new shared helper; it was removed.
  These were build failures, not behavioral red-phase evidence.
- The first new lifecycle test run exposed a test-harness instance/execution
  identity mismatch. Correcting the fixture, without changing production
  behavior or assertions, yielded clean focused runs: 425/425 on net10.0,
  425/425 on net11.0, and 405/405 on net462, all with zero skips.
- Independent shared-source suites passed on both modern targets: 16 terminal
  contract tests and 28 protocol contract tests per target.
- The final overload shape then passed 426/426 on net10.0, 426/426 on
  net11.0, and 406/406 on net462, all with zero failed/skipped. Removing a
  newly redundant forwarding cast is a style-only follow-through to that
  overload simplification.
- Owning-project `UpdateXlf` regenerated all 13 terminal catalog changes.
  Localization guard: 41 self-tests passed and all 364 catalogs validated.
- A unique local shipping package was produced:
  `Microsoft.Testing.Platform.3.0.0-dev.framework7066.nupkg`. Actual package
  and consumer payload hashes are recorded with the remaining composed runs,
  not inferred from the package version.
- The first immutable producer transport had SHA-256
  `110395760EE20020837D99F329F90CF4945999F24ABC19354028941C5687FF99`.
  Its nuspec still identified the selected base because the source delta was
  uncommitted; it is not misrepresented as a final-commit build.
- The first wider prerequisite pack used `SkipUapTargetFramework=true` under
  dotnet MSBuild. It created the needed products but correctly failed the
  existing full-package layout guard for omitted MSTest UAP assets (three
  package-layout errors). The normal Windows pack without that exclusion then
  passed with zero warnings/errors and validated complete package content.
  No validation rule was weakened.
- The four new packed-host regressions were independently discovered and then
  executed on the actual net12.0 acceptance host. Final gate:
  planned=4, selected=4, executed=4, passed=4, failed=0, skipped=0, process
  exit=0. Each case compared the deployed and effective-cache MTP payload
  hashes with the selected shipping nupkg entry before fixture cleanup.
  An earlier standalone expectation incorrectly assumed the runtime banner
  used the raw description; the preserved banner is compact (`net481` here).
  The test was corrected to assert that existing compact banner shape,
  separate `net472` assembly identity, and absence of the new suffix.

### Inspected released-producer compatibility evidence

The coordinator supplied actual logs, TRX files, and package/source provenance
for an MTP 2.5.2 / MSTest 4.5.2 probe. This session inspected the retained
baseline/fixed logs and parsed baseline, fixed, and module-only TRX files.
Each reported one executed/passed test, zero failed/skipped. Source and
deployed package hashes were unchanged between the target-mismatch runs.

```text
Original SDK 11.0.100-rc.3.26522.148:
  FrameworkDisplayProbe.exe (net481|x64)

Corrected managed SDK 12.0.100-dev:
  FrameworkDisplayProbe.exe (net472|x64, runtime: net481)
```

The corrected CLI `dotnet.dll` SHA-256 in the inspected provenance was
`DAF9D209623A30EBDBFB916B2E755508B63B45DF4E55A8B2BE2BBDE414F02C96`.
The deployed MTP netstandard2.0 payload SHA-256 was
`124C24644715690BBDAC0D3345B53654A812BA9AC6D932B3CA58F18C7E8D389C`.

These are real SDK/old-producer compatibility observations, not proof of the
new field-21 producer. This session also reran the identical retained
`Assert-FrameworkDisplay.ps1` against both captured logs: exit 1 for the
original `(net481|x64)` identity, with the intended identity-mismatch
assertion, and exit 0 for the corrected start/completion pair. The product
itself exited zero in both cases; the red observation is the display
assertion, not a failed test launch or changed probe.

The coordinator also reported inspecting the actual
80x24 terminal and a compact matching net10.0 case; this session has not
independently inspected that rendered canvas.

### Inspected new-producer composed evidence

The SDK owner supplied a real MTP-only application using the unique v1
shipping package above, a built managed SDK, isolated cache, exact command
files, stdout/stderr, and independent execution XML for 13 cases. This session
read the runner and representative raw logs, parsed every execution XML, and
reconciled all counts with the retained summary.

The aligned CLI SHA-256 was
`DC03D4AE7C7E70E62BA66F80898FC7B6548165DC436B4AB12DF9019F4B43B422`.
This is a later CLI build than the first released-producer capture above.

| Actual composed cases | Expected/observed contract |
| --- | --- |
| Project and module-only net472 | `(net472\|x64, runtime: net481)`, exit 0, one executed/passed each. Module-only has no project target and exercises the new producer field. |
| Project and module-only net481 / net12.0 | Compact matching identity, exit 0, one executed/passed each. |
| Project and module-only net8.0, roll-forward to runtime 12 | `(net8.0\|x64, runtime: net12.0)`, exit 0, one executed/passed each. The app target, MTP package asset (`netstandard2.0`), and runtime are all different. |
| Missing TargetFrameworkAttribute, module-only | `(x64, runtime: net481)`, exit 0, one executed/passed; no invented target. |
| Missing TargetFrameworkAttribute, project | Known project `net472` stays primary, exit 0, one executed/passed. |
| Actual x86 process | `(net472\|x86, runtime: net481)`, exit 0, one executed/passed; not AnyCPU or parent architecture. |
| Deliberate failed test | Correct target/runtime identity, exit 2, one executed/failed and zero passed. |
| Zero-result selection | Correct target/runtime identity, exit 8, zero selected/executed/passed/failed. This is intentionally not an outer green-test execution gate. |

The effective cached/deployed payload observations were
`lib/netstandard2.0` for net472/net481/net8 and `lib/net11.0` for net12.
The netstandard2.0 SHA-256 was
`2B1032D73C490E212625965EC2D5B5C53F05B4867B8A273527AD0BBA18336E7A`;
the net11.0 SHA-256 was
`BB4A5770A2C1073804B6CC294F2D894C28D40746F1CA964CAB54A1510FFE6226`.

An additional actual SDK 11.0.100-rc.3.26522.148 run accepted the new producer
property, executed/passed one net472 test, and exited zero. Its old
`(net481|x64)` display remained unchanged, confirming the runtime field was
not repurposed. This session inspected its stdout and independent execution
XML too.

### Final source/package refresh

The implementation was committed locally as
`c2b618b17ef3ecbf46054cde3785476673ff9f63`, tree
`6d4341b40cdf7a5174ddec89116a75ef22655dba`. This follow-up record changes
evidence only; the producer/shared source remains that exact revision.
The SDK counterpart was committed locally as
`ce63d4292a5a7df62e651f12946e0570dd624d6d`.

The final immutable package is
`Microsoft.Testing.Platform.3.0.0-dev.framework7066.final1.nupkg`, SHA-256
`E476C77337E7AF644D03D475F36DCECFE8307CB66BB36058316F6BA6FCAF8139`.
Its nuspec source commit was read back and matched the implementation commit
exactly. A new version and fresh `.packages-final1` cache prevented an earlier
development package from satisfying the final consumer restore.

The final managed SDK CLI SHA-256 is
`EB6A8D101F8325448BEFFDEB40CA4A6A6F5CE36D2CF23FC59058BC4BF5B4579F`.
The 13-case composed matrix above was rerun on this final pair, and this
session reconciled every final independent execution XML with its retained
summary. All expected outcomes were preserved, including exits 0/2/8,
metadata absence, module-only target fallback, runtime roll-forward, and x86.
Original-SDK-11/final-producer reverse compatibility also passed one test and
exited zero. The SDK owner and coordinator separately verified the 363-case
SDK suite, including existing real multi-module/TRX-merge coverage.

Final cached/deployed package payloads were independently matched:

| Asset | Final SHA-256 |
| --- | --- |
| `lib/netstandard2.0/Microsoft.Testing.Platform.dll` | `EF1F8A12D8B8B736D53FEB07E7765AC9233128C4A87AC3B0E0EFCA5D3761EE1F` |
| `lib/net11.0/Microsoft.Testing.Platform.dll` | `35DE1521350C0C2EE04E9F90C0ED5DE15AEB5A62738795FA9A3F3EAADC574717` |

The four new packed regressions were rerun against the final package:
planned=selected=executed=passed=4, failed=skipped=0, outer exit=0.
The fresh TRX interval was 2026-11-10 15:36:23Z through 15:36:34Z.
Their in-test provenance checks compared both effective-cache and deployed
payload bytes with the current shipping nupkg.

The coordinator refreshed actual ANSI CLI inspection at its real 80x24
viewport on the final SDK hash. The existing terminal canvas cannot resize
its actual viewport, so a 40-column full CLI/PTTY inspection remains
unavailable, not simulated. Executed renderer-level checks are separate:

```text
width 40:  [✓0/x0/↓0] Tests.exe (net472|x64)(1s)
width 80:  [✓0/x0/↓0] Tests.exe (net472|x64, runtime: net481)(1s)
width 120: [✓0/x0/↓0] Tests.exe (net472|x64, runtime: net481)(1s)
long filename, width 40:
           [✓0/x0/↓0] ...get.exe (net472|x64)(1s)
```

These are retained actual renderer strings with ANSI control codes removed,
not cursor-resolved full-console screenshots. Their measured visible lengths
were 37, 54, 54, and 38; all fit the configured widths. The two independent
model reviews, these deterministic width checks, and actual normal-width CLI
inspection establish different evidence and are not conflated.

## Validation and demonstration

### Requirement coverage

| Requirement / risk | Automated evidence | Rendered check / gap |
| --- | --- | --- |
| Genuine target, not package/runtime | `GetTargetFrameworkFromAssembly_UsesDeclaredMetadataRatherThanRuntimeOrDisplayName`; `IsCompatibleProtocolAsync_SeparatesDeclaredTargetFromExecutingRuntime`; packed regressions and final composed matrix | Actual final-package module-only net472 and net8/runtime12 outputs inspected. |
| Unknown target stays unknown | `GetTargetFrameworkFromAssembly_WithNullAssembly_DoesNotInventRuntimeTarget`; `GetTargetFrameworkFromAssembly_WithoutTargetMetadata_DoesNotInventRuntimeTarget`; `GetDisplayName_KeepsTargetAndLabelsOnlyMeaningfulRuntimeDifferences` | Final missing-attribute module/project outputs inspected separately; no target fabricated. |
| Compact matching and labelled differing runtime | `GetDisplayName_KeepsTargetAndLabelsOnlyMeaningfulRuntimeDifferences`; `AssemblyRunStarted_RuntimeIdentityPersistsThroughStartCompletionAndSummary`; final composed matrix | Actual normal 80x24 ANSI inspection by coordinator; raw final outputs inspected here. |
| Durable/progress identities agree | `ActualSimpleTerminalProgress_PreservesTargetAndSeparateRuntime`; `SlowTest_PreservesSeparateTargetAndRuntimeIdentity`; `AssemblyRunCompleted_ProcessFailureRecapKeepsRuntimeWithItsTarget` | Final real pass/failure/zero output and deterministic ANSI/non-ANSI rendering checked. |
| Constrained width preserves primary identity | `AnsiProgress_RuntimeSuffixRespectsWidthAndPreservesPrimaryIdentity` (before/at/after boundaries); `AnsiProgress_LongFilenameCannotDisplaceTargetAndArchitecture`; SDK 40/80/120 checks | Renderer evidence executed; actual constrained CLI viewport unavailable, explicitly retained as a gap. |
| Old peers/signatures remain usable | `HandshakeMessage_RoundTrips` with and without target; `HandshakeMessagePropertyNames_AreStable`; `Constructor_PreservesSeparateRuntimeAndExistingCallShape`; `AssemblyRunStarted_DefaultLiteralStillSelectsExistingAttemptOverload` | Actual released-MTP/new-SDK and new-MTP/old-SDK compatibility inspected separately. |
| Packed application metadata/platform/missing attribute | `DotnetTestPipe_TargetFrameworkReflectsApplicationMetadata`; `DotnetTestPipe_FrameworkTargetIsNotInstalledRuntimeOrPackageTarget`; `DotnetTestPipe_TargetFrameworkIncludesDeclaredPlatform`; all four final-package cases executed/passed | Final shipping layout/cache/deployed hash equality asserted. |
| Standalone stays separate | Existing fallback tests; packed net472 case asserts compact runtime banner, target identity, and no new `runtime:` suffix; executed/passed | Actual packed standalone stdout inspected in the regression. |

### Commands already executed

From the TestFx worktree, Debug:

```powershell
.\build.cmd -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -bl
.\build.cmd -msbuildEngine dotnet -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -bl
.\.dotnet\dotnet.exe msbuild src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj /t:UpdateXlf /v:minimal '-bl:artifacts\log\Debug\framework-metadata-localization.binlog'
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Debug --no-restore '-bl:artifacts\log\Debug\framework-unit-head.binlog' -v:quiet
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.TerminalReporterContract.UnitTests\Microsoft.Testing.Platform.TerminalReporterContract.UnitTests.csproj -c Debug '-bl:artifacts\log\Debug\framework-terminal-contract.binlog' -v:quiet
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests\Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests.csproj -c Debug '-bl:artifacts\log\Debug\framework-protocol-contract.binlog' -v:quiet
.\build.cmd -msbuildEngine dotnet -projects src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -pack /p:VersionSuffix=dev.framework7066 -bl
.\build.cmd -pack /p:VersionSuffix=dev.framework7066 -bl
.\build.cmd -msbuildEngine dotnet -projects src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -pack /p:VersionSuffix=dev.framework7066.final1 -bl
python .github\scripts\test_check_localization.py
python .github\scripts\check_localization.py
```

Each final command above exited zero. The initial bootstrap/native PATH and
multi-project quoting failures are retained in the session artifacts, not
counted as passed builds. On this host the successful build.cmd commands were
invoked inside `cmd /d /c` with process-local Windows PowerShell/System32 PATH
and executable-extension settings.

The focused main test selection was:

```text
FullyQualifiedName~TargetFrameworkParserTests|FullyQualifiedName~TestFrameworkDisplayTests|FullyQualifiedName~TerminalTestReporterTests|FullyQualifiedName~AnsiTerminalTests|FullyQualifiedName~SilenceDrivenHeartbeatRendererTests|FullyQualifiedName~DotnetTestConnectionTests
```

It ran directly against each built net10.0/net11.0 DLL with the repo-local
dotnet, and against the net462 executable, using `--filter`, `--report-trx`,
`--report-trx-filename head-<tfm>.trx`, and
`--results-directory artifacts\TestResults\Debug\framework-metadata-head`.
All three process exits were zero. Contract suites ran directly, unfiltered,
with TRX under `framework-metadata-contracts`.

The packed discovery and final green commands were:

```powershell
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-build --no-launch-profile --list-tests text --filter 'FullyQualifiedName~DotnetTestPipeTargetFrameworkTests' -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= '-bl:artifacts\log\Debug\framework-acceptance-discovery.binlog'
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-build --no-launch-profile --no-progress --filter 'FullyQualifiedName~DotnetTestPipeTargetFrameworkTests' -p:UsingDotNetTest=true '-p:ArtifactsTestResultsDir=<worktree>\artifacts\TestResults\Debug\framework-metadata-packed-green-2' '-bl:artifacts\log\Debug\framework-acceptance-green.binlog'
```

Both exited zero. The execution verifier checked the independently planned
four names and fresh TRX timestamps, not just console success. This SDK needed
the explicit `text` argument for discovery; the earlier omitted-format command
failed argument parsing and was not counted as discovery.

### Test-value evidence

The source state is implementation commit
`c2b618b17ef3ecbf46054cde3785476673ff9f63`, against selected parent
`c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`. The isolated comparison retained
every test/assertion/API and changed exactly two production lines:
declared assembly metadata was made absent, and a nonempty primary identity
discarded its secondary runtime. Its saved patch SHA-256 is
`F2583430DB001AD1D686C2E2EA23E2EEAD861B7D9D4BE8189CC26FB69CD5944D`.
This is a buildable behavior-removal counterfactual, not a pristine-base run.

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- | --- |
| Declared metadata / `FrameworkWithoutDisplayName`, `FrameworkNameIsAuthoritative`, `PlatformWithoutDisplayName`, and handshake data cases | Set metadata lookup result to null, retaining the new API | Executed: detected; 10 parser + 3 handshake assertions fail | Executed: passed | Expected `net472`/platform-qualified target versus null; expected target-present true versus false. |
| Runtime identity / `FrameworkRollForward`, `DifferentRuntime`, `RuntimeExactlyFits`, slow-test and process-recap cases | Retain runtime state/API but discard its suffix when a primary identity exists | Executed: detected; 13 renderer/formatter assertions fail | Executed: passed | Expected labelled secondary runtime versus primary-only output. |
| Packed field 21 / `DeclaredModernTarget`, framework and platform cases | Rebuild/repack metadata-omitting producer, same tests and fresh cache | Executed: detected; three target-present assertions fail, missing-metadata case passes | Executed: passed; all four cases pass | Each red child exits normally; failures are the intended outer expected-true/actual-false metadata assertions, not launch failures. |

The comparison first used session-artifact storage, where the generated
platform-qualified application's Windows process path exceeded the usable
launch length. That launch failure was excluded from behavioral proof. The
owned comparison was relocated to the shorter
`Q:\src\copilot-comparisons\testfx7066-ecb9`, rebuilt and repacked without
changing tests, and the same complete selection was rerun successfully.

| Same selection and configuration | Behavior removed | Restored committed behavior |
| --- | --- | --- |
| 426 net10.0 focused cases, Debug, pinned SDK | Exit 2; 426 executed, 400 passed, 26 assertion failures, zero skipped | Exit 0; 426 executed/passed, zero failed/skipped |
| Four packed cases on net12.0 outer host, Debug, generated consumers Release | Exit 2; four executed, one passed, three intended target assertions, zero skipped | Exit 0; planned=selected=executed=passed=4, zero failed/skipped |

Selected test identities and multiplicities were compared between each pair;
they were identical. Both variants built with zero warnings/errors. The
correct two production files were restored and `git diff --exit-code HEAD`
confirmed the entire comparison source matched the implementation before
the green rebuild/repack.

Comparison package version stayed
`3.0.0-dev.framework7066.final1`, but the feeds and per-run acceptance caches
were isolated. Red package SHA-256:
`731583084DDE38118948436A09C15BF003A65D025431279EF9D74BD7CE9E52FC`;
restored comparison package SHA-256:
`356C195B26381C33814F413EF0EC588E23DA3C25AB0A0458462215A73FB3813C`.
These comparison-root builds are not the delivered package identified above.
The tests checked the current physical nupkg/cache/deployed payload in each
variant. The restored packed green interval was 2026-11-10 15:50:50Z through
15:50:58Z.

The counterfactual source build used the repository script and the same
`VersionSuffix`; the focused runner used the exact selection above:

```powershell
.\build.cmd -msbuildEngine dotnet -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj /p:VersionSuffix=dev.framework7066.final1 -bl
.\.dotnet\dotnet.exe build test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug -p:VersionSuffix=dev.framework7066.final1 '-bl:artifacts\log\Debug\counterfactual-short-acceptance-build.binlog' -v:quiet
.\build.cmd -msbuildEngine dotnet -projects src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -pack /p:VersionSuffix=dev.framework7066.final1 -bl
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-build --no-launch-profile --no-progress --filter 'FullyQualifiedName~DotnetTestPipeTargetFrameworkTests' -p:UsingDotNetTest=true -p:VersionSuffix=dev.framework7066.final1 '-p:ArtifactsTestResultsDir=<comparison>\artifacts\TestResults\Debug\<variant>' '-bl:artifacts\log\Debug\<variant>.binlog'
```

Both executions used those same arguments apart from separate result/binlog
destinations. Red is intentionally nonzero and is not passed through the
green-only execution gate; restored green passed the independent four-name
gate. The unit runner was the built net10.0 DLL with the exact filter above,
`--report-trx`, and separate red/green report paths.

Retained session artifacts include the two-line patch, console logs,
`sensitivity-evidence/{red,green}-{unit,packed}.trx`, and compilation/pack
binlogs. They are local evidence, not uploaded/public artifacts. Concise
results are preserved here so the decision does not depend on access to a
local absolute path.

Both affected unit assemblies retain MSTest method-level parallelization,
Workers=0 (32 effective workers in these runs). New parser/formatter cases use
per-test data and dynamic assemblies; terminal/clock/connection state is
per-test fake state. Packed cases create distinct test assets and use the
existing acceptance assembly's isolated restore cache. No new process-global
mutation, shared asset writer, scheduling exclusion, or lock is introduced.

### CI reachability

The Windows pipeline's `SkipTests` parameter gates
`eng\pipelines\steps\build-test-windows.yml`. Its ordinary Release path invokes
`dotnet test --solution TestFx.slnx` through
`test-windows-configuration-tests.yml`; Debug intentionally selects only
unit-test modules. The new acceptance class is part of the existing MTP
acceptance project, has no extra category/environment opt-in, and creates a
unique mutable asset per case. Its two .NET Framework/platform cases retain
the Windows OS condition. Existing affected-test/full-suite selection remains
unchanged. No CI run was published or observed for these local commits, so
CI execution is unproven rather than inferred from local results.

### Delivery and owned cleanup

The final source snapshot/26-file blob manifest and final1 package were
transported to the SDK owner before the evidence-only follow-up. Both
counterfactual checkouts and their SDK junction were removed after preserving
paired TRX, source patch, logs, and binlogs; the repository-pinned SDK was
preserved. Generated local consumer assets and immutable transport artifacts
remain available for provenance/reproduction. A single SDK-install temporary
file in the Windows system temp directory could not be deleted because access
was denied; no ACL/elevation workaround or unrelated cleanup was attempted.
No application/test process was intentionally left running.

## Open questions and delivery limits

- Actual constrained-width full CLI/PTTY rendering remains unavailable with
  the current canvas capabilities; normal 80x24 execution and deterministic
  40/80/120 renderer checks are explicitly separate.
- NativeAOT execution is not proven by a managed SDK build or unit tests;
  SDK native-linker prerequisites remain a reported environment limitation.
- CI execution is unproven; all evidence above is local or retained
  coordinated execution tied to the stated source/package identities.
- Scope is main-derived, local implementation/delivery only. No publication,
  dependency rollout, servicing validation, or backport is implied.
