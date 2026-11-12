# Windows build output reuse

## Problem and contract

The public and official pipelines rebuild products and packages on Linux, and
the public pipeline also rebuilds them on macOS, although Windows produces the
packages that ship. Trial sharing the Windows Release outputs without changing
the selected tests or the acceptance tests' target-OS consumer builds.

`UseWindowsBuildOutputs` is an explicit, disabled-by-default pipeline parameter.
The trial applies when tests are enabled and, in the official pipeline, when
`products=all`. Other combinations retain their existing build path. This keeps
the native source-build baseline available for comparison and rollback.

The primary Windows Release job owns a versioned archive containing:

- Configuration-matched `artifacts/bin` outputs, including dependencies, test
  data, runtime configuration files, and both product and test assemblies.
- Configuration-matched Shipping and NonShipping packages.
- Generated `coverage.config` files, but no other intermediates, restore caches,
  SDKs, workloads, test results, or credentials.
- A manifest binding the payload to the source commit, configuration, original
  repository/package roots, and individual file lengths and SHA-256 hashes.

The archive is published after the build and before tests, under a name that
includes the producer's job attempt. Consumers depend on the Windows job and use
its output variable to select that exact artifact from the current pipeline run.
They may still run when Windows tests fail after successful artifact publication.
A missing, stale, malformed, or incompatible payload fails rather than falling
back to an unreported rebuild. A producer that never exports a payload does not
start artifact-consuming jobs.

Each consumer keeps its own checkout, pinned SDK, runtimes, workloads, and NuGet
restore. It imports the outputs, rebases coverage source/package path expressions,
and rebases the physical paths in
`.msCoverageExtensionSourceRootsMapping_<assembly>` sidecars without changing
their virtual path-map keys or encoding. It then runs the existing test
selection with `UseAppHost=false`. The property is
passed to the outer test command, not exported as an environment default that
could change generated acceptance applications.

The public pipeline retains `NonWindowsTests.slnf`, affected-test collection and
fallback, report arguments, diagnostics, and result publication. The official
pipeline retains its Arcade unit/integration test command. Windows-only TFMs and
application models remain in Windows jobs. Cancellation and test exit handling
remain owned by the existing SDK/Arcade runners.

## Research and experiments

### 2026-11-10: source audit and initial design

The audit at `32a6ed6e1511ae9b61c9ce307e22ed5594b239cc` found no Linux/macOS
preprocessor branches in tracked C# sources. `WIN_UI`, `WINDOWS_UWP`,
`CLASSIC_UWP`, and `PACKAGEDAPP_WINRT` follow target frameworks. Linux-only unit
tests use runtime `OSCondition` attributes.

Relevant existing contracts:

- `eng/Build.props` selects the non-Windows solution/project set.
- `test/Directory.Build.props` already disables apphosts on macOS.
- `test/Directory.Build.targets` generates coverage settings containing absolute
  regex-escaped repository and NuGet roots.
- `Microsoft.Testing.Platform.MSBuild.targets` supports DLL-based test launching.
- `eng/common/cibuild.sh` enables build and pack by default; consumers must use
  the restore-only `eng/common/build.sh` entry point instead.
- The test infrastructure resolves configuration-matched local Shipping and
  NonShipping feeds and builds generated consumer applications on the test OS.

The archive contract tests run in public change-detection preflight on Linux and
before trial publication on Windows. The same 12 tests passed locally on Windows
and in `mcr.microsoft.com/powershell:7.4-ubuntu-22.04`, including relocated roots
with spaces/ampersands, nested Windows-to-Unix coverage paths, configuration
isolation, stale commits, wrong configurations, unsupported formats, missing
manifests, corrupted files, traversal, duplicates, and existing-output rejection.

The Windows-to-Unix case initially failed at its source-root regex assertion:
replacing only the repository prefix left `src\\` and other relative Windows
separators in Linux coverage filters. Normalizing the rebased path's escaped
directory separators fixed it; invariant mapped-source expressions remain intact.

The composed Linux run executed all 37 selected Windows-built test cases,
but coverage had no coverable data and logged `no_symbols`. The following
bounded experiments did not resolve that blocker:

- Extracting embedded portable PDBs into supplemental sidecars.
- Producing normal portable PDBs on Windows, with matching assembly/PDB IDs.
- Adding explicit symbol search directories.
- Removing source filters in an isolated diagnostic configuration.
- Running the same Windows payload on native Linux storage instead of the
  Windows bind mount.

The runtime's own `TryOpenAssociatedPortablePdb` successfully found the
transported portable PDB, but the coverage collector still rejected symbols.
A clean Linux-created checkout built the same source and ran the same 35
deadline cases with 19.0% line coverage, whereas the Windows artifact reported
no coverable data. This establishes a composed-product parity gap, not a
successful rollout. The unsuccessful PE extraction and symbol-format changes
were removed from the delivered patch; it preserves existing build formats.

Existing affected-test validation passed 71 self-tests. Collection scheduling
passed its 32 Linux cases; its Windows-only orphan cases passed too, but that
Windows invocation subsequently failed to delete its temporary fixture. No
collection scheduler change was made. Both pipeline YAML files and their shared
step templates parsed, and structural checks confirmed default-off rollout,
producer dependencies/output references, current-run artifact selection, and
restore-only consumer commands.

### 2026-11-12: coverage source-root mapping diagnosis

Following the coverage owner's guidance, the retained controller diagnostics
were inspected beyond the final `no_symbols` summary. They show the portable
PDB being loaded, followed by
`AssemblyMetadataProcessor.IsIncludedBySources: ... as all sources do not exist`.
The collector reads `.msCoverageExtensionSourceRootsMapping_<assembly>` from
the transported output and maps `/_/` back to the original Windows checkout.
Rebasing `coverage.config` alone does not relocate this separate metadata.

The generated coverage settings do not set any `SymbolsRestrict*` switches.
The tested coverage package's configuration defaults were also inspected:
`SymbolsRestrictOriginalPathAccess`, `SymbolsRestrictReferencePathAccess`,
`SymbolsRestrictDBGAccess`, and `SymbolsRestrictSystemRootAccess` are all false.
The repair does not change those switches. It rebases the physical
project/source/package paths in the mapping files and includes hidden sidecars
when enumerating outputs on Unix. Longest-root-first matching retains the
consumer package root when the producer's package cache is inside its checkout.

`CoverageSourceMappingsUseConsumerPaths` failed against the unchanged importer
at the stale repository-path assertion. With the repair, all 13 transport
cases pass on Windows and Linux, including Windows-to-Unix nested paths,
UTF-16 encoding, unchanged virtual roots, and package-cache relocation.

A fresh paired experiment built Windows binaries from
`f50f73e66dd136c88c213795ffcf207ad8bf3321`, packed the matching MTP core, and
used the same archive with the old and repaired importers in isolated Linux
checkouts. SDK `12.0.100-alpha.1.26556.115`, Release/net11.0, runtime
`11.0.0-rc.3.26522.148`, and CodeCoverage
`18.13.0-preview.26560.3` were used. The production DLL in
`Microsoft.Testing.Platform.3.0.0-ci.nupkg` matched the test host's loaded output
(SHA-256 `31964BFC9C73D598A7551FE4A128D3B73478B1B2B7E7E0FA409DFEC30900A979`).

| Execution | Selected/executed/passed | Runner line coverage |
| --- | --- | --- |
| Windows-built binaries on Windows | 35 / 35 / 35 | 18.9% |
| The same binaries on Linux, old importer | 35 / 35 / 35 | No coverable data |
| The same binaries on Linux, repaired importer | 35 / 35 / 35 | 18.9% |

Both Linux selections were reconciled by exact TRX names against the Windows
baseline. Each exited zero with no failed/skipped cases. All 2,836 transported
DLL/PDB files stayed byte-identical in both variants, and replaying both restore
and no-build test binlogs found zero compiler tasks. The repaired report
contains real source coverage and the correctly relocated Linux source filename:
the source-linked `DeadlineHelper` copy has 43/43 covered line entries, while
the shipping-core copy has 4/43. These are separate module records, not combined
shipping-only coverage.
The full Cobertura report has 5,223 covered line entries out of 25,000, versus
an empty `<packages />` in the old-importer report. These report-entry counts
are not the runner summary's differently aggregated percentage denominator.

The shared test invocation, run from each isolated consumer checkout, was:

```text
dotnet test --project <consumer>/test/UnitTests/Microsoft.Testing.Platform.UnitTests/Microsoft.Testing.Platform.UnitTests.csproj -c Release --framework net11.0 --no-build -p:UseAppHost=false -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=<results> -bl:<results>/test.binlog --filter FullyQualifiedName~DeadlineHelperTests --show-test-results failed
```

The actual commands, roots, reports, diagnostic logs, binary hashes, and
counterfactual helper are retained in the session's `coverage-source-map`
evidence. Initial scratch-harness attempts failed on a PowerShell interpolation
error and an assumption that empty Cobertura reports have line-count attributes;
those are setup/verification errors, not behavioral red-phase evidence.

This corrects the earlier diagnosis: `no_symbols` was the final skip category,
but the detailed cause was unresolved source paths, not unsupported Windows
PDBs. The focused Linux coverage blocker is resolved. Full acceptance, macOS,
and live pipeline transport still need validation; the trial remains off.

## Alternatives and decision

Keep local builds as the baseline and rollback. For the trial, retain project
evaluation rather than introducing a second module-selection/reporting runner:
it preserves existing solution filtering and test/report options. Transfer only
the necessary coverage intermediates and rebase their XML regex values rather
than copying Windows `obj` restore state or regenerating all outputs.

The initial implementation deliberately keeps the existing Windows build/test
job boundary. Publication happens before tests, but consumers start only after
that job finishes. This can reduce aggregate build work without reducing elapsed
CI time. Splitting Windows build and test jobs is a separate optimization, not a
prerequisite for proving payload correctness.

## Validation and demonstration

Windows validation used the pinned SDK `12.0.100-alpha.1.26556.115` and built
`Microsoft.Testing.Platform.UnitTests` and
`Microsoft.Testing.Platform.MSBuild.UnitTests` in Release, with
`ContinuousIntegrationBuild=true` and `DeterministicSourcePaths=true`. For each
project the command was:

```powershell
.\eng\build.ps1 -restore -build -configuration Release -projects <project> -msbuildEngine dotnet -binaryLog /p:ContinuousIntegrationBuild=true /p:DeterministicSourcePaths=true
```

Both project builds passed. Initial bootstrap attempts failed because the
runtime-install `.cmd` wrapper could not resolve `powershell`; invoking the
repository's PowerShell runtime installer directly installed the pinned
prerequisites. A semicolon-separated `-projects` attempt also failed argument
parsing; the successful commands invoked the two project builds separately.
These setup failures are not test-value evidence.

The built core project packed successfully:

```powershell
.\.dotnet\dotnet.exe pack src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -c Release --no-build /p:ContinuousIntegrationBuild=true /p:DeterministicSourcePaths=true -bl:artifacts\log\Release\windows-build-reuse-pack.binlog
```

The initial embedded-symbol export contained 3,040 files and
`Microsoft.Testing.Platform.3.0.0-ci.nupkg`; its ZIP was 223,910,157 bytes.
The portable-symbol experiment exported 3,186 files with an aligned
`Microsoft.Testing.Platform.3.0.0-ci.nupkg`. These are representative two-suite
payloads, not full-pipeline size estimates; the trial does not change symbol
format or rewrite assemblies.
Windows no-build DLL execution with coverage and TRX/CTRF/JUnit reporting passed
all 35 `DeadlineHelperTests` cases. The two data rows of
`TryGetDotnetPathByArchitecture_LinuxConfiguredElfMuxer_IsRejectedWithoutFallback`
were present and skipped on Windows as expected.

A separate checkout outside the repository was used for the real Linux
restore/no-build experiment. Its initial scoped restore used a relative project
path that Arcade resolved against its toolset directory; the successful restore
uses an absolute project path. This does not affect the pipeline's normal
solution-selected restore. A native-build attempt in the Windows-created
comparison checkout hit 194 formatting diagnostics. A clean Linux-created
comparison checkout subsequently built with zero warnings/errors and passed
all 35 deadline cases with nonempty coverage. No unrelated source was changed.

The Windows artifacts executed 35 deadline cases and both Linux-only muxer
data rows successfully on Linux, with TRX, CTRF, JUnit, and coverage files
produced. All 2,722 transported DLLs remained byte-identical. Replaying the
restore and no-build test binlogs found zero Csc/Vbc/Fsc tasks. Coverage-file
existence alone was rejected as proof because their contents were empty.

The transport self-tests passed on Windows and Linux:

```powershell
.\eng\test-windows-build-outputs.ps1
.\eng\validate-affected-tests.ps1 -SelfTest
```

Results: 12 transport cases on each OS and 71 affected-test cases passed.
Removing only the source-commit comparison in an isolated helper copy made
`RejectsStaleSourceBeforeExtraction` fail at its intended rejection assertion.
The earlier nested-path test also exposed the unnormalized Windows separators
before the corresponding fix. These are behavioral detections, not setup
failures or skipped-test evidence.

No Azure pipeline or macOS execution has been established yet. Compare the same
commit, configuration, and test inventory with
the flag disabled and enabled. Require matching selected/executed module/TFM
identities, working reports/coverage, current package provenance, and preserved
failure exits. Measure total agent-minutes, artifact size/transfer time, and
end-to-end duration; do not infer a saving from source changes alone.

This is build infrastructure, so application UI/media is inapplicable. The
observable surfaces are runner exits, module results, packages, and CI artifacts.

## Open questions

- macOS execution and the live public/1ES artifact transport require CI evidence.
- The source-root repair restores focused Windows-to-Linux coverage. Keep the
  trial disabled until complete cross-platform validation succeeds; do not
  remove coverage options or accept empty coverage to make the experiment green.
- This change targets the current branch; release-branch adoption/backports need
  separate validation before enabling reuse there.
- The change owner must compare the trial and baseline and either enable the
  proven path or remove the experiment; the flag does not lower the validation bar.
