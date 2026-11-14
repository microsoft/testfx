# Legacy CLI and HangDump filename migrations

## Problem and contract

Tracking Task: [#12314](https://github.com/microsoft/testfx/issues/12314).
Reviewed source: `471a807cf8182e0bcd0d9967c8dbf002c04945d2`.
Implementation base: `c46cfa7394e7be10c4057d3ba8d1d3b698fe242e` (MTP 3 / MSTest 5).

Retire the authorized MTP spellings only after their producers can migrate:
`--no-progress` to `--progress off`, `--no-ansi` to `--ansi off`, and HangDump
`%p` to `{pid}`. This does not retire other old-extension contracts, supported
.NET Framework targets, or the .NET runtime's CrashDump `%p` / `%e` tokens.
No other repository or mirrored `eng/common` file is in scope.

The MTP application owns its command-line validation and terminal initialization.
Native `dotnet test` has a separate parser and terminal renderer; SDK-owned
switches must not be confused with options forwarded to the MTP application.
Removing the MTP ANSI alias intentionally makes it an unknown application option
(exit 5). Canonical ANSI values, environment detection, and `NO_COLOR` remain.

HangDump continues to resolve all six case-sensitive `{placeholder}` templates,
preserve a configured root filename, and give process-tree children distinct
names. `%p` no longer receives HangDump substitution or counts as a PID
placeholder for child uniqueness. Results-directory containment, relative
subdirectories, paths with spaces, dump types, and process cleanup are unchanged.
Default and generated child patterns use `{pid}` without changing the resulting
default filename. CrashDump's runtime-owned percent tokens are unaffected.

## Research and experiments

### 2026-11-10: producer boundaries

- `global.json` pins `Microsoft.DotNet.Arcade.Sdk` **12.0.0-beta.26556.2**;
  `eng/Version.Details.xml` records source
  `bd945b47c51da25b17dde4e8e0f8066ad001d05f`.
  The [pinned MTP test target](https://github.com/dotnet/arcade/blob/bd945b47c51da25b17dde4e8e0f8066ad001d05f/src/Microsoft.DotNet.Arcade.Sdk/tools/Microsoft.Testing.Platform/Microsoft.Testing.Platform.targets)
  unconditionally appends `--no-progress` to `_TestRunnerArgs` in `RunTests`.
  Supplying `TestRunnerAdditionalArguments` cannot remove that later literal.
  Arcade main at `ac48b193ff381faeb73f097d14ca55a95b284e18` still has it.
- The pinned [runner selection and repository hooks](https://github.com/dotnet/arcade/blob/bd945b47c51da25b17dde4e8e0f8066ad001d05f/src/Microsoft.DotNet.Arcade.Sdk/tools/Tests.targets#L23-L28)
  support replacing the **whole** runner with
  `eng/Microsoft.Testing.Platform/Microsoft.Testing.Platform.targets`;
  lines 172-173 also import repository-specific `eng/Tests.targets`.
  Neither is a flag-omission property in the stock MTP runner. Its lines 23-30
  combine `RunArguments` and `TestRunnerAdditionalArguments`, then append the
  negative flag unconditionally. Keeping the stock orchestration requires an
  upstream change; a repository-owned runner fork is a separate alternative,
  not an unavailable extension point.
- The repository-pinned **12.0.100-alpha.1.26556.115** SDK reports VMR source
  `6e6f978fd9ca19b62796a13ff5230b21af0c42fa` in its installed `.version`.
  Its [native command definition](https://github.com/dotnet/dotnet/blob/6e6f978fd9ca19b62796a13ff5230b21af0c42fa/src/sdk/src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Test/TestCommandDefinition.MicrosoftTestingPlatform.cs)
  registers SDK-owned `--no-ansi` and `--no-progress`, not `--ansi`.
  Its [terminal initialization](https://github.com/dotnet/dotnet/blob/6e6f978fd9ca19b62796a13ff5230b21af0c42fa/src/sdk/src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs)
  reads only the SDK-owned `NoAnsiOption` for ANSI suppression, but
  `GetSharedProgressEnabled` / `GetProgressEnabled` observe forwarded
  `--progress off`. Its [argument construction](https://github.com/dotnet/dotnet/blob/6e6f978fd9ca19b62796a13ff5230b21af0c42fa/src/sdk/src/Cli/dotnet/Commands/Test/MTP/TestCommandOptions.cs)
  forwards unmatched application arguments, not the recognized SDK ANSI switch.
  The installed SDK's native help also lists its two negative switches.
- Consequently, the module pool can use `--progress off` but must retain the
  **SDK-owned** `--no-ansi` until an SDK implements the positive ANSI option in
  its own renderer. Changing that token to `--ansi off` here would leave the
  native renderer uncontrolled, even though the child accepts the option.
- The MTP terminal source manifest explicitly excludes its options provider:
  the SDK owns a different parser. In-repository direct-host producers can
  migrate to `--ansi off` before the MTP registration is removed.

### 2026-11-10: localization regeneration

The first owning-project `UpdateXlf` run preserved nonempty translations of the
old progress description. The localization guard correctly rejected seven
targets missing the new locked `--ansi off` token. Reusing the ANSI description
key would also leave translations advertising the removed precedence rule.
Use fresh neutral `TerminalAnsiModeOptionDescription` and
`TerminalProgressModeOptionDescription` resource IDs and
`HangDumpFileNameTemplateOptionDescription` for the description dropping `%p`,
then regenerate with the owning target. This requests new translations without
hand-editing XLF or publishing stale descriptions of the removed spellings.

### 2026-11-10: isolated package setup

The first pristine-production comparison pack compiled successfully but failed
pack metadata validation with 33 `PackageProjectUrl must be specified` errors
(exit 1). The owned local clone's `origin` was its filesystem source, not a
GitHub URL. Correct only that clone's origin to
`https://github.com/microsoft/testfx.git`; do not change product source or weaken
metadata validation. This is setup evidence, not a behavioral red phase.
The host's other TestFx sessions requested serialized heavy validation; further
builds and tests wait for the coordinator's validation slot.

### 2026-11-10: the native writer is another filename consumer

Static inspection caught a necessary second boundary before behavioral
validation: deleting HangDump's own replacement alone does **not** make `%p`
literal. The runtime's diagnostic IPC
[`GenerateDump`](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/vm/excep.cpp)
passes the filename unchanged to `createdump` on both Windows and Unix.
Its [`FormatDumpName`](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/debug/createdump/dumpname.cpp)
still expands `%p`; `%%` is the documented literal-percent escape.

Escape percent characters only in HangDump's native **write argument** using the
existing `GetDumpFileNames` seam. Keep the resolved artifact path unescaped and
retain the Windows-space quoting workaround. This prevents a second formatter
from changing a resolved filename or results-directory segment. HangDump's
supported template language remains the six curly-brace tokens; CrashDump's
separate runtime-token path is unchanged. Add two focused writer-argument cases
covering literal `%p` and combined percent-containing path segments/spaces, and
retain the real literal-percent dump regression. This is a static discovery,
not an invented failing test run.

## Alternatives and decision

1. Remove all three spellings now: rejected because the pinned Arcade runner
   would send an unregistered option to every MTP application.
2. Rewrite mirrored infrastructure or change external SDK/Arcade sources:
   rejected as out of scope. Use Arcade's supported whole-runner hook instead:
   considered but not selected, because it would fork the complete runner's
   architecture selection, output capture, timeout, report, and exit handling
   solely to change one literal. A launcher/argument-stripping shim would add
   compatibility code rather than migrate the stock producer.
3. Keep all spellings: safe but unnecessarily defers the independent MTP ANSI
   and HangDump migrations.
4. **Chosen:** migrate owned producers, remove the MTP ANSI alias and HangDump
   percent substitution, and explicitly defer the MTP progress alias.

Keep the shipped internal API baseline intact and record the intentional ANSI
constant removal in its unshipped baseline. No new shipping public API is needed.
Existing old-extension compatibility cases remain unless directly tied to one
of these named removals.

## Validation and demonstration

### Revisions, toolchain and package provenance

Executed on Windows x64 on 2026-11-10, using the repository-pinned
`12.0.100-alpha.1.26556.115` SDK and Release configuration:

- Product packages: `52f5c8e709cc0285806dc08f6133ce17d3e9c52d`.
- Final test assertions: `9f7bbc5a4b571fabad818977371e17459fdb90f4`.
  This follow-up changes only assertion style, not production or packaging.
- Pristine production comparison:
  `c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`, with the same tests and harness
  retained. The test/harness patch SHA-256 is
  `D9FE3524AB3D0EC91C83F583C869A3FC39EED5A273920E3C66CF576F18FC2B73`.
  Production `src`, build configuration, versions, and the module-pool producer
  have no tracked changes in that comparison.
- Full implementation/test diff, excluding documentation: SHA-256
  `CC2AE2C6B6C91200E3298583C8232551489D8E3FDC962F77A75B5821D9C48237`.
  These patch hashes are not commit IDs.

Both variants were built and packed separately with real default Windows/UAP
assets. The NuGet content/metadata guard passed in both variants; no guard or
mirrored file was changed:

```powershell
# Run from each variant's root; $root is that exact checkout.
$root = (Get-Location).Path
.\build.cmd -pack -c Release '-nodeReuse:$false' /m:2 `
    /p:UseSharedCompilation=false /p:EnableCodeCoverage=false `
    "/p:Projects=$root\src\*\*\*.csproj" `
    /bl:artifacts\log\Release\legacy-cli-green-pack-3.binlog
# Baseline uses the same command with legacy-cli-red-pack-2.binlog.
```

Green: exit 0, zero warnings/errors, 3m56s. Baseline: exit 0, zero
warnings/errors, 10m55s. The glob was checked against Git's tracked source
projects: all 41, with no omission. The changed test owners, shared
`Microsoft.Testing.TestInfrastructure`, and performance runner then built
successfully with the pinned SDK using:

```powershell
.\.dotnet\dotnet.exe build $project -c Release /m:1 /nr:false `
    /p:BuildProjectReferences=false /p:UseSharedCompilation=false `
    /p:EnableCodeCoverage=false /p:TreatWarningsAsErrors=true `
    /p:MSBuildTreatWarningsAsErrors=true "-bl:$uniqueBinlog"
```

The final provider-test rebuild and all baseline owner builds have zero
warnings/errors. The initial green owner build exposed nine repeated
IDE0022/MSTEST0037 warnings; the test-only follow-up above corrected them.

Same package versions were deliberately used in isolated per-run caches.
The existing marked-cache fixture opt-in retained those task-owned caches for
inspection; neither the global cache nor another session's outputs was cleared.
`project.assets.json` identified the owned primary cache and local shipping
feed; Visual Studio also configured a read-only fallback folder. Cached package
archives and executed-directory DLLs matched the correct packed variant.
Core and HangDump runtime payloads matched on net10.0, net11.0, and the
netstandard2.0 asset consumed by net462/net472. Nuspec repository commits
matched the product revisions above.

| Package | Green SHA-256 | Pristine-production SHA-256 |
| --- | --- | --- |
| `Microsoft.Testing.Platform.3.0.0-dev.nupkg` | `7A59FB3274135AE6C673A990A078BA6E4B72378D48B3F31B66FD032E0D105B3C` | `AE9EC51A8D9ED4B1AB21D0ADBD4E920A8414C71D40E492D081C8CB833BF283F7` |
| `Microsoft.Testing.Extensions.HangDump.3.0.0-dev.nupkg` | `5E090A7EFC1EC815F170B5151172070D1FE9669280D04C010E9F30C03DC3AE51` | `414783E3D65BA01507E1227DB4107351FC1C50447944C52FC82FC5C3080BC787` |

Two setup attempts are not behavioral red evidence: the PowerShell `-projects`
glob wrapper expanded to an unescaped semicolon list and failed MSB1006, so the
supported `Projects` property above was used directly; a dotnet-engine pack
with `SkipUapTargetFramework=true` omitted required Windows assets and failed
three MSTest package-count checks. Default-UAP packaging fixed the actual
layout. No expectation was reduced.

### Executed requirements and commands

Every test group had separate JSON discovery followed by actual execution.
Discovery exited 0; fresh TRX and JUnit reports were parsed. The green execution
gate reconciled exact discovered display names, counts, unique execution IDs,
timestamps, and outer exit 0. No selected test was skipped.

The exact shared execution arguments were:

```powershell
# $module is artifacts\bin\<project>\Release\<tfm>\<project>.dll.
# For net462/net472, run the corresponding .exe directly instead.
.\.dotnet\dotnet.exe $module --filter $filter --ansi off --progress off `
    --no-banner --report-trx --report-trx-filename results.trx `
    --report-junit --report-junit-filename results.xml `
    --results-directory $uniqueResults --show-test-results all
# Independent discovery used the same module/filter/output preferences,
# replacing report arguments with: --list-tests json
```

| Requirement | Exact cases / selector | Executed result |
| --- | --- | --- |
| Default/child uniqueness, literal percent paths and Windows quoting | `HangDumpTests.EnsureProcessIdPlaceholder_MakesCustomDumpPathUnique`, `GetDumpFileNamePattern_PreservesRootNameAndMakesChildNamesUnique`, `GetDumpFileNames_*` | 15 each on net10.0/net11.0/net462/net472: 60 passed. |
| Removed ANSI registration; canonical option validation; retained progress warning/precedence; six naming templates | `TerminalTestReporterCommandLineOptionsProviderTests`, `TerminalOutputDeviceTests.InitializeAsync_NoProgress*`, `InitializeAsync_ProgressOff*`, `InitializeAsync_ProgressOption_TakesPrecedenceOverLegacyNoProgress*`, `ArtifactNamingHelperTests` | 146 each on net10.0/net11.0/net462: 438 passed. |
| Real host rejection, ANSI on/off aliases, auto/CI and `NO_COLOR` | `AnsiOptionTests` | 15 passed. |
| Actual literal-percent native dump | `HangDumpTests.HangDump_LegacyPercentPlaceholder_IsLiteralFileName` | 1 passed; the actual filename is `literal_%p.dmp`. |
| Default/custom filenames, all templates, spaces/subdirectories and containment | `HangDumpTests.HangDump_DefaultSetting_CreateDump`, `HangDump_CustomFileName_CreateDump`, `HangDump_PathWithSpaces_CreateDump`, `HangDump_TemplateFileName_CreateDump`, `HangDump_TemplateFileNameWithSubdirectory_CreateDump`, `HangDump_TemplateWithPathTraversal_RejectsAndFails` | 12 passed, including net10.0/net11.0/net462 where parameterized. |
| MTP help/info and unknown-option command echo | `HelpInfoTests.Help_WhenNoExtensionRegistered_OutputDefaultHelpContent`, `Info_WhenNoExtensionRegistered_OutputDefaultInfoContent`, `Help_WhenNoExtensionRegisteredAndUnknownOptionIsSpecified_OutputDefaultHelpContentAndUnknownOption` | 9 passed on the three consumer TFMs. |
| All-extension help/info, including HangDump description | `HelpInfoAllExtensionsTests.Help_WithAllExtensionsRegistered_OutputFullHelpContent`, `Info_WithAllExtensionsRegistered_OutputFullInfoContent` | 6 passed on the three consumer TFMs. |
| Direct process-exit producer and joined teardown | `ExitOnProcessExitTests.ExitOnProcessExit_Succeed` | 3 passed. |
| MSTest help | `HelpInfoTests.Help_WhenMSTestExtensionRegistered_OutputHelpContentOfRegisteredExtension` in MSTest acceptance | 3 passed. |
| Direct `dotnet run` producer with coverage settings and working-directory preservation | `SdkTests.DotnetRun_WithCoverageSettings_PreservesInvocationWorkingDirectory` | Both absolute/relative cases passed. |
| Shared helper still supports current core with deliberate old extensions | `ForwardCompatibilityTests.NewerPlatform_WithPreviousExtensions_ShouldExecuteTests` | 1 passed with extensions 2.2.1 / MSBuild extension 2.3.0. |

Each listed selector uses `FullyQualifiedName~<class.method>`; alternatives are
joined with `|`. Acceptance outer hosts run on net12.0; their generated consumers
use the TFMs listed above. There are 550 distinct unit/acceptance case executions
in these groups, plus two exact-selector green repeats after the red comparison:
**552 passed, zero failed/skipped**. The repeats are not additional coverage.
Per-run `run.json`, discovery, TRX, JUnit and command logs remain under each
variant's `artifacts\TestResults\Release\legacy-cli`.

The real producer's self-tests ran separately:

```powershell
pwsh -NoProfile -File eng\validate-collection-module-pool.ps1
```

Green: exit 0, **34 passed**, including native failure, peer cancellation,
orphan cleanup and output/cleanup-failure cases. The pristine producer with
the same validator retained launched its workers, passed 24 preceding checks,
then failed the intended argv assertion requiring separate `--progress` /
`off` tokens and preserving SDK-owned `--no-ansi` (exit 1).

### Real native SDK consumer and rendered surface

The newly packed `AnsiOptionTest` net11.0 module was also launched by the
**real pinned SDK**, not only by `TestHost`:

```powershell
.\.dotnet\dotnet.exe test --test-modules AnsiOptionTest.dll `
    --root-directory $consumerBin --no-build --progress off --no-ansi `
    --show-test-results all --no-banner
```

Exit 0; exactly **Test1 / Passed**, total 1, failed 0, succeeded 1, skipped 0;
stdout contained no ANSI escapes. The resolved module's core DLL SHA-256 was
`D5D72B8CA426F64655B79296FA8E3B3C61ACCF0F6898981263A9118C55136A22`,
matching the current pack's `lib/net11.0` payload. This proves the exercised
SDK consumes its own `--no-ansi` rather than forwarding the removed MTP option.
It does not establish a version floor for older SDKs.

An actual terminal canvas at **80 columns x 24 rows** displayed the packed
host's help, canonical ANSI-off/on Test1 executions (both exit 0), and removed
option diagnostic (exit 5). The option list excludes `--no-ansi`, keeps
`--no-progress`, and exposes `--ansi off`; normal summaries remained readable.
A 120-column console resize attempt did not resize the canvas's 80-column
renderer and produced mismatched reflow, so wider/constrained-grid validation
is **unverified**, not a claimed layout pass. The original size was restored.
Readable command/output excerpts are more useful here than fabricated media.

### Test-value evidence

The same retained tests and selectors ran on independently rebuilt/repacked
production states. The two exact packed selectors were re-run green after red.

| Contract / case | Without removal: pristine production | Correct implementation | Distinguishing assertion |
| --- | --- | --- | --- |
| ANSI registration/validation / provider cases | Executed: 2 intended failures per TFM; other 144 cases pass. | Executed: all 146 pass per TFM. | `no-ansi` still appears in registration; its validator returns normally instead of rejecting an unregistered option. |
| HangDump naming/native write argument / 15 focused cases | Executed: 10 intended failures and 5 preservation passes per TFM. | Executed: all 15 pass per TFM. | Old defaults/child names use `%p`; the native argument is not percent-escaped. |
| `NoAnsiOption_IsRejectedAsUnknownOption` | Executed: outer exit 2, one assertion failure; old child exits **0**, expected **5**. | Executed: outer exit 0, one pass; child exits 5. | The removed application option must be unknown, not silently accepted. |
| `HangDump_LegacyPercentPlaceholder_IsLiteralFileName` | Executed: outer exit 2, one assertion failure; actual **`literal_39992.dmp`**, expected **`literal_%p.dmp`**. | Executed: outer exit 0, one pass; literal file exists. | Exact actual native dump filename, not output-text presence. |
| Module-pool argv | Executed: worker argv assertion detects the old producer; outer validator exit 1. | Executed: 34 checks pass, exit 0. | Token boundaries and independent SDK ANSI ownership. |

The paired unit/packed selection executes **500 cases per production state**:
baseline **452 passed / 48 intended failures / 0 skipped**, corresponding green
selection **500 passed / 0 failed / 0 skipped**. All intended failures were
attributed to their behavioral assertions; restore/compile/launch failures,
zero tests, skips and green CI are not counted as red proof.

Additional guards: localization self-tests 41 passed; final localization guard
checked all 364 catalogs and exited 0; PowerShell AST checks included the
embedded worker; all 19 changed C# files retain UTF-8 BOMs.

### PR path-feedback preservation

The three related path comments were evaluated together. Decline the proposed
rooted-TFM guard: `TargetFrameworks.NetCurrent` comes from a repository-owned
relative TFM and the other appended component is an `N`-formatted GUID.
Partially accept the two literal-suffix suggestions for clarity: use
`Path.Join` for the expected `literal_%p.dmp` filename and `HangDump` directory.
Those literals were already relative; exact full-path/directory equality and
the anchored PID filename assertion remain unchanged.

The test-only patch against `36a0265bcf69ae7eb501775c014b9681bc8658f4` is SHA-256
`D61477711C4AAE5B34DD6AE8D02175373467CA57EC9976C5202F89177D704714`.
The pinned Release acceptance-owner build, using the owner-build command above
and `path-review-acceptance-build-1.binlog`, passed with zero warnings/errors.
The identical selectors `HangDumpTests.HangDump_LegacyPercentPlaceholder_IsLiteralFileName`
and `HangDumpTests.HangDump_TemplateFileNameWithSubdirectory_CreateDump` passed
**four cases before and four after**, with zero failures/skips (subdirectory
consumers: net10.0/net11.0/net462). Discovery identities, fresh TRX/JUnit, and
the unchanged packed core/HangDump hashes were verified. This is green/green
preservation evidence, not a new product behavior or a claimed CodeQL rerun.

Remaining evidence gaps: Linux/macOS runtime execution, older SDK native
renderer behavior, a second correctly resized terminal grid, and CI for the
published head. These do not imply those checks passed. No source-generation
runtime variant is credited merely because its fixture compiled.

### 2026-11-11: final progress-alias follow-up

The final follow-up starts from TestFx
`ecee016ececdffa3d8b8fbdc835055e3cc6cbe21`. The three earlier implementation
PRs (#12316, #12317, and #12319) are merged. This branch proposes removing the
remaining MTP `--no-progress` registration, precedence/warning state, resources,
and compatibility expectations. The supported application spelling remains
`--progress off`; the native SDK's separately parsed switches are unchanged.

The dependency is still a merge gate, not a completed migration.
`global.json` now pins Arcade **12.0.0-beta.26560.9**, whose source
`305558e284ce35e7b35ac9ebd75802915394fd3e` still appends `--no-progress`.
[dotnet/arcade#17784](https://github.com/dotnet/arcade/pull/17784), open at
`e7c0fe16e0266cfa6847d466eeb88ee9cb5e6d7a` when inspected, changes that producer
to `--progress off`. Do not merge this removal until a published Arcade SDK
containing that change has flowed into TestFx. No mirrored runner fork,
argument-stripping workaround, or claim that the current stock runner works
without the alias is part of this follow-up.

Validation uses the existing terminal option/unit and packed acceptance
fixtures: retain canonical progress/default behavior, reject the retired option
both alone and alongside `--progress off`, and reconcile MTP/MSTest help/info.
Use the same rejection assertions against preserved baseline packages and newly
packed removal packages to establish sensitivity. Executed results follow below.
The follow-up PR will be draft while the dependency is unavailable and will use
a closing reference to #12314 so that the issue closes on merge, not before.

The progress help text uses a fresh
`TerminalProgressReportingOptionDescription` resource ID. Reusing the previous
key could retain translations that still advertise precedence over the removed
alias. Only the neutral resources and hand-maintained accessor were edited;
the owning platform's `UpdateXlf` target regenerated the catalogs. The shipped
internal API baseline is preserved, with an explicit unshipped `*REMOVED*`
entry for `NoProgressOption`.

| Requirement / consumer | Focused automated evidence | Boundary |
| --- | --- | --- |
| The retired application option is not registered. | `TerminalTestReporterCommandLineOptionsProviderTests.GetCommandLineOptions_DoesNotIncludeNoProgressOption` | Source-linked provider on net10/net11/net462. |
| A real packaged host rejects the retired spelling. | `AnsiOptionTests.NoProgressOption_IsRejectedAsUnknownOption` | Exit 5 and the exact unknown-option diagnostic. The existing helper also supplies `--progress off`, covering the former precedence combination. |
| Canonical progress values and defaults remain unchanged. | `ValidateOptionArguments_ProgressOption_AcceptsValidValues`, `ValidateOptionArguments_ProgressOption_RejectsInvalidValues`, `IsProgressEnabled_ProgressOption_ReturnsRequestedState`, and `IsProgressEnabled_NoProgressOptions_ReturnsTrue`; existing `AnsiOptionTests` execute packaged passing consumers with the helper's `--progress off`. | Invalid canonical values still fail validation; default/on/off behavior and ANSI independence are retained. |
| MTP and MSTest advertise only supported application options. | `HelpInfoTests.Help_WhenNoExtensionRegistered_OutputDefaultHelpContent`, `Info_WhenNoExtensionRegistered_OutputDefaultInfoContent`, `HelpInfoAllExtensionsTests.Help_WithAllExtensionsRegistered_OutputFullHelpContent`, `Info_WithAllExtensionsRegistered_OutputFullInfoContent`, and MSTest's `Help_WhenMSTestExtensionRegistered_OutputHelpContentOfRegisteredExtension` / `Info_WhenMSTestExtensionRegistered_OutputInfoContentOfRegisteredExtension`. | Packed net10/net11/net462 consumers; native SDK-owned options are not removed. |

Both affected test assemblies retain method-level parallelization with CPU-count
workers. The provider tests use per-test option dictionaries; the packed ANSI
fixture is built once, then executed read-only in separate child processes.
Deleting warning-only tests also deletes their reflection-based static reset and
console-capture helper. The unit class's existing console resource lock remains
for its other console users; no scheduling or lock declaration is weakened.

#### Executed follow-up evidence

The tested source is base `ecee016ececdffa3d8b8fbdc835055e3cc6cbe21` plus the
production/test patch with SHA-256
`C45C69018E08E3CD2F7C2FAA1C4F9FA992179BDEDA67C3876DBA50E80ACF987D`.
The patch includes neutral/generated resources and the API removal record.
Its hash was checked again after all runs; subsequent edits are documentation
only. This identifies the actual dirty build state, not a fictional committed
or CI-verified package.

The pinned SDK was **12.0.100-alpha.1.26556.115**. Shipping projects compiled
their normal target frameworks, including legacy assets and default UAP
packaging; the outer acceptance hosts target net12.0, while their generated
consumers exercise net11.0, net10.0, and net462.

The source-only solution filter contains all 41 `src/**/*.csproj` projects from
`TestFx.slnx`. The test-owner filter contains only platform units and MTP
acceptance; the MSTest acceptance owner had already compiled successfully in
the initial strict selection. Both filters are task-owned artifacts, not
repository build changes.

```powershell
# Baseline product, before the production edits: exit 0, no warnings/errors.
& .\eng\build.ps1 -restore -build -pack -c Release -projects .\src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -binaryLog -nodeReuse:$false /m:2 /p:UseSharedCompilation=false /p:EnableCodeCoverage=false '-bl:artifacts\log\Release\no-progress-baseline-pack-{}.binlog'

# $evidence is the task-owned artifact directory containing the generated filters.
# Correct product: exit 0, no warnings/errors; 32 shipping packages and pack guards passed.
& .\eng\build.ps1 -restore -build -pack -c Release -projects "$evidence\shipping.slnf" -binaryLog -nodeReuse:$false /m:4 /p:UseSharedCompilation=false /p:EnableCodeCoverage=false '/bl:artifacts\log\Release\no-progress-green-shipping-pack-{}.binlog'

# Test owners only: exit 0, 22 existing MSTEST0037 warnings, no errors.
& .\eng\build.ps1 -build -c Release -projects "$evidence\test-owners.slnf" -binaryLog -nodeReuse:$false /m:4 /p:UseSharedCompilation=false /p:EnableCodeCoverage=false /p:BuildProjectReferences=false /p:WarningsNotAsErrors=MSTEST0037 /warnnotaserror:MSTEST0037 '/bl:artifacts\log\Release\no-progress-test-owner-build-final-{}.binlog'
```

The strict source-plus-owner selection failed with **22 MSTEST0037 errors** in
untouched assertions. These are not fixed or suppressed in this PR. Only the
local test-owner invocation demotes that rule; shipping compilation/pack remains
strict. An initial local `NoWarn=MSTEST0037` attempt was rejected because global
`NoWarn` replaced the projects' normal exclusions, re-enabling MSTEST0084 and
SA0001. The final command preserves those exclusions. Other discarded setup
attempts were a canceled over-broad solution build and a semicolon-separated
project argument rejected with MSB1006; neither is test or regression evidence.

All focused host runs used the following arguments, with a unique result
directory per run, and separate `--list-tests json` discovery using the same
filter. TRX was checked against that discovery plan with `verify_execution.py`;
JUnit and CTRF case counts and failure/skip outcomes were reconciled too.

```powershell
$unitFilter = 'FullyQualifiedName~TerminalTestReporterCommandLineOptionsProviderTests|FullyQualifiedName~TerminalOutputDeviceTests|FullyQualifiedName~LegacyExtensionContractTests'
$mtpFilter = 'FullyQualifiedName~AnsiOptionTests|FullyQualifiedName~HelpInfoTests|FullyQualifiedName~HelpInfoAllExtensionsTests'
$mstestFilter = 'FullyQualifiedName~HelpInfoTests'
$rejectionFilter = 'FullyQualifiedName~AnsiOptionTests.NoProgressOption_IsRejectedAsUnknownOption'

# $hostExecutable/$prefix select the already-built module below; no MSBuild runs here.
& $hostExecutable @prefix --filter $filter --ansi off --progress off --show-test-results failed --results-directory $results --report-trx --report-trx-filename execution.trx --report-junit --report-junit-filename execution.junit.xml --report-ctrf --report-ctrf-filename execution.ctrf.json
```

| Module / selection | Selected / executed / passed / failed / skipped | Host exit |
| --- | --- | --- |
| Platform units, net10.0, `$unitFilter` | 180 / 180 / 180 / 0 / 0 | 0 |
| Platform units, net11.0, `$unitFilter` | 180 / 180 / 180 / 0 / 0 | 0 |
| Platform units, net462, `$unitFilter` | 181 / 181 / 181 / 0 / 0 | 0 |
| MTP acceptance, net12.0, `$mtpFilter` | 47 / 47 / 47 / 0 / 0 | 0 |
| MSTest acceptance, net12.0, `$mstestFilter` | 6 / 6 / 6 / 0 / 0 | 0 |
| Same MTP acceptance harness, baseline inner package, `$rejectionFilter` | 1 / 1 / 0 / 1 / 0 | 2, intended assertion failure |
| Same MTP acceptance harness, restored removal package, `$rejectionFilter` | 1 / 1 / 1 / 0 / 0 | 0 |

Modern modules were launched with the repository's `.dotnet\dotnet.exe` and
`artifacts\bin\<module>\Release\<tfm>\<module>.dll`; net462 units use their
`.exe` directly. The first net462 invocation incorrectly looked for a DLL and
never executed; correcting the launcher produced the 181-case run above.

**Test-value evidence:** the unchanged acceptance assembly SHA-256 was
`B2375E07D02623897B33E88B60AEF4D9EFDB474E62ED5C911E3AC3D73D250511`
in both package states. The red assertion reported **expected exit 5, actual
exit 0**, with the baseline child actually executing and passing `Test1`.
The same selection then passed with the removal package. Compilation, setup,
or zero-test failures are not credited as sensitivity proof.

| Product payload | Baseline | Removal |
| --- | --- | --- |
| Core `3.0.0-dev.nupkg` SHA-256 | `E687CB9DC8E0A676358EFC48ADCC9174134CBE9891CF237AD1545AA6C15CF224` | `B0A6A2A8C22CC95D2F1C0CF7CDA10B9628D2B555E26699F7B7BEFF84ECE91AA9` |
| Executed net11 core DLL SHA-256 | `ED0E7904EA71D213F388025CC3D56B73262644D8CC1F23E4A16A769BADA63CBE` | `A795A7460079B508452D128821EB3C9FF43356EFF7C9393ACE2535B80AA328CD` |

Each package state used a fresh, marked task-owned cache through
`TESTFX_COLLECTION_NUGET_CACHE`. `project.assets.json`, cache archives, and
physical consumer DLLs were inspected. All 20 initial green consumer core
copies, both red copies, and both replay copies matched their own packed
payloads. The shipping feed was restored to the removal package in `finally`;
no shared package cache was cleared. A standalone host trace also resolved the
executed core from the verified green asset directory.

The real pinned native SDK separately executed the source-generation output of
the packed MSTest help fixture:

```powershell
& .\.dotnet\dotnet.exe test --test-modules HelpInfo.dll --root-directory "$asset\HelpInfo\bin\SourceGen\Release\net11.0" --no-ansi --no-progress --show-test-results all
```

**Exit 0: exactly `Test` passed, total 1, failed/skipped 0, no ANSI escapes.**
This is module mode without MSBuild. Its SDK-owned negative switches were not
forwarded to the application. An initial attempt to add TRX options failed
because this deliberately minimal help fixture does not register that reporter;
the successful preservation run omits those unsupported options rather than
adding dependencies. The regular acceptance runs above still exercise all
three configured report formats. Directly launching the green ANSI fixture
with only `--no-progress` returned **5** and the unknown-option diagnostic.

The terminal canvas rendered the actual green packed host's
`--help --ansi off` at **80 columns x 24 rows**; two settled reads confirmed
readable option indentation, canonical progress help without the removed row
or precedence sentence, and help exit 0. A text transcript is clearer than an
image for this option removal. No second resized grid or non-Windows runtime
execution is claimed. Localization passed 41 guard self-tests and all 364
catalogs; all 11 changed C# files retained their UTF-8 BOMs.

Logs, discovery plans, TRX/JUnit/CTRF, package/consumer hash manifests, and
counterfactual diagnostics are retained in task-owned artifact storage. The
counts, decisive assertion, commands, and package hashes above preserve the
reviewable evidence without relying solely on local absolute paths. Published
head CI and a stock Arcade `-test` pass remain unproven; the latter is blocked
by the dependency below.

After archiving the execution reports and consumer manifests, all six owned
generated-asset directories and four marked comparison caches were removed.
The demonstration terminal was stopped, and no private process using this
worktree's SDK remained. Shipping outputs, baseline/removal package snapshots,
and diagnostic evidence were retained; no borrowed cache or process was changed.

### 2026-11-13: rebase and review readiness

The follow-up was rebased onto main
`e92578e3a4c076150c8b4174fbe04bd5b9df91d6`. Git applied the commit without
textual conflict markers, preserving main's newer source and translations.
The owning platform's `UpdateXlf` target was then rerun: ten translated catalogs
still contained the obsolete `TerminalProgressModeOptionDescription` unit after
the automatic merge. Regeneration removed those units without hand-editing any
translation. The localization guard's 41 self-tests and all 364 catalogs passed.

[dotnet/arcade#17784](https://github.com/dotnet/arcade/pull/17784) is now merged
at `d3c2ebbd7d9a81318117839335ba7e8540c3d77f`. The newly inherited Arcade pin
is **12.0.0-beta.26562.1**, source
`ffc98fd0de10a312712802f9b6b43da9326781fc`, which is the merge commit's parent.
Its actual runner target still appends `--no-progress`. No build for the fix's
exact commit was registered in BAR when checked, and no compatible replacement
package was established for this rebase. Readiness is therefore **ready for
review at the user's request**, not a claim that the package-flow prerequisite
or a stock Arcade test run has passed. The PR must retain that merge blocker.

The November 11 execution and counterfactual evidence above remains bound to
that earlier source/package state. Rebased validation is recorded separately;
it must not be presented as a replay of those earlier packages.

The rebased production/test delta against that main revision, including the
regenerated catalogs, has SHA-256
`3CA456F8DD10B2E5BEF20A39E23BA51A2F346C3CFB5E8B4CD962CAF3FD7E489F`.
The same repository SDK **12.0.100-alpha.1.26556.115** was used with the
updated pinned runtimes. The generated validation filter contains all 41
shipping source projects and the three affected test owners; no samples,
unrelated unit projects, or warning-policy overrides are included.

```powershell
& .\eng\build.ps1 -restore -build -pack -c Release -projects "$evidence\rebase\validation.slnf" -binaryLog -nodeReuse:$false /m:4 /p:UseSharedCompilation=false /p:EnableCodeCoverage=false '/bl:artifacts\log\Release\no-progress-rebase-pack-{}.binlog'
# Exit 0: zero warnings/errors; shipping packages and package guards passed.

& "$evidence\run-validation.ps1" -Suite unit -Framework net10.0 -RunName rebase-unit-net10
# Exit 0: 179 selected/executed/passed, zero failed/skipped.
& "$evidence\run-validation.ps1" -Suite unit -Framework net11.0 -RunName rebase-unit-net11
# Exit 0: 179 selected/executed/passed, zero failed/skipped.
& "$evidence\run-validation.ps1" -Suite unit -Framework net462 -RunName rebase-unit-net462
# Exit 0: 180 selected/executed/passed, zero failed/skipped.
& "$evidence\run-validation.ps1" -Suite mtp -Framework net12.0 -RunName rebase-mtp
# Exit 0: 47 selected/executed/passed, zero failed/skipped.
& "$evidence\run-validation.ps1" -Suite mstest -Framework net12.0 -RunName rebase-mstest
# Exit 0: 6 selected/executed/passed, zero failed/skipped.
```

The helper uses the same explicit filters, independent JSON discovery, host
arguments, and TRX/JUnit/CTRF checks documented above. Thus **538 unit cases
and 53 packed acceptance cases passed**, with no skips. The unit count is
three lower than the historical run because upstream removed its obsolete
cancelled-state data row on each TFM; that change was preserved. The new
`NoProgressOption_IsRejectedAsUnknownOption` case and all canonical-progress
cases still execute. Unlike the November 11 owner build, this strict build
needs no MSTEST0037 demotion.

The freshly packed core nupkg SHA-256 is
`E86B9599C5E8BF7A0247D6EC15996BBB0BFE729CE1A3935B1ABDCEDDEEB072B1`;
both acceptance caches matched that archive and all 20 physical consumer core
DLLs matched its TFM-specific payloads. The real native SDK module invocation
documented above was repeated against this run's source-generated MSTest
`HelpInfo` output: **`Test` passed, exit 0, total 1, failed/skipped 0, no ANSI
escapes**. The SDK's negative switches still belong to its own parser.

**Test-value evidence:** these are executed post-rebase preservation runs,
not a newly executed red phase or CI result. The earlier paired package
failure/pass remains historical evidence for the unchanged rejection assertion.
No new behavior is introduced by this rebase. Fresh reports and package/cache
manifests are retained separately from the November 11 artifacts.

## Remaining prerequisite

**The final MTP `--no-progress` removal is proposed in this follow-up, but
blocked from merge.** Arcade's `Microsoft.Testing.Platform.targets` must use
`--progress off`, a compatible Arcade SDK must be published, and that dependency
must flow into TestFx before merging this branch. The current pin still passes
the retired argument and would make the stock test runner fail command-line
validation. This branch removes the MTP registration, initialization/warning
state, resources, precedence handling, internal constant, and corresponding
compatibility/help/info expectations; it does not bypass the dependency gate.

The native SDK's separately parsed `--no-ansi` and `--no-progress` options are
not removed by this MTP-only change, and their retirement is not a prerequisite
for closing #12314. The module pool correctly retains SDK-owned `--no-ansi`;
replacing it would require support in the SDK renderer and remains outside this
PR's scope.
