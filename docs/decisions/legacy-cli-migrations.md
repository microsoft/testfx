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

## Remaining prerequisite

**MTP `--no-progress` removal is blocked**, not implemented. First migrate
Arcade's `Microsoft.Testing.Platform.targets` to `--progress off`, publish a
compatible Arcade SDK, and flow that dependency into TestFx. Then remove the
MTP registration, initialization/warning state, resources, precedence handling,
internal constant, and corresponding compatibility/help/info expectations.
The existing alias and its tests remain until that prerequisite is satisfied.

The native SDK's separate `--no-ansi` spelling is also not migrated: a future
SDK must honor `--ansi off` in its own renderer before the module-pool producer
can replace that SDK-owned argument. Neither prerequisite authorizes changes to
Arcade or the SDK in this PR.
