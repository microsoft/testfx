# Preloading referenced assemblies before test execution

## Problem and contract

A dependency loaded for the first time inside a test contributes to that test's wall-clock time.
An assembly initializer is too late for a host that starts its clock at the test-start notification.
MSTest can instead opt in to loading dependencies **before reporting the first test start** for each
source. This moves load work, not method JIT or test execution. It neither changes timeout accounting
nor guarantees improved per-test or total run time.

The option is `PreloadReferencedAssemblies` under `RunSettings/MSTest` or
`mstest.execution.preloadReferencedAssemblies` in `testconfig.json`. The default is `false`.
See the [configuration examples](../testconfig.schema.md#preloading-referenced-assemblies).

When enabled, the execution-domain `UnitTestRunner` walks the test assembly's direct and transitive
references using the same exclusions as [fixture-provider discovery](assembly-fixture-provider-discovery.md).
On .NET it resolves each reference by name through the referring assembly's load context; on .NET
Framework it uses name-based binding in the execution app domain. It walks each resolved assembly
instance only once. A run without tests matching the host filter skips the walk; filters evaluated
later by the runner can still exclude tests. The walk checks for cancellation between loads.
Unresolvable references are traced and counted, not turned into test failures. The informational
summary counts successfully resolved assemblies, including those already loaded, not just new loads.
Classic UWP and .NET runtimes without dynamic code support skip the walk with a warning.

## Research and alternatives

- [#12291](https://github.com/microsoft/testfx/pull/12291) made fixture-provider discovery prefer
  metadata over loading assemblies. It avoided loading and locking otherwise-unused files. In a local
  comparison with coverage enabled, reverting just that change brought first-test times back toward
  the previous version but increased total process time. Restoring eager loading by default would
  undo the original benefit.
- Loading libraries recursively in `[AssemblyInitialize]` also moved work out of measured test bodies
  locally, but did not move it before the host's test-start event. Warming up a different process
  does not populate the execution process's loaded assemblies. Neither experiment establishes when
  an external harness starts its clock.
- Design reviews favored one explicit switch over coverage detection or a list of DLL patterns.
  `Microsoft.Extensions.*` dependencies are included when the reference graph reaches them. Scanning
  for unreferenced DLLs would introduce a different identity and binding contract; defer it unless
  there is a demonstrated need for deployed plugins not represented by references.
- The new setting belongs on the feature release train, not in a servicing change. There is no new
  discovery mode, shared cache, or public managed API. Enabling it can cost startup time and lock
  libraries the tests never use.

## Validation and demonstration

On Windows the packed `MSTest.TestAdapter` consumer runs one test, `ObservesPreloadState`. The
test reads the names of loaded assemblies without using either sample library; the direct reference
and its transitive leaf occur only in methods that are never called. The parent asserts the child
exit, TRX identity and outcome, and compares the loaded adapter DLL against the `.nupkg` by SHA-256.
This is a loader-state change; those observations are more useful than a screenshot.

| Contract | Evidence |
|---|---|
| Default and opt-in, both configuration channels | `AssemblyPreloadTests.ReferencedAssembliesAreLoadedBeforeTheFirstTestOnlyWhenRequested`: MTP (`testconfig.json`) and VSTest (`.runsettings`), each with the setting omitted and enabled. |
| .NET Framework execution-domain binding | The same test runs on `net472`, including VSTest with app domains disabled and enabled. In the isolation-domain pair, disable referenced fixture-provider discovery in both runs: that independent feature can load the sample libraries itself. |
| Before the first test-start event | `TestExecutionManagerSourceExecutionMutationTests.RunTests_PreloadEnabled_PreloadsReferencedAssembliesBeforeTheFirstTestStarts` checks the completed preload at the recorder's start callback. |
| Distinct bindings, sharing, cycles, failure, cancellation | `AssemblyPreloaderTests` includes a diamond whose two referrers bind the same name to different libraries. Skipping a name globally lost the second library and its leaf (`Expected loaded to be 5, but found 3.`). |
| Settings parsing and copying | `MSTestSettingsTests` and `MSTestRunSettingsContinuationTests` cover both settings channels, the default, invalid input and copying into the execution domain. |
| Trimmed consumers | The existing `TrimTests.Publish_WithTestAdapter_DoesNotSurfaceWarningsFromSuppressedSources` check covers `AssemblyPreloader.cs`. The walk needs only references that survived trimming. Before the method-level suppression, both `net10.0` and `net11.0` consumers reported IL2026 on `Assembly.GetReferencedAssemblies()`. |

Windows results for implementation `8b7e63da3d70e8b5a07faa5bfe74d9332d74abca`, using the pinned
SDK `12.0.100-alpha.1.26570.125` and the Debug `MSTest.TestAdapter.5.0.0-dev` package:

| Comparison | Result |
|---|---|
| Feature build | `.\build.cmd -pack -c Debug -bl` exited 0. All ten packed cases passed (exit 0). In all five opt-in cases both sample libraries were present; in all five default cases both were absent. VSTest reported `Preloaded 2 referenced assemblies before running tests. 0 could not be loaded.` for the opt-in sample. |
| Same cases, preload call removed | Kept the configuration and tests, omitted only the guarded `testRunner.PreloadAssemblies(...)` call from the production code, and repacked the adapter in isolation. All five opt-in cases failed at the direct-library assertion (expected `"True"`, actual `"False"`); all five default controls passed (exit 2). The ten child tests still passed and the loaded adapter digest matched its respective package in every case. |
| Review fixes | The two trimmed publishes that had reported IL2026 now passed on `net10.0` and `net11.0`. Four focused adapter unit classes passed on `net10.0` (127) and `net462` (126), including the cross-referrer binding and pre-start ordering checks. |

To reproduce the paired comparison, run the same built `net12.0` acceptance host from each
checkout, so it resolves that checkout's packages. Use separate results directories:

```powershell
.\artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net12.0\MSTest.Acceptance.IntegrationTests.exe --filter "FullyQualifiedName~AssemblyPreloadTests" --ansi off --results-directory $results --report-trx --report-trx-filename run.trx
```

## Limits

The graph does not cover reflection-only or filename-based plugin loads. Discovery, a profiler, or
the host may already have loaded some dependencies. In particular, .NET Framework fixture-provider
discovery can fall back from metadata to runtime binding under app-domain binding policy. A positive
summary in such a run does not establish that the option caused a new load. No hosted deadline,
cross-platform performance claim, or JIT/instrumentation cost reduction was validated.
