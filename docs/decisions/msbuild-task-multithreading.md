# MSBuild task multithreading: research and decisions

## Problem and contract

`TestingPlatformSelfRegisteredExtensions` and `ConfigurationFileTask` must be
eligible for in-process execution during `-mt` builds. Their generated source,
configuration merging, errors, incremental-build behavior, and supported project
languages must remain unchanged.

The producer is `Microsoft.Testing.Platform.MSBuild`, including its
`buildMultiTargeting` targets and `_MSBuildTasks/netstandard2.0` assembly. Consumers
are projects importing that package, including projects using MSTest. The task
project also builds for `net10.0` and `net11.0`. Older MSBuild hosts must continue
to load the shipped task without requiring the new `TaskEnvironment` APIs.

Each invocation owns its inputs and output paths; different projects must not
read or overwrite one another's files. There is no process-launch or handshake
contract for these two generation tasks. Existing task failures must still fail
the build. Other tasks, including entry-point generation and test execution, are
outside this migration.

## Research and experiments

### 2026-11-10: migration contract

- [Microsoft Learn migration guide](https://learn.microsoft.com/visualstudio/msbuild/update-task-multithreaded)
  documents the compatibility bridge: define
  `Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute` locally and
  avoid process-relative APIs. `ITaskItem.GetMetadata("FullPath")` is an
  established source of project-resolved absolute paths.
- [MSBuild thread-safe task specification](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md)
  distinguishes routing (the attribute) from environment injection (the
  interface). The attribute must be applied directly to each concrete task and
  must not be inherited.
- [MSBuild API audit](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks-api-analysis.md)
  requires auditing helpers as well as direct calls. These tasks have
  invocation-local collections and use a stateless filesystem helper; neither
  uses registered task objects.
- [Task authoring analyzer 18.13.0](https://www.nuget.org/packages/Microsoft.Build.TaskAuthoring.Analyzer/18.13.0)
  analyzes attributed tasks and their source call chains independently of the
  MSBuild runtime dependency. Its suggestions for newer typed task parameters
  are not a reason to break older task hosts.
- The current targets already normalize the self-registration output and
  configuration source against `MSBuildProjectDirectory`. The task
  implementations nevertheless use `ItemSpec` for file access. The
  self-registration constructor also reads
  `TESTINGPLATFORM_MSBUILD_LAUNCH_ATTACH_DEBUGGER` directly.
- The repository-pinned SDK is `12.0.100-alpha.1.26556.115`; the MSBuild reference
  packages remain at `18.9.6`. Baseline build and subsequent migration
  experiments are recorded below as they complete.
- Baseline at `8bdfc29f415696738d845d56aca5d438d0a068de`: the focused unit-test
  project built successfully with the repository's `eng/build.ps1` entry point
  (without invoking Windows SDK installation). The focused `MSBuildTests`
  selection executed 77 cases on each of `net10.0` and `net11.0`: 154 passed,
  zero failed/skipped. The pinned SDK reports MSBuild `18.14.0.55715`.
- Restoring analyzer `18.13.0` failed with `NU1103`: the configured feeds do not
  contain that stable version. The existing public `dotnet-tools` feed contains
  `18.14.0-1.26559.118`, which restores successfully. Use that build-only preview
  rather than adding a feed or requiring access to the private VS feed.
- The task project had no public API baseline files. Adding the explicit
  `LaunchDebugger` task input therefore also initializes its API inventory:
  existing signatures and the new property are listed in `PublicAPI.Unshipped.txt`,
  with an empty shipped baseline. No existing signature is changed.
- The migrated project builds all three TFMs with zero warnings/errors. The
  focused generation-task suite executes 80 cases on each of `net10.0` and
  `net11.0`: 160 passed, zero failed/skipped, including the three new resolved-path
  cases. No task-authoring diagnostic suppression was needed; the two `CS0436`
  suppressions select the deliberately embedded compatibility marker.
- Packaged validation executes two projects, `Copy` and `Merge`, with two build
  workers. Both generation tasks execute once per project, without TaskHost
  routing. Each task assembly matches the packed DLL by SHA-256 and its path
  belongs to the consumer's resolved package folders. The compiled consumers
  execute the appropriate generated hook, and their configuration files retain
  the correct project identity and defaults.
- The strict-mode binlog contains exactly one `MT-sentinel-CWD` occurrence: the
  enable message. No sentinel paths leak into task parameters, outputs, or other
  logged values. `MSB4286`, `MSB4287`, and `MSB4288` are promoted to errors.
- The same new tests were built against unchanged production sources at the
  baseline in an isolated sibling worktree. All six resolved-path cases fail
  at their intended assertions; the other 154 unit cases still pass. The
  packaged multithreaded case fails because four generation invocations use
  TaskHosts, while its multiprocess counterpart passes.
- The final package also executes both tasks through its imported targets on
  installed SDK `10.0.114`, MSBuild `18.0.36.52326`, without `-mt`. The generated
  hook and merged configuration match the expectations. This is actual
  older-host execution, not just inferred assembly compatibility.
- Earlier validation setup failures were corrected before sign-off: incomplete
  API inventories (`RS0048`/`RS0016`), a missing acceptance `TestContext`,
  `ConfigureAwait` in the generated consumer, discovery's explicit `text`
  format, and the assumption that NuGet has only one package folder. None is
  counted as behavioral red-phase evidence.

## Alternatives and decision

Leaving the tasks unchanged preserves correctness through TaskHost isolation,
but does not meet the in-process migration goal. Referencing
`IMultiThreadableTask` and `TaskEnvironment` would couple the shipped task to
newer MSBuild; maintaining separate task assemblies would add unnecessary
packaging and selection complexity.

Use the compatibility bridge, preserve existing task parameter types, resolve
file access from project-bound item metadata, and pass the debugger opt-in as an
explicit task input from MSBuild rather than reading process environment state.
Add the task-authoring analyzer as a private build dependency of this project
only. Do not mark unrelated tasks as thread-safe.

## Validation and demonstration

| Requirement / risk | Automated evidence | Rendered check / gap |
| --- | --- | --- |
| Both generation tasks execute in-process with `-mt`. | `GenerationTasks_PreserveProjectIsolationAndRunInProcess`: 2/2 cases passed; four real generation invocations per build, packaged DLL hashes, strict mode, per-project outputs, and executed consumers. | Nonvisual build contract; binlog and exact artifact assertions are more useful than screenshots. |
| Relative registration output identities do not depend on process CWD. | `SelfRegisteredExtensions_UsesProjectResolvedFullPath`: passed on both unit-test TFMs; fails against the original task at the output-path assertion. | No UI changes. |
| Configuration source and destination use resolved paths. | `ConfigurationFileTask_UsesProjectResolvedFullPaths`: copy and merge cases pass on both TFMs; both fail against the original task at missing-output or incorrect-path assertions. | No UI changes. |
| Existing copy, merge, missing-input, and invalid-input behavior is preserved. | All 77 pre-existing cases per TFM remain green, including the `netstandard2.0` configuration implementation. | No UI changes. |
| Older task hosts remain supported. | All three task TFMs build; actual packed-task execution on MSBuild 18.0 passes with the original parameter types and constructors. | Desktop MSBuild and non-Windows execution were not run locally. |
| The migration regressions execute and detect absent behavior. | Unit comparison: 154 passed, 6 intended failures, 0 skipped. Package comparison: multiprocess passed; multithreaded failed with four TaskHost invocations. Final state: 160 unit cases and 2 package cases passed. | TRX execution gates and JUnit parsing confirm execution and report validity, not merely discovery. |

No performance improvement is claimed without comparable measurements. Local
results are not CI execution evidence.

### Source and package provenance

The tested source state is baseline
`8bdfc29f415696738d845d56aca5d438d0a068de` plus
`artifacts\mt-validation\tested-source.patch`, SHA-256
`D7608217087F8AEF689C9C5033548D06F850AA22B829E330004B0CA2A52B01E8`.
That patch includes all changed production, package, API, and test files,
including new files; this decision record is excluded to avoid a self-referential
hash.

Both package variants use ID `Microsoft.Testing.Platform.MSBuild`, version
`3.0.0-dev`, with separate consumer caches:

| Variant | Package SHA-256 |
| --- | --- |
| Final implementation | `BF62382A92CF1F184661B5B45E79C72D3FBD94ACB213C853349766F21D4E7278` |
| Original production sources, same regression tests | `3C0B719B56B363D6237E9E528F365C252CC6F97A3C136A2E339B2C016579DEE2` |

The retained counterfactual acceptance source has the same SHA-256 as the final
test source:
`5B5666D78F8B4DC0FA399F2819577449AB90D41908FE4FD1250A09A1E978EBA7`.
Logs, TRX/JUnit reports, the old package, retained test patch/source, and
comparison binlogs are under `artifacts\mt-validation\counterfactual`.
Final execution reports are under `unit-final-verified` and `acceptance-final`
in the same artifact directory.

The decisive comparison assertion was:

```text
Assertion failed. Expected collection to be empty.
expected count: 0
actual count:   4
Assert.IsEmpty(<collection>)
```

Its binlog records `TestingPlatformSelfRegisteredExtensions` and
`ConfigurationFileTask` each launching an external TaskHost for both `Copy` and
`Merge`. In the final package there are zero such launches. Both final consumers
exit 0 and emit only their associated `hook=Copy` or `hook=Merge` marker.

### Commands and results

These are reproducible forms of the executed commands, with output paths made
relative to the worktree. Test processes use the bootstrapped `.dotnet` on
`PATH` and as `DOTNET_ROOT`.

```powershell
.\eng\build.ps1 -restore -build -projects test\UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests.csproj -msbuildEngine dotnet -bl
.\.dotnet\dotnet.exe pack src\Platform\Microsoft.Testing.Platform.MSBuild\Microsoft.Testing.Platform.MSBuild.csproj -c Debug --no-build --no-restore -bl:artifacts\mt-validation\task-pack.binlog
.\.dotnet\dotnet.exe build test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-restore -bl:artifacts\mt-validation\acceptance-build-final.binlog -v:quiet
```

All three commands pass (exit 0). The task builds cover `netstandard2.0`,
`net10.0`, and `net11.0`; unit tests cover `net10.0` and `net11.0`; the outer
acceptance host targets `net12.0` and its generated consumers target `net10.0`.
No analyzer or other `Microsoft.Build` package leaks into runtime dependencies.

```powershell
$unitResults = Join-Path $PWD 'artifacts\mt-validation\unit-final-verified'
$acceptanceResults = Join-Path $PWD 'artifacts\mt-validation\acceptance-final'
.\.dotnet\dotnet.exe test --project test\UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests\Microsoft.Testing.Platform.MSBuild.UnitTests.csproj -c Debug --no-build --filter 'FullyQualifiedName~Microsoft.Testing.Platform.MSBuild.UnitTests.MSBuildTests' -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$unitResults"
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Debug --no-build --filter 'FullyQualifiedName~MSBuildMultithreadingTests' -p:UsingDotNetTest=true -p:TestRunnerAdditionalArguments= "-p:ArtifactsTestResultsDir=$acceptanceResults" --show-test-results all
```

Final results: unit exit 0, 160/160 passed; acceptance exit 0, 2/2 passed; no
skips. Separate discovery found the same two source-declared acceptance cases.
The execution verifier reconciled each fresh TRX against independent expected
names (80 per unit module, 2 for acceptance); emitted JUnit files also parse with
the corresponding passed counts.

The isolated comparison used the same build/test commands, filters, assertions,
SDK, and configuration, with separate outputs and packages. Unit exit was 2
(154 passed, 6 intended failures); acceptance exit was 2 (1 passed, 1 intended
failure). Production files in that worktree had no diff from the baseline.

The older-host check ran `C:\Program Files\dotnet\dotnet.exe msbuild
Generation.proj -nologo -t:Build -m:2 -nr:false -v:normal -bl:legacy-host.binlog`
from `artifacts\mt-validation\legacy-msbuild`, whose `global.json` selects
installed SDK `10.0.114`. Exit 0; expected generated source and merged JSON were
checked after execution.

### Limits and cleanup

The full-solution command
`.\eng\build.ps1 -restore -build -pack -msbuildEngine dotnet -bl -properties
'/p:SkipUapTargetFramework=true'` compiles successfully but fails its final
Windows package verifier: excluding UAP yields 64 versus 66 expected files for
`MSTest.TestAdapter`, and 88 versus 105 for `MSTest.TestFramework`. This unrelated
package-count limitation is not suppressed or fixed here. The focused task
package packs successfully and is the package exercised above.

The normal Windows Release pipeline selects the full solution through
`eng\pipelines\steps\test-windows-configuration-tests.yml`, under the existing
`SkipTests`/documentation-only gates. The new cases have no category or environment
opt-in gate. CI, desktop MSBuild, and non-Windows runs remain unverified; this
change does not claim their execution. Interactive debugger attachment was not
launched during unattended validation.

The isolated comparison worktree, its borrowed-SDK junction, and its own build
host processes were removed after retaining evidence. SDK first-run markers
created by the older-host probe were removed; existing certificates and shared
caches were left unchanged. Both test assemblies retain method-level
parallelization with CPU-count workers. New tests own their assets and use only
child-process environment overrides, so no additional resource locks or
serialization were needed.

No design questions remain for this migration. Release-branch servicing,
downstream rollout, and publication are outside the requested work.
