# Assembly-level OS conditions: research and decisions

## Problem and contract

SDK-generated assembly-level `SupportedOSPlatform` metadata describes platform
compatibility but does not stop MSTest from executing tests. Repeating the same
`OSCondition` on every test class is unnecessary when an entire assembly has the
same restriction. Support:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: OSCondition(OperatingSystems.Windows)]
```

An assembly guard constrains every test in that assembly. Conditions at assembly,
class, and method scopes compose with AND; a narrower scope cannot re-enable an
excluded test. Within each scope, preserve the existing OR-within-group and
AND-between-groups behavior, include/exclude modes, and custom ignore messages.
An excluded assembly reports its selected tests as skipped with the assembly
condition's reason, before assembly/class/test initialization or test construction.
Cleanup must not run for scopes that were never initialized.

The framework, `MSTest.TestAdapter`, and `MSTest.Analyzers` must ship together with
this support. Both MTP-hosted MSTest and VSTest use the adapter. Rooting and
reflection-free source generation must observe the same guard. Existing assemblies
without an assembly condition retain their behavior. Only `OSConditionAttribute`
gains an assembly target; other built-in conditions are outside this feature.

This work targets the current `main` development line (MSTest 5.0 / MTP 3.0),
not the PowerToys 4.5.2 upgrade. An older adapter does not enforce the new assembly
guard, even if a newer framework lets it compile. Do not adopt it downstream until
compatible framework/adapter packages are available. Backports to 4.x require a
separate servicing decision and validation; none is implied here.

There is no new handshake, process protocol, fallback, cancellation contract, or
exit code. Existing discovery, scheduling, reporting, and cancellation remain in
place; only the runnable-test decision changes. Test results remain individual
results rather than a synthetic assembly result.

## Research and experiments

### 2026-11-11: current-source inspection

Starting revision: `e84b5554dd59e80d27c6480ef63284b5d34cc2a9`.

- `OSConditionAttribute` currently targets classes and methods. Its constructors
  already supply mode-specific reasons, and its OS-detection implementations
  cover the supported framework variants.
- `AttributeExtensions.IsIgnored` already evaluates an arbitrary attribute
  provider using the established grouping rules. Reusing it for the test
  assembly avoids a second evaluation algorithm.
- `UnitTestRunner.IsTestMethodRunnable` evaluates class and method conditions
  before running initialization. Normal cleanup paths guard against uninitialized
  scopes. Further tracing found that graceful-stop `ClassCleanupManager.ForceCleanup`
  omitted that check for cached assemblies: add the same initialization guard
  there, so a stopped run cannot clean up an excluded, uninitialized assembly.
- `MSTestReflectionMetadataGenerator` already materializes assembly attributes;
  `RuntimeRegistrationEmitter` registers them, and
  `SourceGeneratedReflectionOperations` serves them through the same reflection
  abstraction. No separate source-generated condition evaluator is necessary.
- MSTEST0084 already intersects platform metadata across containing scopes, but
  compares only class/method runtime guards. Include the assembly runtime guard
  in that comparison; do not suppress a mismatch merely because a guard exists.
  Preserve the conservative policy against member-local fixes when a containing
  guard can prevent the proposed fix from satisfying the contract.
- Public API baselines record the existing constructors and properties, not
  `AttributeUsage` targets. This feature requires no new public signature or
  baseline entry; preserve shipped API entries and verify the existing gates.
- The initial `build.cmd` invocation could not resolve `powershell` from its
  shell environment. `eng\build.ps1` under PowerShell 7.6.9 bootstrapped the
  pinned SDK, but nested runtime-install wrappers hit the same shell issue.
  Setting the Windows PowerShell directory explicitly inside a task-local
  `cmd /d /c` invocation fixed the launcher without changing repository scripts
  or global settings.
- The framework build then passed with zero warnings/errors for its declared
  targets using SDK `12.0.100-alpha.1.26556.115`. This checked the widened
  attribute contract, not execution behavior. A subsequent wildcard
  multi-project pack failed before compilation with MSB1006 (semicolon-expanded
  `Projects` property); use a task-owned source-only solution for packing
  instead of changing unrelated build infrastructure.
- The source-only solution initially lacked ancestor `global.json` SDK pins
  because it lives in session storage. Copying the repository's unchanged
  `global.json` and `NuGet.config` beside it resolved SDK/feed discovery.
  The resulting Release build/pack passed with zero warnings/errors and emitted
  `MSTest.*` packages at `5.0.0-dev` and MTP packages at `3.0.0-dev`.
- Prepared an outside-repository comparison worktree at the same starting
  revision. Its compiling counterfactual keeps the assembly-target API and test
  setup, but removes assembly rejection, removes the graceful-stop initialization
  guard, passes no assembly guard into MSTEST0084's equivalence check, and removes
  the fixer's defensive assembly check.
  Production and comparison use separate output/feed directories and acceptance
  package caches; only the pinned toolchain is reused.
- The counterfactual Release source build/pack passed with zero warnings/errors.
  Outer analyzer-test restore then found an existing feed gap for
  `Microsoft.CodeAnalysis.VisualBasic.Analyzer.Testing` 1.1.4. The exact pinned
  version exists on nuget.org; a temporary configuration maps only that package
  to the official source. No dependency versions or repository feeds were changed.
- The first adapter-test harness called forced cleanup after normal cleanup had
  already completed, producing an unrelated duplicate cleanup. Restrict that
  harness call to rejected/uninitialized scopes; retain the exact normal
  eight-event lifecycle assertion and the existing initialized forced-cleanup
  control. Final focused execution passed all 43 planned cases. Earlier harness
  compilation/style failures and that invalid setup are not behavioral red proof.

## Alternatives and decision

1. Keep class-level guards: correct today, but repeats an assembly-wide policy
   and leaves the reported analyzer friction unresolved.
2. Widen `AttributeUsage` only: compiles but does not enforce the restriction,
   allowing unsupported tests and initialization to run. Rejected.
3. Add a separate assembly scheduler or generator-specific evaluator: duplicates
   condition semantics and risks dropping test identities or lifecycle accounting.
   Rejected.
4. Evaluate the assembly first through the shared runnable-test decision, then
   retain existing class/method evaluation. Chosen: minimal implementation,
   shared across execution paths, with existing skip and cleanup bookkeeping and
   an initialization guard in graceful-stop assembly cleanup.

Assembly skip reasons take precedence when the assembly excludes a test; do not
evaluate narrower conditions to override that decision. Preserve existing
class-versus-method reason selection when the assembly permits execution.

## Validation and demonstration

### Executed consumer matrix

Windows x64 host, Release, SDK `12.0.100-alpha.1.26556.115`; the primary unit selections
run on `net11.0`, the outer acceptance host on `net12.0`, and its real packaged
consumers on `net11.0` / runtime `11.0.0-rc.3.26522.148`.

All five affected test projects built across their declared target frameworks.
The framework and complete shipping source graph also built and packed
successfully. Existing analyzer-test dependency audit warnings remain unchanged;
there were no feature compiler/style errors or baseline changes.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Include/exclude, matching/nonmatching, absent guard, scope intersection, grouping, and reasons | `Mtp_AssemblyOSCondition_PreservesResultsReasonsAndLifecycle`: five guard configurations in each of Reflection, Rooting, and ReflectionFree, **15/15 passed**. | Actual terminal inspection described below. |
| Lifecycle and result identity | The same cases assert ten exact class/method/display-name/outcome/reason tuples, distinct IDs, two distinguishable data rows, no rejected-scope markers, and exactly 34 positive-control lifecycle/body markers. | Reasons stay associated with the right test names. |
| VSTest adapter | `Vstest_AssemblyOSCondition_PreservesResultsReasonsAndLifecycle`: matching and rejecting guards, **2/2 passed**, using the packed adapter and `dotnet vstest`. | IDE Test Explorer remains unverified locally. |
| Shared runtime and graceful stop | Four `AssemblyOSCondition_*` cases, `ForceCleanupSkipsCachedAssembliesThatNeverInitialized`, initialized cleanup and existing same-/different-group controls: **8/8 passed**. | Nonvisual lifecycle contract. |
| Generated metadata and attribute policy | `Generator_MaterializesAssemblyOSConditionWithoutMergingNarrowerScopes`, two modes, plus two framework attribute-policy cases: **4/4 passed**. | Compiles/materializes the real framework attribute, not a permissive stub. |
| MSTEST0084 and fix safety | Matching/mismatching guards, narrowing, SDK-style `windows10.0.17763.0` assembly metadata, unknown-family semantics, stale fix properties, and existing unguarded fix control: **14/14 passed**. | No diagnostic wording or code-action UI redesign. |

Primary total: **43 selected/executed/passed, zero failed/skipped outer cases**.
The 17 acceptance cases assert **170 child results: 70 passed, 100 intentionally
skipped**. Intentional child skips are not counted as outer execution proof.
Independent source-based plans and discovery matched the executed name multisets;
fresh TRX, JUnit, and CTRF reports were reconciled.

The same focused unit selections also passed on every remaining declared runtime
target, using the already-built, hash-matched outputs (`--no-build`). Each target
had independent discovery, a fresh execution report and the canonical TRX gate;
no source, assertion or package changes were needed.

| Module | Additional targets | Passed |
| --- | --- | --- |
| Adapter | `net462`, `net48`, `net10.0`, `net10.0-windows10.0.18362.0` | 32 |
| Framework | `net48`, `net10.0`, `net10.0-windows10.0.18362.0` | 6 |
| Generator | `net10.0` | 2 |
| Analyzers | `net472`, `net10.0` | 28 |

These are **68 additional passes** across ten runtime targets, for **111 total
green outer executions, zero failures/skips**. .NET Framework hosts ran x86;
modern .NET hosts ran x64. The Windows-specific rows are unit-host execution,
not a claim of packaged WinUI/UWP activation testing.

### Commands and provenance

Build/pack used `build.cmd -projects <task-owned source-only solution> -c Release
-pack -bl -v quiet` (exit 0). That solution contains the repository source
projects, not replacement components. Test-project builds used the bootstrapped
`.\.dotnet\dotnet.exe build <project> -c Release --no-incremental -bl:<log>
--verbosity minimal` (all exit 0).

Focused execution used `.\.dotnet\dotnet.exe test --project <project> -c Release
--framework <TFM> -p:UsingDotNetTest=true --show-test-results all`, with unique
binary-log/results paths and these selectors. The exact resolved argument vectors,
timestamps, native exits, package hashes, and report files are retained in the
session's per-run `command.json` and provenance artifacts.

| Project | TFM | Selector | Green exit / cases | Counterfactual exit / cases |
| --- | --- | --- | --- | --- |
| `MSTestAdapter.PlatformServices.UnitTests` | `net11.0` | `--treenode-filter "/*/*/*/(AssemblyOSCondition_*)\|(ForceCleanupSkipsCachedAssembliesThatNeverInitialized)\|(ForceCleanupFlowsClassAndAssemblyPropertiesWithoutLeakingClassScope)\|(IsIgnored_SameGroupOneSatisfied_ReturnsFalse)\|(IsIgnored_DifferentGroupsOneUnsatisfied_ReturnsTrue)"` | 0 / 8 passed | 2 / 4 failed, 4 passed |
| `TestFramework.UnitTests` | `net11.0` | `--treenode-filter "/*/*/OSConditionAttributeTests/*"` | 0 / 2 passed | 0 / 2 passed |
| `MSTest.SourceGeneration.UnitTests` | `net11.0` | `--filter "FullyQualifiedName~Generator_MaterializesAssemblyOSConditionWithoutMergingNarrowerScopes"` | 0 / 2 passed | 0 / 2 passed |
| `MSTest.Analyzers.UnitTests` | `net11.0` | `--filter "FullyQualifiedName~WhenAssemblyOSCondition\|FullyQualifiedName~WhenAssemblyCompatibilityAndAssemblyGuard\|FullyQualifiedName~WhenAssemblyAndNarrowerOSConditions\|FullyQualifiedName~WhenDiagnosticCarriesFixPropertiesUnderAssemblyGuard\|FullyQualifiedName~WhenAssemblyOnlyRestrictsTestClass_AddsEffectiveCondition"` | 0 / 14 passed | 2 / 9 failed, 5 passed |
| `MSTest.Acceptance.IntegrationTests` | `net12.0` | `--filter "FullyQualifiedName~MSTest.Acceptance.IntegrationTests.AssemblyOSConditionTests"` | 0 / 17 passed | 2 / 7 failed, 10 passed |

Table backslashes before `|` escape Markdown, not command-line characters.
Analyzer evaluation additionally uses the temporary `RestoreConfigFile` described
above. Acceptance runs set `MSTEST_ACCEPTANCE_SKIP_SOURCEGEN=0` and use distinct,
marked, parent-owned green/red `TESTFX_COLLECTION_NUGET_CACHE` directories.

Each generated consumer resolved `MSTest.TestFramework` and `MSTest.TestAdapter`
`5.0.0-dev`; generated variants also resolved `MSTest.SourceGeneration`
`5.0.0-dev`. MTP/TRX packages are `3.0.0-dev`. Effective `project.assets.json`
package folders, cached nupkgs, physical loaded DLL paths and SHA-256 payloads
were checked against each variant's own current shipping packages. No version
string or successful outer exit was used as a substitute for loaded-bit proof.

### Test-value evidence

Both source states start at
`e84b5554dd59e80d27c6480ef63284b5d34cc2a9`. The saved patches include all changed
production/test sources and the new acceptance test, with identical test bytes:

- Green code/test patch SHA-256:
  `0140D3E80032FE8B67726E368569F6C2D195FCDD5BDDFED2050E6F4201C6FDDA`.
- Counterfactual code/test patch SHA-256:
  `C43EE1067009EC2008295BB2283C6481DA13779030387D4DB88E47EF2A910BDE`.

This is a **compiling behavior-removal comparison**, not a pristine old-API
baseline. Both states were rebuilt/repacked separately. The counterfactual
retains assembly-target compilation and generated metadata, but removes the four
behavior checks identified above. All 43 unchanged tests execute in both states;
the counterfactual has **20 intended assertion failures, 23 passes, zero skips**.
This is not a mutation score or a claim that 20 independent faults were tested.

| Contract / exact case | Without behavior | Correct implementation |
| --- | --- | --- |
| Assembly restriction: `AssemblyOSCondition_FailedAssemblyCannotBeReenabledByNarrowerScopes` | Executed: detected `Passed` instead of `Ignored`. | Executed: passed. |
| Cleanup: `ForceCleanupSkipsCachedAssembliesThatNeverInitialized` | Executed: detected an unexpected assembly-cleanup event, not a setup exception. | Executed: passed; initialized cleanup control also passed. |
| Analyzer: `WhenAssemblyCompatibilityAndAssemblyGuardConstrainMembers_NoDiagnostic` | Both unversioned/versioned cases execute and detect unexpected MSTEST0084 diagnostics. | Both execute and pass. |
| Fixer: `WhenDiagnosticCarriesFixPropertiesUnderAssemblyGuard_FixerDoesNotChangeMembers` | Class/method cases detect an unsafe offered action. | Both execute and pass. |
| Packaged runner: both `*PreservesResultsReasonsAndLifecycle` methods | Seven outer assertions detect incorrectly executed excluded assemblies in Reflection, Rooting, ReflectionFree, and VSTest. All 17 real consumers exit 0 but yield the wrong aggregate **119 passed / 51 skipped**, proving why exits alone are insufficient. | All 17 outer cases pass with the required **70 passed / 100 skipped** child partition. |

The correct worktree was never mutated for the comparison; its final production
and test hashes still match the passing records. Framework API/metadata and
generator controls pass in both states, confirming that the red phase is not an
attribute-compilation, restore, or launch failure.

Reports, command records, source patches, loaded-bit provenance, counterfactual
producer nupkgs, and the source-only comparison checkout are retained as session
evidence. The 34 owned green consumer/result directories, both marked acceptance
caches, counterfactual generated build/package directories, and the comparison's
borrowed-SDK junction were removed after capture. The actual SDK was preserved.

### Rendered demonstration and limits

Inspected the actual packed ReflectionFree consumer in a live **80 x 24**
terminal, with ANSI off and on. A rejected assembly shows ten named skipped
results, the assembly reason on each result, and exit 0; the matching assembly
shows seven passes and three correctly associated method/class skip reasons.
Names and reasons remain readable without clipping/overlap. Retained real
terminal transcripts are preferable to a staged screenshot for this non-layout
change. The terminal surface has no exposed resize action; additional widths
were not claimed.

The source-generated modes above execute generated metadata under the JIT;
no NativeAOT publish was performed. IDE rendering and Linux/macOS execution are
not established by Windows process tests. `TestFx.slnx` includes the affected
projects; Windows Release's full-test template selects that solution when
`SkipTests=false` and the product-change gate permits it. The tests add no
category/environment opt-in. This is static CI wiring evidence, **not an
executed CI result**. No GitHub publication or PowerToys adoption was performed.

## Open questions

- Release owner must decide the first compatible published framework/adapter/
  analyzer versions and any 4.x servicing/backport before PowerToys adoption.
- Local runtime coverage is complete for the declared unit targets; external
  OS/IDE, NativeAOT publishing and CI execution limits remain as stated above.
