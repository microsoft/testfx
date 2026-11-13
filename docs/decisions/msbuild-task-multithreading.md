# MSBuild task multithreading: research and decisions

## Problem and contract

`TestingPlatformSelfRegisteredExtensions` and `ConfigurationFileTask` must run
in-process during `-mt` builds without changing generated source, configuration
merging, errors, incremental-build behavior, or supported project languages.

The producer is `Microsoft.Testing.Platform.MSBuild`, including its
`buildMultiTargeting` targets and `_MSBuildTasks/netstandard2.0` assembly. Consumers
are projects importing that package, including projects using MSTest. Older
MSBuild hosts must continue to load the tasks. Each invocation owns its inputs
and output paths; one project's files must not be used by another. Other tasks,
including entry-point generation and test execution, are outside this migration.

## Research and experiments

### Migration research, 2026-11-10

- The [Microsoft Learn migration guide](https://learn.microsoft.com/visualstudio/msbuild/update-task-multithreaded)
  documents the compatibility bridge: define the marker locally and use
  project-bound absolute paths such as `ITaskItem.GetMetadata("FullPath")`.
- The [MSBuild thread-safe task specification](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md)
  separates routing (the attribute) from environment injection (the interface).
  The marker must be applied directly to each concrete task.
- The [API audit](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks-api-analysis.md)
  requires reviewing helpers, not just direct calls. These tasks use
  invocation-local collections and a stateless filesystem helper, with no
  registered task objects.
- The targets already normalize the registration output and configuration
  source. The implementations still used `ItemSpec` for file access, and the
  registration constructor read the debugger environment variable directly.
- Restoring stable task-authoring analyzer `18.13.0` failed with `NU1103` on the
  configured feeds. Preview `18.14.0-1.26559.118` restored from the existing public
  `dotnet-tools` feed. No additional feed is required. CI, source-build, and
  offline feed availability were not established by that local restore.

### Review correction, 2026-11-10

The published `Microsoft.Testing.Platform.MSBuild` preview
`3.0.0-preview.26556.8` contains the existing public/protected task signatures.
Comparing its API with this implementation found only the two `LaunchDebugger`
accessors added. Seed `PublicAPI.Shipped.txt` with that existing surface and keep
only the new property in `PublicAPI.Unshipped.txt`; do not present released APIs
as new.

## Alternatives and decision

Leaving the tasks unchanged preserves correctness through TaskHost isolation,
but does not meet the in-process migration goal. Referencing
`IMultiThreadableTask` and `TaskEnvironment` would require newer MSBuild hosts.
Maintaining separate task assemblies would add unnecessary packaging and
selection complexity.

Use the attribute compatibility bridge and project-bound `FullPath` metadata.
Preserve existing constructors and parameter types. Pass debugger activation
through the new public `LaunchDebugger` task input, defaulting to `false`,
rather than reading the process environment in the constructor. The importing
targets preserve the existing environment opt-in. Keep the analyzer private to
the task project; it must not become a package runtime dependency.

The preview analyzer remains pinned until a stable version is available on an
existing configured feed. No unrelated task is opted into multithreading.

## Validation and demonstration

The initial implementation was validated at
`22b50c1dfd3cf33b2a606d1868592398c5db91e5`, compared with unchanged production at
`8bdfc29f415696738d845d56aca5d438d0a068de` while retaining the same regression
tests. Each variant was built and packed separately, with isolated consumer
caches. These are local execution results, not independently published CI
evidence.

| Contract | Executed evidence |
| --- | --- |
| Registration writes to the resolved output. | `SelfRegisteredExtensions_UsesProjectResolvedFullPath`: passes on both unit-test TFMs; fails at the output-path assertion against the original implementation. |
| Configuration copy and merge use resolved paths. | `ConfigurationFileTask_UsesProjectResolvedFullPaths`: both data cases pass on both TFMs; the original implementation fails at missing-output or incorrect-path assertions. |
| Packaged tasks run in-process with project isolation. | `GenerationTasks_PreserveProjectIsolationAndRunInProcess`: multiprocess and multithreaded builds pass. Four generation invocations load the packed DLL, produce each project's own configuration, and compile consumers that execute only the associated hook. |
| The migration is observable. | With the original package, the multithreaded case fails: expected zero external generation TaskHosts, actual four. Its multiprocess counterpart passes. |
| Existing behavior and older hosts are preserved. | All 77 pre-existing cases per TFM stay green. The packed netstandard tasks also executed successfully on MSBuild 18.0, generating the expected source and merged configuration. |

Final initial-run totals were 160/160 unit cases and 2/2 packaged cases passed,
with zero skips and exit 0. The comparison executed the same selections: 154
unit passes and six intended failures; one packaged pass and one intended
failure, both commands exiting 2. Compilation or setup failures are not counted
as behavioral red-phase proof. Fresh TRX execution checks and JUnit parsing
confirmed the reported identities and counts.

After the review corrections to API classification, documentation, and comments,
the same selections were rerun: 160 unit cases and two packaged cases passed
with exit 0 and no skips. No production behavior or regression assertion changed.

### Reproduction

Use the repository-pinned toolchain and pack before running acceptance tests,
following [the contributor commands](../../.github/copilot-instructions.md#build-test-and-debug-commands).
The focused selections are:

```powershell
.\.dotnet\dotnet.exe test --project test\UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests.csproj -c Debug --no-build --filter 'FullyQualifiedName~Microsoft.Testing.Platform.MSBuild.UnitTests.MSBuildTests' -p:UsingDotNetTest=true
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-build --filter 'FullyQualifiedName~MSBuildMultithreadingTests' -p:UsingDotNetTest=true -p:TestRunnerAdditionalArguments=
```

The acceptance test follows the upstream
[migration validation contract](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md#validate-a-task-migration):
enable strict mode, promote `MSB4286`/`MSB4287`/`MSB4288` to errors, and require
exactly one `MT-sentinel-CWD` occurrence. MSBuild emits that occurrence in its
strict-mode enable message; any additional logged occurrence indicates a
process-relative path leak. This check is intentionally tied to the
repository-pinned SDK's strict-mode contract, not to this package's own logging.
Keep the independent TaskHost-routing and per-project output assertions too.

### Limits

Scoped task packing and all three task TFMs (`netstandard2.0`, `net10.0`,
`net11.0`) passed. Full-solution packing with UAP excluded compiled successfully
but failed the existing Windows package file-count verifier for
`MSTest.TestAdapter` and `MSTest.TestFramework`; that check was not suppressed.
CI, desktop MSBuild, non-Windows execution, source-build, offline restore,
interactive debugger attachment, and performance gains remain unverified.

This is a nonvisual build-routing change, so executable consumer assertions and
binlogs are more useful than screenshots. Both test assemblies retain
method-level parallelization; new tests own their assets and override only
child-process environments. Release-branch servicing and downstream rollout
are outside this change.

## Repository build defaults, 2026-11-13

### Contract and decision

Enable multithreaded MSBuild by default for repository command-line builds,
including Windows public and official CI builds. Preserve the selected build
engine, targets, output layout, exit codes, and explicit compatibility opt-outs.
This changes build scheduling, not the public package API or test scheduling.
No performance improvement is promised.

Arcade already passes `-mt` to .NET MSBuild but excludes desktop `MSBuild.exe`.
Its [upstream tracking issue](https://github.com/dotnet/dotnet/issues/9989)
identifies Visual Studio 18.12 as the prerequisite for removing that exclusion.
The installed desktop MSBuild is 18.13.0.52701. Raise `tools.vs.version` to 18.12
so the supported default does not silently depend on an older, incapable host.
[Public CI run 1674604](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1674604),
log 56, reports Visual Studio 18.12.12316.0 on the existing VS 2026 scout image.
That confirms the public image prerequisite, not execution of this patch in CI.

Use the repository-owned `eng/configure-toolset.ps1` and `.sh` hooks, imported
by Arcade before toolset initialization, to set `MSBUILDENABLEMULTITHREADED`
from its existing multithreading option. The
[documented environment default](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/MSBuild-Environment-Variables.md)
also reaches child MSBuild invocations and, unlike
`MSBUILDFORCEMULTITHREADED`, allows `-mt:false` to win. Expose the existing
`-msbuildMultiThreaded` / `-mt` option through the Windows repository wrapper.
Set the same default at pipeline scope for build/test steps that invoke the
.NET CLI directly rather than going through Arcade.

Do not edit mirrored `eng/common` files, switch Windows to .NET MSBuild
(which would break desktop/UWP tooling), or rely solely on
`Directory.Build.rsp` (Arcade's entry project can live outside the checkout).
Retain intentional multiprocess/legacy test cases and BuildXL's separate
native scheduler. No package release, dependency flow, or servicing change is
required; the change applies to this branch's build infrastructure.

### Validation

| Requirement | Evidence | Presentation |
| --- | --- | --- |
| Windows local and CI entry points use the new default. | `test_entry_points_enable_multithreading_by_default` imports the actual Arcade tools and configuration hook through the batch wrapper, PowerShell wrapper, and direct common entry point. Desktop and pinned .NET engine probes log multithreaded execution. | Nonvisual; binlogs and command output retained locally. |
| Explicit opt-out remains effective. | `test_entry_points_disable_multithreading_despite_inherited_ci_default` checks `-mt:0` against an inherited CI default of `1`. Both real engines honor the hook opt-out and `/mt:false`. | Nonvisual. |
| Existing wrapper failure semantics survive. | `test_batch_entry_point_preserves_arcade_exit_code` and `test_powershell_entry_point_preserves_arcade_exit_code` preserve exits 0 and 23. | Nonvisual. |
| Shell and direct CI invocations receive the default. | `test_toolset_propagates_multithreading_default_and_opt_out` passes using Git Bash. Both pipeline declarations parse with the default set to `1`. | Native Linux/macOS and hosted CI execution remain unverified. |

Baseline: `e92578e3a4c076150c8b4174fbe04bd5b9df91d6`. Before implementation,
`python -m unittest discover -s .github\workflows\tests -p test_build_entry_points.py -v`
passed both existing tests (exit 0). New tests use per-case temporary checkouts
and child-process environments; they do not mutate the Python host environment
or share build outputs. The Python unittest runner is sequential; no MSTest
assembly scheduling or resource declarations changed.

The tested implementation/configuration/test patch, excluding documentation,
has SHA-256
`747327f9902fa3d3dd0940469417dc6b81a77887a847e4ea56cbc5ef2e9c7fff`.
The retained test file has SHA-256
`5d489c30ddd4e27a5ad788bea51723a699953c1c96d75cdcdd10926e1451f1d1`.

**Test-value evidence:** the same five tests were run against an isolated copy
of the baseline scripts, with the new tests retained. The two existing tests
passed; the three new tests reported eight intended subcase assertion failures
(exit 1, no errors or skips): enabled defaults were absent, and inherited `1`
remained set when disabled. Rerunning against the current repository scripts
passed all five tests, including all 12 subcases (exit 0, no skips). The isolated comparison
directory was removed; repository source and build outputs were not reverted.

Executed commands and results (`$evidence` identifies this session's retained
`files/mt-default-validation` directory):

| Command | Result |
| --- | --- |
| `python -m unittest discover -s .github\workflows\tests -p test_build_entry_points.py -v` | Exit 0: 5 tests / 12 subcases passed; no skips. Final green run followed the isolated counterfactual. |
| `python "$evidence\counterfactual.py"` | Expected exit 1: the same 5 methods executed; 8 intended assertion failures, no errors or skips. |
| `python .github\scripts\test_buildxl_pipeline.py -v` | Exit 0: 29 existing BuildXL pipeline/cleanup contract tests passed. |
| `.\build.cmd -c Release -bl -nodeReuse:0` | Exit 0: full Windows solution built with desktop MSBuild 18.13 and pinned SDK `12.0.100-alpha.1.26556.115`, including desktop/UWP targets. Eight existing NuGet audit warnings; no errors. |
| `.\build.cmd -c Release -pack -nodeReuse:0 "/bl:$binlog"` | Exit 0: built and packed successfully, including package-content/metadata validation and 32 shipping packages. The unique binlog is `$evidence\pack-384b82ba3e0040d8b95209069b17d47a.binlog`. |
| `& "$evidence\verify-engines.ps1" -Repository (Get-Location).Path` | All six cases exited 0: default enabled, Arcade opt-out disabled, and explicit `/mt:false` disabled, on both desktop and pinned .NET engines. Runtime strict-mode messages distinguish actual execution mode from merely setting an environment variable. |

`artifacts/log/Release/Build.binlog` records `MSBuild.exe`, one
`Multi-threaded strict mode is enabled.` message, and a successful build of
73 projects. The pack binlog confirms the same execution mode. An
initial pack launch rejected a `{}` binlog filename after batch-wrapper argument
parsing; the successful rerun used the explicit GUID filename above. That
argument failure built no projects and is not regression-test evidence.
The local six-case probe logs, build/pack logs, saved implementation patch, and
isolated comparison transcript are retained as session evidence, not published
CI artifacts.
The mirror under `eng/common` is unchanged. BuildXL restores the new environment
value along with its other borrowed process settings; its native scheduler's
explicit environment allowlist remains unchanged.

Native Linux/macOS product builds, hosted public/official CI, signing,
publication, and the full acceptance suite were not run for this build-default
change. No shipping task assembly changed, and no new performance claim is made.
