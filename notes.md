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
7. **`AzureDevOpsCommandLineProvider` validation gaps — DONE 2026-09-01**: added 33 new tests covering slow-test-history/artifact-upload/publish-run-name cross-option requirements plus numeric-range and glob-pattern argument validation. `AzureDevOpsReport` area now well-covered: `AzureDevOpsPublishConfigurationFactory` (done 2026-08-31), `AzureDevOpsCommandLineProvider` (done 2026-09-01). Remaining candidates for future runs: other report-provider base classes not yet swept (e.g. `GitHubActionsReport`, `HtmlReport`, `JUnitReport` command-line providers — not yet checked for similar validation gaps).

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

2026-09-20 UTC (run 35543089018)

## Completed Work (recent, summarized)

- PR (2026-09-08) — MicrosoftExtensionsLoggingBuilderExtensions.AddMicrosoftExtensionsLogging (both overloads, Microsoft.Testing.Extensions.Logging, previously 0 tests): added 9 tests covering null-argument validation (4), LogLevel.None short-circuit (2), happy-path log forwarding + ownsFactory disposal semantics (2), and builder chaining (1). Pattern: drive registered provider factories via internal `LoggingManager.BuildAsync(IServiceProvider, LogLevel, IMonitor)` (accessed via cast + IVT) rather than reaching into private fields; used a small in-file `CapturingLoggerProvider`/`CapturingLogger` to assert forwarded messages. New gotcha: `Microsoft.Extensions.Logging` and `Microsoft.Testing.Platform.Logging` both define `ILoggerFactory`/`ILogger` — causes CS0104 ambiguous-reference if both namespaces are `using`'d; resolve with explicit aliases (`MtpILogger`, `MtpILoggerFactory`) per existing `MicrosoftExtensionsLoggingBridgeTests.cs` convention. Also confirmed MTP's `ILoggerFactory`/`ILogger` do NOT implement `IDisposable` (unlike the MEL counterpart) — cannot `using`-declare them. CLI gotcha reconfirmed: `--treenode-filter` printed `--help` for this project too; `--filter-uid` ran but matched 0 tests silently — just run the built binary with no args for full-suite verification. Full Microsoft.Testing.Extensions.UnitTests suite: 1765 total, 1728 succeeded, 0 failed, 37 skipped (includes new tests).

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

## Older Run Details (2026-08-19 to 2026-09-06) — summarized

Full detail for these runs has been trimmed to keep this file within size limits; key lasting facts are captured in the 'Testing Notes / Gotchas' and 'Key Test Pattern Notes' sections above, and in the 'Tasks Run History (summarized)' table. Notable PRs from this window (all merged unless noted): StackTraceRegexHelper, ArtifactPostProcessingHelper (IsReparsePoint + OrderInputs), RunSettingsConfigurationProvider, SlowTestReporterBase (superseded-retry branch), ReportGeneratorBase, RunSettingsCommandLineOptionsProviderBase (browser guard), AzureDevOpsPublishConfigurationFactory, AzureDevOpsCommandLineProvider validation gaps, HangDump/CrashDump command-line providers, VideoRecorderCommandLineProvider, MicrosoftExtensionsLogging bridge (recovered from orphaned-PR discrepancy twice: 2026-09-05 and 2026-09-06).

## 2026-09-08 UTC (this run)

- Task 3: PR "Add unit tests for MicrosoftExtensionsLoggingBuilderExtensions" created (9 tests, both `AddMicrosoftExtensionsLogging` overloads). Full details above in "Completed Work" section.
- Task 7: issue #10920 (September) updated — new Run History entry prepended with this run's link, added new PR to Suggested Actions, removed the now-addressed backlog item, refreshed Discovered Commands wording (clarified `--treenode-filter`/`--filter-uid` unreliability applies broadly, not just one project).
- Task 2/3/4/5: not performed this run — full time budget spent on diagnosis + recovery of the orphaned PR + verification build/tests.
- Remaining candidates for future runs (unchanged): `MicrosoftExtensionsLoggingBuilderExtensions` (2 `AddMicrosoftExtensionsLogging` overloads, needs real `ILoggerFactory`/`ITestApplicationBuilder`); `Microsoft.Testing.Extensions.Telemetry` (`AppInsightTelemetryClient`/`AppInsightTelemetryClientFactory`, ~400 LOC, mock AppInsights SDK — do not hit live endpoint, firewall-blocked); `Microsoft.Testing.Extensions.AzureFoundry` (`OpenAIChatClientProvider`) unswept.

## Runs 2026-09-08 to 2026-09-15 — condensed summary

PRs from this window (all merged): #11130 (AppInsightTelemetryClientFactory + AppInsightsTelemetryProviderExtensions), #11199 (SummaryBudget, fixed once per Copilot Test Reviewer feedback), #11214 (GitHubActionsRepositoryRoot), #11232 (StepSummaryWriter.GetSummaryLength), #11254/#11260 (BoundedUtf8LineReader — #11260 superseded #11254 with maintainer review feedback), #11282 (PipeNameEnvironmentVariableProviderBase), #11329 (ReportEngineBase).

Key lasting gotchas from this window:
- **Copilot Test Reviewer bot** posts a mutation-style grading comment on every test-improver PR — check PR comments at the start of Task-4 passes; it catches weak assertions (relative comparisons, degenerate boundary values) that build+test-pass alone won't catch.
- Fetching an issue body via `issue_read` returns HTML-entity-escaped text (`&amp;`, `&#34;`) — run through `html.unescape()` before treating as literal markdown; also strip the trailing bot-generated footer before treating a fetched body as a clean editable base.
- Adding an optional `CancellationToken cancellationToken = default` parameter to a test helper triggers MSTEST0049 at every call site that doesn't pass an explicit argument — pass `CancellationToken.None` explicitly once introduced.
- GitHubActionsReport area (SummaryBudget → GitHubActionsRepositoryRoot → StepSummaryWriter) and BoundedUtf8LineReader/CI-run-summary-aggregation area are now considered well-covered/exhausted.

## Run 2026-09-16 (run 35159930843) — RandomId tests + reconciliation

- Task reconciliation: verified via GitHub search that no `[test-improver]`-prefixed PRs are currently open (Task 4: nothing to maintain). No open issues labeled `testing` other than the Monthly Activity issue #10920 itself (Task 5).
- Task 2/3: swept `Microsoft.Testing.Extensions.Retry` for zero-coverage classes per the standing backlog item ("re-check Retry/OpenTelemetry/Analyzers for newly-added zero-coverage classes"). Found `RandomId` (internal static class, 5-char id generator backing `RetryOrchestrator`'s temp-directory naming) had zero direct tests — only exercised incidentally via retry integration scenarios. Verified `MSTest.Analyzers` naming mismatches (`AvoidExplicitDynamicDataSourceTypeAnalyzer` vs `AvoidExplicitDynamicDataSourceTypeTests.cs`, `UseProperAssertMethodsAnalyzer.*` partial-class files vs `UseProperAssertMethodsAnalyzerTests.cs`) are just filename differences, not real gaps — analyzers area remains saturated, no action needed there.
- Added `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/RandomIdTests.cs` (4 tests): fixed 5-character length, character-pool membership across 100 samples (verifies the rejection-sampling loop never leaks an out-of-range byte), distinctness across 200 calls, and thread-safe concurrent invocation via `Parallel.For` (covers the `lock (Pool)` critical section).
- Build succeeded (0 warnings after fixing one IDE0008 var-vs-explicit-type warning on a `string[]` local). Targeted suite 4/4 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0 suite: 1843 total (was 1839), 0 failed, 37 skipped (pre-existing, no regressions). `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean (only the expected harmless F#-project warning).
- Created draft PR "Add unit tests for RandomId" on branch `test-assist/random-id-tests` via `create_pull_request` (tool returned `result: success` with a patch bundle). Per the established process note, verify at the start of next run that this PR actually materialized on GitHub before assuming success.
- Task 7: issue #10920 rebuilt cleanly (kept only the last ~4 Run History entries inline per the established pattern; older entries remain in this memory file's history). Suggested Actions set to only the new PR. Backlog refreshed: added `RetryExtensions.AddRetryProvider`/`RetryDataConsumer`/`RetryLifecycleCallbacks`/`RetryTestHostRunner` as lower-priority Retry-area candidates for future runs; confirmed `RetryThresholdPolicy`/`RetryFailedTestsPipeServer` remain architecturally blocked (no new construction strategy found); confirmed MSTest.Engine internal classes remain permanently absent from the codebase.
- Remaining candidates for future runs: `RetryExtensions.AddRetryProvider` (public wiring method — lightweight smoke test candidate), `NamedPipeServerFactory` (low value, needs a real bound pipe), `TestingPlatformBuilderHook` (low value, thin pass-through). Consider re-sweeping OpenTelemetry area next (not yet checked this run) or pivoting to Task 5/6 if the Retry vein thins out further.

## Run 2026-09-17 (run 35284383111) — RetryExtensions.AddRetryProvider tests + reconciliation

- Task reconciliation: confirmed via GitHub search that all 5 previously-outstanding test-improver PRs are merged: #11355 (RandomId), #11329 (ReportEngineBase), #11282 (PipeNameEnvironmentVariableProviderBase), #11199 (SummaryBudget), #11214 (GitHubActionsRepositoryRoot). No open `[test-improver]`-prefixed PRs found needing maintenance (Task 4). No open issues labeled `testing` other than the Monthly Activity issue itself (Task 5).
- Task 2/3: Selected `RetryExtensions.AddRetryProvider` (public wiring method for the entire retry-failed-tests feature — command-line options provider, test-host application lifetime callback, data consumer/test session lifetime handler, and test-host orchestrator) per the backlog item seeded in the 2026-09-16 run. Confirmed zero direct coverage — all existing Retry-area test files (`RetryTests`, `RetryDataConsumerTests`, `RetryArgumentsBuilderTests`, `RetryOrchestratorHelperTests`) construct the individual extension classes directly, never exercising this wiring entry point.
- Added `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/RetryExtensionsTests.cs` (3 tests): command-line provider discoverable end-to-end via `CommandLineManager.BuildAsync` (cast `builder.CommandLine` to internal `CommandLineManager`, IVT-granted); exactly-one test-host-orchestrator factory registered (reflection on internal `TestHostOrchestratorManager._factories`, needed a `using X = Y;` alias since `TestHostOrchestratorManager` exists in both `Microsoft.Testing.Platform.TestHostOrchestrator` (concrete) and `Microsoft.Testing.Platform.Extensions.TestHostOrchestrator` (interface) namespaces — CS0104 ambiguity); and lifecycle-callback/data-consumer/session-lifetime-handler registration sharing the same composite `RetryDataConsumer` singleton.
- **Gotcha discovered**: `RetryLifecycleCallbacks.IsEnabledAsync()` gates on the **pipename** option (`RetryFailedTestsPipeNameOptionName`), not the user-facing `--retry-failed-tests` option — a `TestCommandLineOptions([])` with no options set means `BuildTestApplicationLifecycleCallbackAsync` silently returns an empty array (no exception, so an easy miss). Must set `RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName` in the fake `ICommandLineOptions` for the lifecycle callback to register.
- **Gotcha discovered**: `RetryDataConsumer.InitializeAsync()` calls `serviceProvider.GetRequiredService<RetryLifecycleCallbacks>()` — so `BuildTestApplicationLifecycleCallbackAsync`'s output must be registered into the `ServiceProvider` (via `AddServices`) *before* calling `BuildDataConsumersAsync`, exactly mirroring the real `TestHostBuilder.Modes.cs` pipeline order. Skipping this step throws `InvalidOperationException: Cannot find service of type 'RetryLifecycleCallbacks'`.
- **Gotcha discovered**: `BuildDataConsumersAsync`/`BuildTestSessionLifetimeHandleAsync` for composite-registered extensions (`CompositeExtensionFactory<T>`) must be called with the **same** `List<ICompositeExtensionFactory> alreadyBuiltServices` list across both calls, or each call creates its own singleton instance (assertion `AreSame` on the two returned `RetryDataConsumer` references fails with "same type, different instance" otherwise) — this mirrors how the real pipeline threads one shared list through both build phases.
- Build succeeded (0 warnings/errors after fixing the header — copied wrong dual-license header from `RetryTests.cs`'s neighbor by mistake; corrected to the standard MIT header used elsewhere in `Microsoft.Testing.Extensions.UnitTests`). Targeted 3/3 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0 suite: 1879 total, 1842 succeeded, 0 failed, 37 skipped (pre-existing, no regressions). `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean (only the expected harmless F#-project warning).
- Created PR "Add unit tests for RetryExtensions.AddRetryProvider" on branch `test-assist/retry-extensions-tests` via `create_pull_request` (tool returned `result: success` with a patch bundle).
- Task 7: issue #10920 updated — new Run History entry prepended (kept only last ~4 entries inline per established pattern), Suggested Actions set to only the new PR (all prior items confirmed merged), backlog refreshed (removed resolved AzureFoundry/HtmlReport-JUnitReport/MSTest.Engine items permanently, marked `RetryExtensions.AddRetryProvider` as addressed).
- Remaining candidates for future runs: `RetryDataConsumer`/`RetryLifecycleCallbacks`/`RetryTestHostRunner` (thin orchestration wrappers around already-tested pieces, low value per repeated prior assessment — reassess only if a genuinely untested branch is found); `NamedPipeServerFactory` (low value, needs a real bound pipe); `TestingPlatformBuilderHook` (low value, thin pass-through). Consider re-sweeping OpenTelemetry area (not recently revisited) or pivoting to Task 5 (issue comments)/Task 6 (test infrastructure) next run if the Retry-area vein is now exhausted.
## Run 2026-09-19 (run 35474658405) — recovered orphaned ActivityWrapper/OpenTelemetryEnvironmentVariables PR (again)

- Task 4/reconciliation: confirmed **both** the 2026-09-17 (`RetryExtensions.AddRetryProvider`, branch `test-assist/retry-extensions-tests`) and 2026-09-18 (`ActivityWrapper`/`OpenTelemetryEnvironmentVariables`, branch `test-assist/activity-wrapper-and-env-vars-tests`) PRs are **missing from GitHub** — searched by title, by head branch, and via `list_branches`; neither branch nor PR exists. This is the same recurring "safe-outputs branch-naming discrepancy" pattern seen multiple times before (2026-09-02, 2026-09-05, 2026-09-06, 2026-08-30) — a `create_pull_request` tool call reports `success` but the PR never materializes on GitHub, and the work is lost unless a later run notices and recreates it.
- Recreated only the more recent one (`ActivityWrapper`/`OpenTelemetryEnvironmentVariables`, since it was never merged and its content is still valid): re-verified `OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace` still has zero direct tests; re-added the 6 `ActivityWrapper` tests (non-W3C TraceId/SpanId null fallback, IsRecording, SetTag chaining, RecordException tag merge, non-ambient Dispose) to `OpenTelemetryPlatformServiceTests.cs`, plus a new `OpenTelemetryEnvironmentVariablesTests.cs` (7 tests) for `IsNullOrWhiteSpace`.
- **New gotcha for `IsRecording`**: a plain `new Activity(name); activity.Start()` with no `ActivitySource` always has `IsAllDataRequested == true` regardless of any registered listener (verified via a throwaway console repro) — there is no way to get a "not recording" `Activity` that way. The correct approach: create a **separate** `ActivitySource`+`ActivityListener` pair whose `Sample` callback returns `ActivitySamplingResult.PropagationData` (not `AllDataAndRecorded`), then start the activity through that source. This is required because the test class's own shared listener (registered in the constructor) always samples `AllDataAndRecorded`.
- Did **not** recreate the `RetryExtensions.AddRetryProvider` PR this run — instead flagging it in the Monthly Activity issue as a maintainer-visible gap, since recreating it from memory notes alone (rather than from a diff) risks drifting from what was actually validated; will recreate next run if still missing, from the detailed gotchas already recorded in the 2026-09-17 entry below.
- Build succeeded (fixed one IDE0350 simplify-lambda warning along the way — `(ref ActivityCreationOptions<ActivityContext> _) => ...` simplifies to `(ref _) => ...`, matching the existing `Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded` pattern already used in this file). Targeted suite 23/23 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0 suite: 1891 total (was 1879), 0 failed, 37 skipped (pre-existing, no regressions). `dotnet format whitespace TestFx.slnx --verify-no-changes --include <files>` clean.
- Created PR "Add unit tests for ActivityWrapper and OpenTelemetryEnvironmentVariables" on branch `test-assist/activity-wrapper-and-env-vars-tests-v2` (renamed with a `-v2` suffix since the original branch name may still exist server-side even though it wasn't discoverable via search/list_branches — avoids a possible silent collision).
- Task 2: re-verified the standing backlog item "Report-provider command-line providers not yet checked for validation gaps: GitHubActionsReport, HtmlReport, JUnitReport" is **stale** — `GitHubActionsCommandLineProviderTests.cs` (412 lines), `JUnitReportGeneratorCommandLineTests.cs` (173 lines), `HtmlReportGeneratorCommandLineTests.cs` (194 lines) all already exist with substantial coverage. Removing this item from the backlog permanently.
- Task 5: no open issues labeled `testing` other than the Monthly Activity issue itself.
- Task 7: issue #10920 updated — new Run History entry prepended, Suggested Actions includes the new PR plus a flagged item to re-verify/recreate the still-missing `RetryExtensions.AddRetryProvider` PR next run, backlog pruned (removed the stale GitHubActionsReport/HtmlReport/JUnitReport item).
- **Process recommendation for future runs**: at the very start of each run, before anything else, verify the PR(s) claimed as "created" in the previous run's memory entry and Run History actually exist on GitHub (search by title and by candidate branch name). Do this before starting new Task 2/3 work, since orphaned PRs are otherwise easy to miss for multiple runs in a row (as happened here across both 09-17 and 09-18 simultaneously).


## Run 2026-09-20 (run 35543089018) — recreated RetryExtensions.AddRetryProvider PR

- Task reconciliation: verified via `search_pull_requests`, `list_branches`, and local file/grep checks that **both** the 2026-09-17 (`RetryExtensions.AddRetryProvider`, branch `test-assist/retry-extensions-tests`) and 2026-09-18 (`ActivityWrapper`/`OpenTelemetryEnvironmentVariables`, branch `test-assist/activity-wrapper-and-env-vars-tests-v2`) PRs are confirmed **still missing** from GitHub — no test files (`RetryExtensionsTests.cs`, `OpenTelemetryEnvironmentVariablesTests.cs`) exist in the repo, no matching branches, no matching PRs by title search. This is now the 3rd/4th time this "create_pull_request reports success but the PR never materializes" issue has recurred for these two specific items in a row (2026-09-17→18→19→20).
- Recreated only `RetryExtensions.AddRetryProvider` this run (per the 2026-09-19 run's stated plan) — the `ActivityWrapper`/`OpenTelemetryEnvironmentVariables` item remains outstanding for a future run.
- Added `RetryExtensionsTests.cs` (3 tests): command-line options provider registration (invoked the internal `CommandLineManager._commandLineProviderFactory` list directly via reflection rather than building a full `CommandLineHandler`, since that needs a real `IConfiguration` which is heavier to construct than the test needs); exactly one test-host-orchestrator factory registered and builds a `RetryOrchestrator` (needed `ICommandLineOptions` with `RetryFailedTestsOptionName` set — gates `RetryOrchestrator.IsEnabledAsync` — plus `IFileSystem` registered, since `RetryOrchestrator`'s constructor calls `GetFileSystem()`); and same-instance sharing of `RetryDataConsumer` across lifecycle-callback/data-consumer/session-lifetime-handler (reused the exact construction order and gotchas already documented in the 2026-09-17 entry above: `RetryFailedTestsPipeNameOptionName` must be set for `RetryLifecycleCallbacks.IsEnabledAsync()`, the built lifecycle callback must be registered into the `ServiceProvider` before building data consumers, and the same `alreadyBuiltServices` list must be passed to both `BuildDataConsumersAsync` and `BuildTestSessionLifetimeHandleAsync`).
- Build succeeded (0 warnings/errors after fixing one IDE0028 collection-initialization simplification warning). Targeted suite 3/3 passed; full `Microsoft.Testing.Extensions.UnitTests` net8.0 suite: 1879 total (was 1876), 0 failed, 37 skipped (pre-existing, no regressions). `dotnet format whitespace TestFx.slnx --verify-no-changes --include <file>` clean (only the expected harmless F#-project warning).
- Created PR "Add unit tests for RetryExtensions.AddRetryProvider" on branch `test-assist/retry-extensions-tests-v2` (renamed with `-v2` suffix per the established process recommendation, to avoid a possible silent collision with the still-undiscoverable original branch name) via `create_pull_request` (tool returned `result: success` with a patch bundle).
- Task 5: confirmed only the Monthly Activity issue #10920 itself carries the `testing` label — nothing to comment on this run.
- Task 7: issue #10920 rebuilt cleanly from scratch again (same recurring duplicated-section accumulation pattern as prior runs); kept only the last ~5 Run History entries inline, condensed the rest into this memory file's own history. Suggested Actions set to only the new PR. Backlog refreshed: added the still-outstanding `ActivityWrapper`/`OpenTelemetryEnvironmentVariables` recreation item, kept `RetryDataConsumer`/`RetryLifecycleCallbacks`/`RetryTestHostRunner`/`NamedPipeServerFactory`/`TestingPlatformBuilderHook` as known low-value/deprioritized items.
- **Process note reinforced**: this makes at least 5-6 distinct occurrences of the "create_pull_request reports success but PR never appears on GitHub" discrepancy across the project's history (2026-08-30, 2026-09-02, 2026-09-05, 2026-09-06, 2026-09-19 for two items simultaneously). Continue the standing practice of verifying every previously-claimed PR at the start of each run before starting new Task 2/3 work.
- Remaining candidates for future runs: recreate `ActivityWrapper`/`OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace` tests (highest priority — 2nd run in a row this item has been deferred); otherwise the Retry/GitHubActionsReport/OpenTelemetry veins are largely exhausted of high-value gaps — consider a fresh sweep of Analyzers (not revisited in several weeks) or pivot to Task 5/6 if no new zero-coverage classes are found.
