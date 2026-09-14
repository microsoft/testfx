# Mutation-testing remediation plan

**Created:** 2026-09-14  
**Source report:** [2026-09-14 Stryker.NET report](../reports/2026-09-14-stryker-report.md)

## Current baseline

Twelve of the 28 configured production projects produced JSON reports:

- 28,293 total mutants
- 7,017 killed
- 589 survived
- 8,721 without coverage
- 424 timed out
- 1,365 runtime errors
- 5,291 compile errors
- 4,879 ignored
- 44.42% aggregate score using `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`

The remaining 16 projects did not produce a JSON report. Most either encountered Stryker.NET's current Microsoft.Testing.Platform test-discovery limitation or stalled during mutant execution. `TestFramework` additionally exhausted local memory while running two mutation workers.

## Phase 1: Establish a trustworthy, repeatable baseline

**Priority: Critical**

1. Land the weekly per-project CI matrix so one failed or hung target cannot suppress every other report.
2. Run the workflow manually and classify every project as completed, no-compatible-tests, timed out, build failure, or infrastructure failure.
3. Preserve each project's JSON, HTML, and console log as a separate artifact.
4. Compare the Linux CI results with this Windows snapshot. Do not treat a project score as authoritative when its run has runtime errors or when Stryker associated unrelated test assemblies with the target.
5. Investigate the Microsoft.Testing.Platform zero-test failures and report a minimal reproduction upstream if the behavior remains on Stryker.NET 5.0.0.
6. Investigate the projects that enter mutant execution but do not finish: CrashDump, CtrfReport, JUnitReport, TrxReport, Microsoft.Testing.Platform.MSBuild, and TestFramework.

**Exit criteria:** every configured project either produces a stable JSON report or has a documented, tracked tooling limitation with a reproducible command.

## Phase 2: Eliminate high-confidence surviving mutants

**Priority: High**

Start with projects that produced genuine `Survived` statuses and have focused unit-test projects:

1. `MSTestAdapter.PlatformServices` — 481 survived.
2. `MSTest.SourceGeneration` — 108 survived.

Prioritize these files first:

| Priority | Project | File | Survived | NoCoverage |
| ---: | --- | --- | ---: | ---: |
| 1 | MSTestAdapter.PlatformServices | `src/Platform/Microsoft.Testing.Extensions.TrxReport/Hashing/XxHashShared.cs` | 59 | 176 |
| 2 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.Lifecycle.cs` | 44 | 13 |
| 3 | MSTestAdapter.PlatformServices | `src/Platform/Microsoft.Testing.Extensions.TrxReport/Hashing/XxHash128.cs` | 33 | 2 |
| 4 | MSTest.SourceGeneration | `src/Analyzers/MSTest.SourceGeneration/Generators/MetadataRegistryEmitter.cs` | 32 | 13 |
| 5 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestClassInfo.Initializer.cs` | 28 | 21 |
| 6 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Services/MSTestAdapterSettings.cs` | 23 | 22 |
| 7 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestAssemblyInfo.cs` | 22 | 4 |
| 8 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.Execution.cs` | 20 | 35 |
| 9 | MSTest.SourceGeneration | `src/Analyzers/MSTest.SourceGeneration/Generators/DynamicDataSourceBuilder.cs` | 18 | 2 |
| 10 | MSTestAdapter.PlatformServices | `src/Adapter/MSTestAdapter.PlatformServices/Execution/AsyncReaderWriterLock.cs` | 17 | 2 |

For each candidate:

1. Confirm that the mutation is behaviorally meaningful rather than equivalent.
2. Add one focused regression test that distinguishes the original and mutated behavior.
3. Run the smallest relevant unit-test project.
4. Re-run Stryker for only the production project and source file.
5. Record the stable mutant fingerprint and verification outcome in the next dated report.

**Exit criteria:** the listed high-priority survivors are killed or documented as equivalent.

## Phase 3: Close meaningful NoCoverage gaps

**Priority: High**

Do not attack raw counts blindly. First confirm that the reported test assembly genuinely exercises the production project. Then prioritize:

1. Public behavior and protocol handling.
2. Error handling and recovery paths.
3. Boundary conditions and state transitions.
4. Serialization, source generation, and compatibility logic.
5. Internal helpers only when their behavior cannot be covered through a higher-level API.

Largest current project gaps:

| Project | NoCoverage |
| --- | ---: |
| Microsoft.Testing.Extensions.AzureDevOpsReport | 2,853 |
| MSTestAdapter.PlatformServices | 2,583 |
| Microsoft.Testing.Extensions.Retry | 1,316 |
| Microsoft.Testing.Extensions.HangDump | 1,006 |
| MSTest.Analyzers | 352 |
| Microsoft.Testing.Extensions.OpenTelemetry | 217 |
| MSTest.TestAdapter | 162 |
| MSTest.Analyzers.CodeFixes | 132 |

**Exit criteria:** each project has a reviewed shortlist of meaningful uncovered behavior and tests for its highest-risk items.

## Phase 4: Reduce invalid and indeterminate results

**Priority: Medium**

1. Analyze the 1,365 `RuntimeError` mutants by common crash signature and test host.
2. Analyze the 424 `Timeout` mutants separately; do not classify them as surviving.
3. Group the 5,291 compile errors by diagnostic. The dominant safe-mode pattern is `CS0165` after statement removal; experimental API diagnostics also caused mutation compilation failures.
4. Tune per-project concurrency and timeout only after identifying whether the bottleneck is test-host startup, memory growth, or a specific mutant.
5. Keep invalid statuses out of the mutation-score denominator and show them explicitly in CI summaries.

**Exit criteria:** runtime errors and timeouts are low enough that score changes represent test-quality changes rather than infrastructure noise.

## Phase 5: Operational cadence

**Priority: Medium**

1. Run the complete project matrix weekly.
2. Retain per-project artifacts for 30 days.
3. Let the mutation-test improver aggregate successful project reports even when other matrix entries fail.
4. Compare trends only between equivalent project sets; call out missing reports.
5. Check in a new dated Markdown snapshot when the baseline materially changes or before a focused remediation campaign.
