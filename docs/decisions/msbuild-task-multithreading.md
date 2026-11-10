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
