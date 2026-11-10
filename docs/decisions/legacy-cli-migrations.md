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

Planned evidence, not yet execution claims:

| Requirement / risk | Automated consumer evidence | Surface |
| --- | --- | --- |
| MTP ANSI alias is rejected; modern values still work | Packed `AnsiOptionTests`, provider tests, direct-host producers, help and info expectations. | Real child stdout/stderr, including forced ANSI and non-ANSI output. |
| Native module pool still configures the SDK renderer | Module-pool launch argument checks and a real pinned-SDK module invocation using `--progress off --no-ansi`. | Native SDK output; `--no-ansi` here is not an MTP alias. |
| No HangDump `%p` substitution; canonical templates and paths preserved | HangDump naming unit cases and packed custom/default/template/subdirectory/space/traversal cases. | Actual dump filename and child exit; no UI change. |
| Regression sensitivity | Same tests retained in isolated production comparisons, with separate outputs and packages. | Intended behavioral assertion failures, not setup or compilation errors. |

Build and pack use the repository-pinned toolchain. Package-consuming acceptance
tests run only after a configuration-matched pack, with fixture-owned caches.
Record exact commands, revisions/patches, test identities/counts, exits, package
provenance, and unavailable evidence here as validation proceeds.

The prepared changed-production patch, excluding documentation, is SHA-256
`74DEA917F87B654685FC1411A4722F7E6A4C423501C45FDCE24A7CE135DD2851`
(`git diff --binary` for `src`, `test`, and the two module-pool scripts).
The isolated comparison remains at the pristine production base above, with
the identical test/harness patch SHA-256
`BAFCEEDD8DBA25EFD6D5D277441FA2CD17742D1E0E17160ED34BA49BB4EE76E2`.
The comparison's `src`, global build configuration, versions, and module-pool
producer have no tracked changes. Neither hash is a fabricated commit ID.

The prepared focused cases are:

- Twelve naming rows in
  `EnsureProcessIdPlaceholder_MakesCustomDumpPathUnique` and
  `GetDumpFileNamePattern_PreservesRootNameAndMakesChildNamesUnique`.
- Two writer-path rows in
  `GetDumpFileNames_PercentCharactersAreLiteralInTheArtifactPath`, plus the
  existing Windows-space quoting case.
- `GetCommandLineOptions_DoesNotIncludeNoAnsiOption` and
  `ValidateOutputOptions_UnregisteredOptionThrows`, plus existing canonical
  option validation and retained progress-alias coverage.
- Packed `NoAnsiOption_IsRejectedAsUnknownOption` and
  `HangDump_LegacyPercentPlaceholder_IsLiteralFileName` for behavioral red/green
  proof, followed by canonical ANSI, `NO_COLOR`, dump-template/path, help/info,
  `ExitOnProcessExit_Succeed`, and
  `DotnetRun_WithCoverageSettings_PreservesInvocationWorkingDirectory` coverage.
- The existing module-pool validator, whose actual worker argv now checks the
  canonical progress tokens and the independently owned SDK ANSI switch.

These are planned execution identities, not passing-test claims. Scheduling
remains method-level with CPU-count workers. New naming rows are pure strings;
acceptance methods use GUID result directories and child-only environment
overrides. Existing console-lock coverage, rendezvous, process joins, and
fixture cleanup are unchanged.

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
