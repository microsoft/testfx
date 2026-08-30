# Test Improver Notes — microsoft/testfx

## Build/Test Commands (Validated from docs — confirmed working 2026-08-05)

- **Build (Debug)**: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- **Build (Release)**: `./build.sh -c Release`
- **Restore SDK**: `./build.sh --restore` (installs .dotnet/ SDK + runtimes) — CONFIRMED working in sandbox 2026-08-05 (SDK 11 preview restored successfully via `./build.sh -restore`, ~2 min).
- **Unit Tests**: `./build.sh -test`
- **Pack NuGets**: `./build.sh -pack`
- **Integration Tests**: `./build.sh -pack -test -integrationTest`
- **Build single project**: `export PATH="$PWD/.dotnet:$PATH" && dotnet build test/UnitTests/<Project>/<Project>.csproj -c Debug`
- **Run all tests in project directly (fast, avoids MTP CLI filter quirks)**: run the built dll directly, e.g. `./artifacts/bin/MSTest.Analyzers.UnitTests/Debug/net8.0/MSTest.Analyzers.UnitTests` (no args) — runs full suite in ~50s for MSTest.Analyzers.UnitTests (1718 tests).
- **`--treenode-filter` gotcha**: `dotnet run --project ... -- --treenode-filter "..."` frequently fails to match (prints help instead) for MSTest.Analyzers.UnitTests — don't fight the filter syntax, just run the whole (fast) suite directly via the built binary instead.
- **NOTE**: Repo requires dotnet SDK 11 preview. `./build.sh -restore` DOES work in this sandbox (verified 2026-08-05) — earlier notes claiming "not available" were wrong. Always try restore before assuming no-build.

## Testing Frameworks & Patterns

- MTP + MSTest Analyzer unit tests → use **MSTest** (`Assert`/`StringAssert`/`CollectionAssert`)
- Adapter unit tests (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) → use **AwesomeAssertions** (FluentAssertions-style)
- MSTest itself (`TestFramework.UnitTests`) → use **AwesomeAssertions** in partial class `AssertTests : TestContainer` (TestContainer framework)
- Each project has `BannedSymbols.txt` listing disallowed assertion APIs
- **No VB.NET tests** for analyzers — repo constraint, maintainers not interested
- Various test pattern notes (see below)

## Key Test Pattern Notes

- **IgnoreAttribute is sealed** — cannot derive from it in test scenarios
- **sealed + inheritance in tests**: first level class must NOT be sealed for multi-level inheritance tests
- **`[Experimental("MSTESTEXP")]` types**: don't inherit from `RetryBaseAttribute` in test strings; use `[Retry]` directly
- **Static classes in Roslyn**: NOT abstract (`IsAbstract=false`); `IsStatic=true`
- **Nullable annotation (CS8632)**: avoid `object?` in analyzer test code strings unless `#nullable enable` is added
- **ManagedMethod/ManagedType**: dead code in TestContextPropertyUsageAnalyzer restriction sets (properties don't exist on TestContext)
- **VerifyCodeFixAsync for "no fix" case**: `VerifyCodeFixAsync(code, diagnostic, code)` (same string for source and fixed-source) is valid when no fix is registered
- **OperationAnalysisContext.ContainingSymbol for lambdas**: resolves to enclosing named method, not the lambda
- **Discard variable name clash**: don't use `_` as param name if test code also uses `_ = expr`
- **`Assert.AreSame(null, null)` is a compile error**: use `(object)null` or typed variable
- **AvoidAssertAreSameWithValueTypes fires for struct-constrained T**: generic `where T : struct` has `IsValueType == true`
- **`[TestClass]` on structs**: CS0592 — only valid on classes
- **CultureMutation / CurrentDirectory / UndeclaredProcessGlobalStateMutation parallel-safety analyzers** (MSTEST0074/0075/0076) all share `ParallelSafetyHelper`, producing 3 fixture branches each:
  1. `[TestInitialize]`/`[ClassInitialize]` (class-scoped) → diagnostic + fix at class scope
  2. `[AssemblyInitialize]`/`[AssemblyCleanup]` → NO diagnostic (serialized behind semaphore, no race possible)
  3. `[GlobalTestInitialize]`/`[GlobalTestCleanup]` (global fixture) → diagnostic fires but NO fix offered (`GetResourceLockFixScope` returns null — global fixture has no effective single-class lock target)
  - As of 2026-08-05 all three analyzers' test files now cover all 3 branches (MSTEST0075 covered since PR #10383; MSTEST0074 and MSTEST0076 gap filled 2026-08-05).

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.
3. **DependsOnShouldBeValidAnalyzer / TestFilterProviderShouldBeValidAnalyzer (MSTEST0078/0081)** — internal-target-class gap closed (2026-08-07); accessibility (type vs constructor) gap closed (2026-08-08). Remaining: another pass for any leftover branch gaps.
4. **Analyzer edge cases (ongoing)** — Continue systematic coverage of untested paths in MSTest.Analyzers. MSTEST0074/0075/0076 fixture-branch gaps all closed.
5. **Issue #10316 — CLOSED 2026-08-17.** Confirmed complete (3rd independent verification, after 2026-08-10 and 2026-08-16 comments already on the issue) — all File.* allowlist entries are covered by existing DataRow tests in `SharedFileSystemPathInTestAnalyzerTests.cs`. Do not resurface.
6. **`ReportFileWriterHelper` (SharedExtensionHelpers) — DONE 2026-08-13**: added direct unit tests for `RetryWhenIOExceptionAsync` (immediate success, retry-then-success, non-IOException passthrough, rethrow after timeout). No more zero-coverage helpers found in `SharedExtensionHelpers/` for now — `TrxReportGeneratorCommandLine`/`ReportFileNameValidator`/`ReportFileWriterHelper` all now covered directly. `TargetFrameworkMonikerHelper.cs` still untested but is a thin one-liner wrapper (low value).

## Tasks Run History (summarized)

| Date | Tasks |
|------|-------|
| 2026-08-17 | Task 2 (broad Assert/CollectionAssert/StringAssert audit — no new zero-coverage gaps found; area is saturated), Task 5 (closed issue #10316 as confirmed-complete, 3rd verification), Task 7. No new PR this run — did not find a genuine, undertested, non-trivial gap after build validated. |
| 2026-08-13 | Task 3 (ReportFileWriterHelper.RetryWhenIOExceptionAsync unit tests, SharedExtensionHelpers), corrected #10316 status (still open, not closed as previously logged), Task 7. |
| 2026-08-10 | Task 2/5 (verified & closed issue #10316 — all File.* allowlist entries confirmed covered), Task 7. No new PR this run (DependsOnShouldBeValidAnalyzer checked, already has broad rule coverage). |
| 2026-08-09 | Task 3 (SharedFileSystemPathInTestAnalyzer MSTEST0077: File.CreateSymbolicLink pathToTarget negative, per issue #10316), Task 7 |
| 2026-08-08 | Task 3 (TestFilterProviderShouldBeValidAnalyzer MSTEST0081: filter-type vs constructor accessibility edge cases), Task 7 |
| 2026-08-07 | Task 3 (DependsOnShouldBeValidAnalyzer MSTEST0078: internal target class HasValidAccessibility edge cases), Task 7 |
| 2026-08-06 | Task 3 (MSTEST0077 SharedFileSystemPathInTestAnalyzer: AssemblyInitialize no-diagnostic + GlobalTestInitialize diagnostic edge cases), Task 7 |
| 2026-08-05 | Task 3 (MSTEST0074 + MSTEST0076: GlobalTestInitialize "diagnostic without fix" branch tests), Task 7 |
| 2026-08-02 | Task 3 (MSTEST0054 + MSTEST0044 edge-case tests), Task 7 |
| 2026-07-31 | Task 3 (CurrentDirectoryMutationUnderParallelizationAnalyzer: TestInitialize+AssemblyInitialize fixture edge cases), Task 7 |
| 2026-07-29 | Task 3 (CultureMutationUnderParallelizationAnalyzer MSTEST0076: 2 edge-case tests), Task 7 |
| 2026-07-28 | Task 3 (UnusedParameterSuppressor MSTEST0047: 2 edge-case tests), Task 7 |
| 2026-07-25 | Task 3 (DoNotStoreStaticTestContextAnalyzer: 2 edge-case tests), Task 7 |
| 2026-07-18 | Task 3 (NonNullableReferenceNotInitializedSuppressor: 2 edge-case tests), Task 7 |
| 2026-07-17 | Task 3 (MSTEST0038 AvoidAssertAreSameWithValueTypes: 3 edge-case tests), Task 7 |
| 2026-07-16 | Task 3 (MSTEST0070 MemberConditionShouldBeValid: 3 tests), Task 7 |
| 2026-07-15 | Task 3 (AvoidOutParameterOnAssertIsInstanceOfTypeFixer: 2 tests), Task 7 |
| 2026-07-14 | Task 3 (TestClassShouldBeValid static-class guard: 2 tests), Task 7 |
| 2026-07-13 | Task 3 (MSTEST0035 UseRetryWithTestMethod: 4 tests), Task 7 |
| 2026-07-10 | Task 3 (MSTEST0063: 4 tests), Task 7 |
| 2026-07-09 | Task 3 (MSTEST0061 + MSTEST0029 edge cases), Task 7 |
| 2026-07-07 | Task 3 (MSTEST0062), Task 4, Task 7 |
| ≤2026-07-06 | Tasks 3/4/7 for many MSTEST00xx analyzers |

7. **Assert/CollectionAssert/StringAssert audit (2026-08-17)**: Did a broad sweep of `src/TestFramework/TestFramework/Assertions/*.cs` looking for zero-coverage newer methods (AreAllDistinct, AreAllNotNull, AreAllOfType, AreAllOfType span/memory, StringAssert.Regex, CollectionAssert.Subset/Type/Membership, Assert.That expression evaluation internals). All are already thoroughly covered (dozens of edge-case tests each) by prior runs. No fresh zero-coverage gap found this pass — future runs should look at MTP/Retry/Analyzer areas rather than core Assert methods, which appear saturated.

## Last Run

2026-08-22 UTC

## Completed Work (recent, summarized)

- PR (2026-08-13) — ReportFileWriterHelper.RetryWhenIOExceptionAsync (SharedExtensionHelpers, consumed by TrxReportEngine and other report writers): added 4 tests (immediate success, retry-then-success on transient IOException, immediate propagation of non-IOException, rethrow after retry timeout elapses via a SequenceClock stub). Also discovered/corrected: TrxReportGeneratorCommandLineTests.cs already existed with full coverage (memory/grep had missed it or it was added since); issue #10316 is still OPEN, not closed as previously recorded — corrected in backlog.
- PR (2026-08-12) — RetryArtifactProcessor.ProcessAsync (Microsoft.Testing.Extensions.Retry, from PR #10542): added 8 tests covering no eligible processors, attemptCount<2, incomplete per-attempt coverage, null processor result, successful merge (replacement recorded), non-cancellation exception (warning logged + displayed, no throw), and OperationCanceledException rethrow. New `TestArtifactPostProcessor` fake helper added (delegate-based `IArtifactPostProcessor`). Full Microsoft.Testing.Extensions.UnitTests suite: 1097 total, 1063 succeeded, 0 failed, 34 skipped after change.
- PR (2026-08-09) — SharedFileSystemPathInTestAnalyzer (MSTEST0077, issue #10316): added `WhenTestMethodCreatesFileSymbolicLinkToConstantTarget_NoDiagnostic`, mirroring the existing Directory.CreateSymbolicLink negative test — File.CreateSymbolicLink's `pathToTarget` param must not be flagged. Full MSTest.Analyzers.UnitTests suite: 1726/1726 passed. Note: most of issue #10316's other listed gaps (write/append/create family, Encrypt/Decrypt/SetAttributes/SetUnixFileMode) turned out to already be covered by existing DataRow tests — issue body may be stale; consider commenting/closing next run.
- PR (2026-08-08) — TestFilterProviderShouldBeValidAnalyzer (MSTEST0081): added `WhenFilterTypeIsInternalWithPublicConstructor_NoDiagnostic` and `WhenFilterTypeIsInternalWithInternalConstructor_Diagnostic`, covering that the filter type's own accessibility is irrelevant but the constructor's declared accessibility (public vs internal) determines whether `Activator.CreateInstance(Type)` can instantiate it. Full MSTest.Analyzers.UnitTests suite: 1723/1723 passed.

- PR (2026-08-07) — DependsOnShouldBeValidAnalyzer (MSTEST0078): added `WhenReferencedTypeIsInternalWithoutDiscoverInternals_NotATestClass` and `WhenReferencedTypeIsInternalWithDiscoverInternals_Cycle`, covering the `HasValidAccessibility` branch for internal *target classes* (previously only internal target *methods* were covered). Full MSTest.Analyzers.UnitTests suite: 1723/1723 passed.

- PR (2026-08-06) — MSTEST0077 SharedFileSystemPathInTestAnalyzer: added `WhenAssemblyInitializeWritesConstantPath_NoDiagnostic` and `WhenGlobalTestInitializeWritesConstantPath_Diagnostic`, closing the last remaining fixture-branch gap among the 4 parallel-safety analyzers (0074/0075/0076/0077). Full MSTest.Analyzers.UnitTests suite: 1721/1721 passed.
- PR (2026-08-05) — MSTEST0074 (UndeclaredProcessGlobalStateMutationAnalyzer) + MSTEST0076 (CultureMutationUnderParallelizationAnalyzer): added `WhenTestMethodSetsEnvironmentVariableInGlobalTestInitialize_DiagnosticWithoutFix` and `WhenGlobalTestInitializeSetsDefaultThreadCurrentCulture_Diagnostic` — fills the "global fixture: diagnostic fires but no fix offered" branch gap, mirroring PR #10383's pattern for MSTEST0075. Locally built + ran full MSTest.Analyzers.UnitTests suite (1718 tests, all passed) before submitting.
- PR (2026-08-02) — MSTEST0054 UseCancellationTokenPropertyAnalyzer: 1 test; MSTEST0044 PreferTestMethodOverDataTestMethodAnalyzer: 1 test
- PR (2026-07-31) — CurrentDirectoryMutationUnderParallelizationAnalyzer: 2 fixture edge-case tests
- PR (2026-07-29) — CultureMutationUnderParallelizationAnalyzer (MSTEST0076): 2 edge-case tests
- PR (2026-07-28) — UnusedParameterSuppressor (MSTEST0047): 2 edge-case tests
- PR (2026-07-25) — DoNotStoreStaticTestContextAnalyzer (MSTEST0024): 2 edge-case tests
- PR (2026-07-18) — NonNullableReferenceNotInitializedSuppressor (MSTEST0028): 2 edge-case tests
- PR (2026-07-17) — MSTEST0038 AvoidAssertAreSameWithValueTypes: 3 edge-case tests
- PR (2026-07-16) — MSTEST0070 MemberConditionShouldBeValid: 3 tests
- PR (2026-07-15) — AvoidOutParameterOnAssertIsInstanceOfTypeFixer: 2 tests
- PR (2026-07-14) — TestClassShouldBeValid static-class guard: 2 tests
- PR (2026-07-13) — MSTEST0035 UseRetryWithTestMethod: 4 tests
- PR (2026-07-10) — MSTEST0063: 4 tests
- PR (2026-07-09) — MSTEST0061 MERGED; MSTEST0029 edge cases
- PR #9731 MERGED; PR #9669 MERGED; PR #9615 MERGED
- PRs #9516,#9489,#9481,#9468,#9438,#9410,#9382,#9355,#9314,#9301,#9223,#9199,#9164,#9103,#9092,#9061,#9020,#8977,#8941,#8909,#8885,#8869,#8837,#8809,#8781,#8721,#8706 — all merged

## Duplicate Monthly Activity Issues Note — RESOLVED 2026-08-06

Maintainer closed #10154 (as not_planned) on 2026-08-06. #10389 is now the sole open Monthly Activity 2026-08 issue — continue updating #10389 going forward. Do not recreate #10154.

## Testing Notes / Gotchas

- `--treenode-filter`/`--filter-uid` sometimes silently fall back to printing `--help` output for MSTest.Analyzers.UnitTests and Microsoft.Testing.Extensions.UnitTests instead of running tests. Workaround: run the assembly directly with no args (fast, full suite), or add `--report-trx --results-directory <dir>` and grep the generated `.trx` for specific test names to confirm they ran.
- `Microsoft.Testing.Extensions.UnitTests` uses MSTest Assert + Moq (not AwesomeAssertions). `ServiceProvider` (Platform, internal) and internal Retry types (e.g. `RetryArtifactProcessor`) are accessible via `InternalsVisibleTo`. Pattern: instantiate a real `ServiceProvider()` + `.AddService(fakeProcessor)` to register fake `IArtifactPostProcessor`s (mirrors `CtrfArtifactPostProcessorTests`).
- `IArtifactPostProcessor`/`ArtifactPostProcessingContext`/etc. are `[Experimental("TPEXP")]` — add `#pragma warning disable TPEXP` to any test file touching them.
- VSTHRD103 forbids sync `CancellationTokenSource.Cancel()` — use `await cts.CancelAsync()`.
- `[Embedded]`-decorated internal types (e.g. `DisposeHelper`, `TimeoutHelper`, `ApplicationStateGuard`) are compiled as *linked source* into consumer projects rather than shipped in `Microsoft.Testing.Platform.dll` proper — `InternalsVisibleTo` alone does NOT make them visible to `Microsoft.Testing.Platform.UnitTests` (CS0103). Fix: add a `<Compile Include="...\Helpers\XHelper.cs" Link="Helpers\XHelper.cs" />` line in the test `.csproj`'s existing "Embedded helpers from Microsoft.Testing.Platform" ItemGroup, following the pattern already used for `TimeoutHelper.cs` etc.
- New `.cs` test files MUST have UTF-8 BOM or `dotnet format whitespace --verify-no-changes` fails with `CHARSET: Fix file encoding`.

2026-08-18 UTC

## Completed Work (recent)

- PR (2026-08-18) — DisposeHelper.DisposeAsync (Microsoft.Testing.Platform, internal `[Embedded]` helper, previously 0 tests): added 6 tests covering null input, IAsyncCleanableExtension-only, IAsyncDisposable+IDisposable combo (net8/net9 — verifies DisposeAsync preferred over Dispose), IDisposable-only (netcoreapp vs net462 variants), both cleanable+disposable together, and plain object (no-op). Required linking DisposeHelper.cs into the test csproj (see gotcha above) since InternalsVisibleTo wasn't sufficient. Full Microsoft.Testing.Platform.UnitTests suite (net8.0): 2213 total, 2194 succeeded, 0 failed, 19 skipped (pre-existing).
- Confirmed PR #10635 (human-authored, open) already covers ReportFileWriterHelper.RetryWhenIOExceptionAsync — do not duplicate.
- Fallback candidates not yet used (still viable next run): `RetryThresholdPolicy` (Microsoft.Testing.Extensions.Retry, no tests — requires constructing a real `RetryFailedTestsPipeServer` with IServiceProvider/named-pipe deps; deprioritized as too heavy to mock cleanly).

## 2026-08-19 UTC

## Completed Work (recent)

- PR (2026-08-19) — StackTraceRegexHelper.CreateFrameRegexPattern (src/Platform/SharedExtensionHelpers, previously 0 tests): added 6 tests covering matchFramesWithoutLocation true/false branches (frame with/without file+line info), the required 3-space "at" indentation, and the MatchTimeout constant. **Correction to the `[Embedded]` gotcha above**: `StackTraceRegexHelper` is a plain `internal static class` (NOT `[Embedded]`-attributed) and IS compiled into `Microsoft.Testing.Platform.dll` proper — it is already visible to `Microsoft.Testing.Platform.UnitTests` via existing `InternalsVisibleTo`, no source-link needed. Attempting to also `<Compile Include>` link its source (as done for genuinely `[Embedded]` types) causes CS0436 duplicate-type errors, since the type would then exist both in the referenced assembly and as directly-compiled source. Rule of thumb: only link source for internal helpers confirmed `[Embedded]`-attributed; for plain internal types, just add a `using` and rely on IVT. Full Microsoft.Testing.Platform.UnitTests suite (net8.0): 2213 total, 2194 succeeded, 0 failed, 19 skipped (pre-existing, unchanged).

## 2026-08-22 UTC (this run)

- Task 2/3: Investigated the 5 Retry IPC serializer classes (`ArtifactRequestSerializer`, `FailedTestRequestSerializer`, `GetListOfFailedTestsRequestSerializer`, `GetListOfFailedTestsResponseSerializer`, `TestRunCountsRequestSerializer` in `src/Platform/Microsoft.Testing.Extensions.Retry/Serializers/`) as a new candidate — non-trivial custom binary round-trip logic, 0 direct tests. **Confirmed architecturally blocked**: their base types (`NamedPipeSerializer<T>`, `INamedPipeSerializer`, `BaseSerializer` in `Microsoft.Testing.Platform/IPC/`) are `[Embedded]`-attributed (Roslyn compiler-embedded-type marker). `[Embedded]` types are linked into each consumer project via `<Compile Include>` and become **compiler-private types invisible outside their own compilation**, even to assemblies with `InternalsVisibleTo` — confirmed via `CS0246: The type or namespace name 'INamedPipeSerializer' could not be found` despite correct `using` directive and confirmed IVT grants both from Platform and from Retry. **New gotcha for memory** (extends the existing `[Embedded]` gotcha): the existing gotcha said "link the source into the *test* csproj to fix it" (which worked for `DisposeHelper`/`TimeoutHelper`) — but that trick only works when the *test project itself* needs the type. Here the type is used as a *parameter/return type in the test's own helper method*, and even linking `NamedPipeSerializer.cs`/`INamedPipeSerializer.cs` into the test csproj creates yet another distinct copy, unrelated to the Retry assembly's copy the concrete serializers actually implement — so instanceof/casts still fail conceptually (not attempted fully, abandoned after confirming CS0246 is fundamental, not a config gap). Conclusion: **do not pursue direct unit tests of `[Embedded]`-base-type serializers from an external test project** — would need either (a) linking the source directly into the test project's own compilation AND testing only via reflection/duck-typing, or (b) an in-repo internal test hosted inside the Retry assembly itself (not how this repo's test projects are structured). Abandoned branch `test-assist/retry-serializers`, deleted the WIP test file, no PR created this run.
- Re-verified `RetryThresholdPolicy`/`RetryDataConsumer`/`RetryLifecycleCallbacks`/`RetryTestHostRunner` — still blocked by live named-pipe dependency, per prior runs. No new fallback candidate found/pursued this run.
- Verified 4 open human-authored PRs (#10655, #10649, #10635, #10631) unchanged, awaiting maintainer review — kept in Monthly Activity Suggested Actions.
- Updated backlog item removing the serializer idea as "not viable via standard IVT pattern" (see Monthly Activity issue #10389 backlog section for full text).
- No new PR this run. Task 7 done (issue #10389 updated). Last Run updated to 2026-08-22.

## 2026-08-20 UTC (this run)

- Task 2/3 research only this run — no new PR. Investigated `RetryThresholdPolicy.EvaluateAsync` again (still the top fallback candidate) but confirmed the same blocker as 2026-08-18: its only non-trivial dependency, `RetryFailedTestsPipeServer`, has a constructor that stands up a real `NamedPipeServer` (needs `IEnvironment`, `ILogger`, `ITask`, `CancellationToken` from a live `IServiceProvider`) and its counters (`TotalTestRan`, `FailedTestResults`, etc.) only have private setters mutated via IPC callback — no seam for direct construction/faking without either standing up the full pipe or reflection hacks. Deprioritizing again; not worth the mocking cost vs. value (thin arithmetic branches).
- Corrected stale backlog item #1 ("MSTest.Engine internal class coverage — TestArgumentsManager, TestFixtureManager, ThreadPoolTestNodeRunner"): grepped the whole repo, **these classes do not exist** in the current codebase. Removing from backlog — likely referred to classes that were renamed/removed, or a hallucinated entry from an earlier run. Do not resurface.
- Verified other Retry-area files also lack direct tests but are all superseded by open human-authored PRs: `RetrySummaryReporter` (covered by `RetryTests.cs`), `RetryArgumentsBuilder` (has own test file), `RetryCommandLineOptionsProvider` (covered). `RetryDataConsumer`, `RetryLifecycleCallbacks`, `RetryTestHostRunner` have 0 direct tests but are thin orchestration wrappers around already-tested pieces — low value, not pursued.
- Confirmed PR #10579 (RetryArtifactProcessor) merged 2026-08-18 — no action needed.
- Found 4 open human-authored (Evangelink) PRs overlapping/superseding prior backlog items, all awaiting maintainer review — added to Monthly Activity "Suggested Actions": #10655 (StackTraceRegexHelper), #10649 (DisposeHelper), #10635 (ReportFileWriterHelper retry, closes #10598), #10631 (MSTEST0001 adapter flag parsing).
- No Task 3 PR this run (no viable non-trivial gap found beyond already-deprioritized RetryThresholdPolicy). Did Task 2 (backlog review/correction) + Task 7.

## 2026-08-24 UTC (this run)

- Task 4 reconciliation: confirmed PRs #10655, #10649, #10635, #10631 all MERGED (2026-08-24). Removed from Monthly Activity Suggested Actions. Issue #10316 remains closed, no action.
- Task 2/3: Scanned `src/Platform/SharedExtensionHelpers/` for zero-coverage helpers (grep against test/UnitTests/). Zero-coverage list: `ArtifactPostProcessingHelper`, `ReportFileNameHelper`, `ReportGeneratorBase`, `RunSettingsCommandLineOptionsProviderBase`, `RunSettingsConfigurationProviderBase`, `RunSettingsEnvironmentVariableProviderBase`, `RunSettingsProviderHelper`, `SlowTestReporterBase`, `SummaryReporterHelpers`, `TestCaseFilterCommandLineOptionsProviderBase`, `TestRunParametersCommandLineOptionsProviderBase`.
- Selected `ArtifactPostProcessingHelper.IsReparsePoint` (security-relevant symlink guard used by Trx/Html artifact post-processors). Confirmed NOT `[Embedded]`-attributed but is `<Compile Include>`-linked into multiple report-engine assemblies (Trx/Html/JUnit/Ctrf), so direct reference from the test project is ambiguous (CS0433). Used the existing `ReportFileNameSanitizationConsistencyTests.cs` reflection pattern (`assembly.GetType(...).GetMethod(..., BindingFlags.NonPublic | BindingFlags.Static)`, anchored on `TrxReportEngine` from the Trx assembly) instead of writing a direct test — this is the correct approach for any linked-source helper compiled into multiple assemblies (distinct from the `[Embedded]` compiler-marker case, which is a separate, harder blocker).
- Added `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/ArtifactPostProcessingHelperTests.cs` (4 tests: regular dir → false, non-existent path → true, symlink to dir → true, plain file → false). Built successfully (`dotnet build .../Microsoft.Testing.Extensions.UnitTests.csproj -c Debug`, ~3min from clean SDK install). Ran targeted (`--filter "FullyQualifiedName~ArtifactPostProcessingHelperTests"`) → 4/4 passed. Ran full suite → 1129 total, 1129 succeeded, 0 failed, 34 skipped (no regressions). `dotnet format whitespace --verify-no-changes` clean.
- **New command note**: `--treenode-filter` returned `--help` output (didn't match) for this project; `--filter "FullyQualifiedName~ClassName"` worked correctly and is the reliable option for Microsoft.Testing.Extensions.UnitTests / MSTest.Analyzers.UnitTests.
- Created draft PR "Add unit tests for ArtifactPostProcessingHelper.IsReparsePoint" on branch `test-assist/artifact-post-processing-helper`.
- Remaining SharedExtensionHelpers zero-coverage candidates for future runs: `ReportFileNameHelper` (thin wrapper, likely low value), the `*Base` abstract classes (need complexity assessment before committing effort — may require more setup/mocking than they're worth).
- Task 7 done: issue #10389 updated (removed 4 merged PRs from Suggested Actions, added new PR to Suggested Actions, updated backlog, added Run History entry for run 32787262866).

## 2026-08-25 UTC (this run)

- Task 3: Selected `ArtifactPostProcessingHelper.OrderInputs` (multi-key deterministic sort: path, module, framework, architecture, executionId; used by Trx/JUnit/Ctrf/Html post-processors), previously 0 direct tests. Verified via reflection listing that `ArtifactPostProcessingHelper.cs` still contains only `OrderInputs` + `IsReparsePoint` (unaffected by the merged PRs #10731/#10717/#10632 that touched the surrounding area).
- Extended `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/ArtifactPostProcessingHelperTests.cs`: refactored the reflection setup to share `ArtifactPostProcessingHelperType`, added `OrderInputsMethod` + `InvokeOrderInputs`/`CreateInput` helpers, added 3 tests: no-module-metadata path+executionId sort, full 5-key sort with module metadata, and relative-vs-absolute path resolution via `Path.GetFullPath`. Required `using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;` + `#pragma warning disable TPEXP` for `InputArtifact` (Experimental).
- Build succeeded; targeted tests 7/7 passed; full `Microsoft.Testing.Extensions.UnitTests` suite: 1184 total, 1184 succeeded, 0 failed, 34 skipped (was 1129 before — no regressions, +55 from new tests + other repo activity). `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean (note: must pass `TestFx.slnx` explicitly — bare `dotnet format` fails with "Multiple MSBuild solution files found").
- Created draft PR "Add unit tests for ArtifactPostProcessingHelper.OrderInputs" on branch `test-assist/artifact-post-processing-helper-orderinputs`.
- Remaining SharedExtensionHelpers zero-coverage candidates for future runs (unchanged from 2026-08-24): `ReportFileNameHelper`, and the `*Base` abstract classes (`ReportGeneratorBase`, `RunSettingsCommandLineOptionsProviderBase`, `RunSettingsConfigurationProviderBase`, `RunSettingsEnvironmentVariableProviderBase`, `RunSettingsProviderHelper`, `SlowTestReporterBase`, `SummaryReporterHelpers`, `TestCaseFilterCommandLineOptionsProviderBase`, `TestRunParametersCommandLineOptionsProviderBase`) — need complexity assessment before committing effort.
- Task 7 done: issue #10389 updated with new Run History entry for run 32908766279, new PR added to Suggested Actions, backlog refreshed.

## Run 2026-08 (later) — RunSettingsConfigurationProvider

- Task 2/3: Assessed remaining `SharedExtensionHelpers` zero-coverage candidates from prior list.
  - `ReportFileNameHelper`: confirmed trivial one-line wrapper — skip (low value).
  - `SummaryReporterHelpers` (`GetTerminalKind`, `FormatDuration`): already saturated — indirectly covered via `AzureDevOpsSummaryReporterTests.cs`/`GitHubActionsSummaryReporterTests.cs` (incl. >24h branch, cancelled-state branch). Skip.
  - `RunSettingsProviderHelper`: mostly saturated. `CanReadFile` covered via `RunSettingsCommandLineOptionsProviderTests.cs`; `TryLoadRunSettingsAsync`/`HasEnvironmentVariables`/`ApplyEnvironmentVariables` covered via `RunSettingsEnvironmentVariableProviderTests.cs`; `FindInvalidTestParameter` covered indirectly via `TestRunParameterCommandLineOptionsProviderTests.cs`. Skip.
  - `RunSettingsConfigurationProviderBase` (91 lines, shared into `MSTestRunSettingsConfigurationProvider` + VSTestBridge's `RunSettingsConfigurationProvider`): genuinely **zero coverage** — real XML lookup + hierarchical config-key routing logic (`TryGet`, `GetChildKeys`, `TryGetScalar`, `BuildAsync`, `IsEnabledAsync`). Selected as target.
- Added `test/UnitTests/Microsoft.Testing.Extensions.VSTestBridge.UnitTests/Configurations/RunSettingsConfigurationProviderTests.cs` (12 tests) for the concrete `RunSettingsConfigurationProvider`. Full suite: 84/84 passed (72 pre-existing + 12 new), 0 failures. Format clean.
- Created draft PR "Add unit tests for RunSettingsConfigurationProvider" on branch `test-assist/run-settings-config-provider`.
- **New gotcha**: this repo's own test code is subject to MSTest analyzers. `MSTEST0068` requires `Assert.AreSequenceEqual(expected, actual)` instead of `CollectionAssert.AreEqual(...)`. `MSTEST0037` requires `Assert.IsEmpty(collection)` instead of `Assert.IsFalse(collection.Any())`/`Assert.AreEqual(0, collection.Count)`. Also watch for IDE0300 (prefer collection-expression `[...]` over `new[] { ... }`). Fix these proactively in new test files to avoid a rebuild cycle.
- Remaining zero-coverage `SharedExtensionHelpers` candidates for future runs, need complexity assessment: `ReportGeneratorBase`, `RunSettingsCommandLineOptionsProviderBase`, `RunSettingsEnvironmentVariableProviderBase`, `SlowTestReporterBase`, `TestCaseFilterCommandLineOptionsProviderBase`, `TestRunParametersCommandLineOptionsProviderBase`. Note: the sibling `MSTestRunSettingsConfigurationProvider` (MSTest.TestAdapter's linked copy of the same base) may still be worth testing in a future run for MSTest.TestAdapter-specific wiring, though the shared base logic is now covered via VSTestBridge's tests.

## Run 2026-08-28 UTC (this run)

- Task 3: Assessed `SlowTestReporterBase` (290 lines, internal abstract, tested only via concrete subclasses `GitHubActionsSlowTestReporter`/`AzureDevOpsSlowTestReporter`). Most logic (scan loop, backoff, per-test tracking, `OnTestSessionStartingAsync`, `ConsumeAsync` InProgress/Terminal/ExecutionCompleted) already saturated via existing 321-line + sibling test files.
- Found one genuine untested branch: `ConsumeAsync`'s `IsSupersededRetryAttempt()` check (line ~147) — a terminal update for a superseded retry attempt must NOT stop tracking, since a later attempt for the same node is still running. Not covered in either `GitHubActionsSlowTestReporterTests.cs` or `AzureDevOpsSlowTestReporterTests.cs` (grepped both, no `RetryAttempt`/`Superseded` references).
- Added `ConsumeAsync_SupersededRetryAttempt_KeepsTrackingAsync` to `GitHubActionsSlowTestReporterTests.cs`; extended `CreateMessage` helper with an optional `retryAttempt` param (uses `RetryAttemptProperty(1, isSuperseded: true)` from `Microsoft.Testing.Platform.Extensions.Messages`).
- Build succeeded; targeted suite 12/12 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0: 1410 passed, 0 failed, 34 skipped. `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean.
- Created draft PR "Cover superseded retry-attempt branch in SlowTestReporterBase" on branch `test-assist/slow-test-reporter-retry-attempt`.
- Verified via `github-pull_request_read`/`search_pull_requests` that the 3 PRs previously in issue #10389's Suggested Actions (IsReparsePoint #10731, OrderInputs #10766, RunSettingsConfigurationProvider #10796) are all **merged** — removed from Suggested Actions.
- Confirmed no open `[test-improver]` PRs before this run (Task 4: nothing to maintain) and no open issues labeled `testing` (Task 5: nothing actionable this run).
- Remaining zero-coverage `SharedExtensionHelpers` candidates for future runs: `ReportGeneratorBase`, `RunSettingsCommandLineOptionsProviderBase`, `RunSettingsEnvironmentVariableProviderBase`, `TestCaseFilterCommandLineOptionsProviderBase` (likely trivial, 27 lines — skip), `TestRunParametersCommandLineOptionsProviderBase`. `SlowTestReporterBase` now considered exhausted (only remaining minor gap would be `OnActivating()` override checks — low value, skip).
- Task 7 done: issue #10389 updated with new Run History entry for run 33133316906, new PR added to Suggested Actions, merged PRs removed, backlog refreshed.

## Run 2026-08-28 (later, run 33218583492) — ReportGeneratorBase

- Task 3: Selected `ReportGeneratorBase` (shared session-lifecycle base for CTRF/JUnit/HTML/TRX/AzDO report generators) as the target — zero direct coverage of `IsEnabledAsync`, `ConsumeAsync`, `OnTestSessionStartingAsync`, `OnTestSessionFinishingAsync`.
- Construction strategy confirmed working: build a real `Microsoft.Testing.Platform.Services.ServiceProvider`, register mocked/fake dependencies via `AddService(...)` (`IFileSystem`, `IEnvironment`, `ITestFramework`, `IConfiguration`, `ITestApplicationModuleInfo` via Moq; stub classes for `IMessageBus`, `IClock`, `ICommandLineOptions`, `IOutputDevice`, `ILoggerFactory`, `ITestApplicationProcessExitCode`), then pass to `CtrfReportGenerator`'s `IServiceProvider` constructor. **Must set `AllowTestAdapterFrameworkRegistration = true` before `AddService(iTestFrameworkMock)`** or it throws.
- Added `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/CtrfReportGeneratorLifecycleTests.cs` (5 tests): option-enabled toggle, `UnreachableException` when finishing without starting, full lifecycle (start → consume passed/failed test nodes + an ignored non-`TestNodeUpdateMessage` → finish → assert `SessionFileArtifact` published on message bus), no-warning happy path.
- `SessionUid` lives in `Microsoft.Testing.Platform.TestHost` namespace — needed explicit `using` (easy to miss since it's not in `Extensions.TestHost`).
- Build succeeded; targeted 5/5 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0 suite: 1465 total, 1465 succeeded, 0 failed, 37 skipped (no regressions). Format check clean.
- Created draft PR "Add unit tests for ReportGeneratorBase session lifecycle" on branch `test-assist/report-generator-base-lifecycle`.
- `ReportGeneratorBase` now considered exhausted for this simple-happy-path level; a possible future extension is testing the warning-display path itself by making `CtrfReportEngine.GenerateReportAsync` return a non-null warning (not attempted this run — needs investigation into what triggers a CTRF warning).
- Remaining zero-coverage `SharedExtensionHelpers` candidates for future runs: `RunSettingsCommandLineOptionsProviderBase`, `RunSettingsEnvironmentVariableProviderBase`, `TestRunParametersCommandLineOptionsProviderBase`. `TestCaseFilterCommandLineOptionsProviderBase` (27 lines) likely trivial — skip.
- Task 4/5: confirmed no open `[test-improver]` PRs needing maintenance beyond the new one just created; no open issues labeled `testing` needing comment this run.
- Task 7 done: issue #10389 updated with new Run History entry for run 33218583492, new PR added to Suggested Actions, backlog refreshed.

## Run 2026-08-29 (run 33279734783) — RunSettingsCommandLineOptionsProviderBase

- Task 4/7 reconciliation: confirmed PR #10840 ("Cover superseded retry slow-test tracking", fixes #10825) is **merged** (2026-08-28) — removed from Suggested Actions. PR #10860 (ReportGeneratorBase, fixes #10850) remains **open** — kept in Suggested Actions.
- Task 3: Selected `RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync` (browser/WebAssembly guard: loads runsettings and rejects `<EnvironmentVariables>` on browser; always valid otherwise) — zero coverage. Verified `RunSettingsEnvironmentVariableProviderBase` and `TestRunParametersCommandLineOptionsProviderBase` are already fully tested (no action needed — remove from backlog).
- Added 2 tests to `RunSettingsCommandLineOptionsProviderTests.cs` (VSTestBridge): non-browser always-valid (even with `<EnvironmentVariables>` present), and no-runsettings-provided valid path. **Limitation**: `OperatingSystem.IsBrowser()` is always false on net8.0/net462 test hosts, so the actual browser-rejection branch can't be exercised — documented inline, consistent with other untested `IsBrowser()` guards elsewhere in repo (no test attempts them either).
- Build succeeded; targeted 6/6 passed; full `Microsoft.Testing.Extensions.VSTestBridge.UnitTests` suite: 91/91 passed, 0 failed (vstestbridge.dll coverage 1.9%→52.3% line). Format check clean.
- Created draft PR "Cover RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync" on branch `test-assist/run-settings-provider-browser-guard`.
- Remaining zero-coverage `SharedExtensionHelpers` candidates for future runs: none of real value left — only `TestCaseFilterCommandLineOptionsProviderBase` (27 lines, trivial, skip). Consider next run pivoting to Task 5 (issue comments) or Task 6 (test infrastructure) since the `SharedExtensionHelpers` backlog begun 2026-08-24 is now exhausted.
- Task 7 done: issue #10389 updated with new Run History entry for run 33279734783, new PR added to Suggested Actions, backlog refreshed, merged PR item removed.

## Run 2026-08-30 (run 33340422292) — recovered orphaned RunSettingsCommandLineOptionsProviderBase PR

- **Root cause found for the 2026-08-29 discrepancy**: `create_pull_request` from the prior run pushed the branch `test-assist/run-settings-provider-browser-guard-e0909b8481cc70cc` (safe-outputs suffixes branch names with a random hash) and committed the test file, but the PR was never actually opened (no PR exists under any title/head search, and issue #10389's own Suggested Actions only had an unlinked bullet for it). **Lesson**: after calling `create_pull_request`, don't blindly trust memory notes across runs — verify the PR actually exists via `github-search_pull_requests`/`list_pull_requests` at the start of the next run before assuming success.
- Recovered the work: fetched the orphaned branch, cherry-picked its single commit (`c07310a87`, "Add tests for RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync") onto a fresh branch `test-assist/run-settings-provider-browser-guard` off current `origin/main` (avoided rebasing — the orphaned branch had unrelated/divergent history with a different root commit, so `git rebase` hit thousands of conflicts; cherry-pick of the single real commit was clean).
- Re-verified: `./build.sh` succeeded (0 warnings/errors); targeted `RunSettingsCommandLineOptionsProviderTests` 6/6 passed; full `Microsoft.Testing.Extensions.VSTestBridge.UnitTests` net8.0 suite 91/91 passed, 0 failures, 0 skipped; `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean.
- Created draft PR "Cover RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync" on branch `test-assist/run-settings-provider-browser-guard` (note: different branch name than the orphaned one — no trailing hash since created directly via git, not through the safe-outputs branch-naming convention).
- **`SharedExtensionHelpers` backlog vein is now fully exhausted** — only `TestCaseFilterCommandLineOptionsProviderBase` (27 lines, trivial) remains and is explicitly skipped as low-value. Future runs should pivot to Task 2 (fresh opportunity discovery), Task 5 (issue comments), or Task 6 (test infrastructure investment).
- Task 4: No other open `[test-improver]` PRs needed maintenance this run (PR #10860 ReportGeneratorBase remains open with clean automated reviews, no action needed).
- Task 5: Confirmed 0 open issues labeled `testing` — nothing actionable.
- Task 7 done: issue #10389 updated with new Run History entry for run 33340422292, new PR added to Suggested Actions, backlog note about exhausted SharedExtensionHelpers vein refreshed.
