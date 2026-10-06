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

0. **HangDump IPC serializers — DONE 2026-10-03** (`ActivitySignalRequestSerializer`, `ConsumerPipeNameRequestSerializer`, `GetInProgressTestsRequestSerializer`, `GetInProgressTestsResponseSerializer`): added `HangDumpSerializersTests.cs` (6 tests), same reflection-helper pattern as `RetrySerializersTests.cs`. ServerMode/IPC/Retry/HangDump serializer sweep now fully exhausted — next run should pivot to a fresh area (Task 5/6, or re-scan other extension projects e.g. CrashDump/VideoRecorder for similar zero-coverage serializer/plumbing gaps).
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
| 2026-10-03 | Task 2/3 (HangDump serializer tests: ActivitySignalRequest/ConsumerPipeNameRequest/GetInProgressTestsRequest+Response, 6 tests), Task 7. |
| 2026-10-02 | Task 2/3 (Retry serializer round-trip tests: FailedTestRequest/GetListOfFailedTestsRequest+Response/TestRunCountsRequest/ArtifactRequest, 9 tests), Task 7. |
| 2026-10-01 | Task 2/3 (TcpMessageHandler reset/bare-LF: 2 tests), Task 7. |
| 2026-09-27 | Task 2/3 (ServerModeManager.Build: 3 tests), Task 7. |
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

2026-10-06 UTC (run 37543546908)

## Completed Work (recent, summarized)

Analyzer edge-case PRs from 2026-06 through 2026-08 (MSTEST0007/0024/0028/0029/0035/0038/0044/0047/0054/0061/0063/0070/0074/0076/0077/0078/0081, TestClassShouldBeValid, AvoidOutParameterOnAssertIsInstanceOfTypeFixer, UseExecuteAsyncOverrideFixer, DependsOnShouldBeValidAnalyzer, SharedFileSystemPathInTestAnalyzer) — all merged, analyzer area now saturated (see "Key Test Pattern Notes" above for the lasting gotchas these runs surfaced). PR (2026-08-12) RetryArtifactProcessor.ProcessAsync: 8 tests. PR (2026-08-13) ReportFileWriterHelper.RetryWhenIOExceptionAsync: 4 tests. PR (2026-09-08) MicrosoftExtensionsLoggingBuilderExtensions: 9 tests (gotcha: MEL vs MTP `ILoggerFactory`/`ILogger` namespace clash — use `MtpILogger`/`MtpILoggerFactory` aliases).

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

## Runs 2026-09-16 to 2026-09-22 — condensed summary

PRs from this window (all merged unless noted): RandomIdTests (Microsoft.Testing.Extensions.Retry.RandomId — 4 tests), RetryExtensionsTests (RetryExtensions.AddRetryProvider — 3 tests, required 2 recreation attempts due to a recurring "create_pull_request reports success but PR never appears on GitHub" discrepancy across 2026-09-17→20, confirmed present on `main` as of 2026-09-22), ActivityWrapper/OpenTelemetryEnvironmentVariables gap coverage (also hit the same orphaned-PR discrepancy; final version merged as PR #11451), CrashDumpFileNameHelper.GetDumpSearchPattern (4 tests).

Key lasting gotchas from this window:
- **Orphaned-PR discrepancy** (`create_pull_request` reports `success` but the PR/branch never appears on GitHub) recurred repeatedly (2026-08-30, 09-02, 09-05, 09-06, 09-17 through 09-20 for two items simultaneously) but did NOT recur from 2026-09-21 onward — process fix: always reconcile against local `main` file tree (not just GitHub search) at the start of each run before starting new work, since a shallow clone reliably reflects `main` while GitHub search/branch listing can lag.
- `RetryLifecycleCallbacks.IsEnabledAsync()` gates on the **pipename** option (`RetryFailedTestsPipeNameOptionName`), not `--retry-failed-tests` itself.
- `RetryDataConsumer.InitializeAsync()` requires `RetryLifecycleCallbacks` registered into the `ServiceProvider` before `BuildDataConsumersAsync` is called (mirrors real `TestHostBuilder.Modes.cs` order).
- `BuildDataConsumersAsync`/`BuildTestSessionLifetimeHandleAsync` for `CompositeExtensionFactory<T>`-registered extensions must share the same `alreadyBuiltServices` list across both calls or each creates its own singleton instance.
- `Activity.IsAllDataRequested` is always `true` for a plain `new Activity().Start()` regardless of listeners — need a dedicated `ActivitySource`+`ActivityListener` pair sampling `PropagationData` to get `IsRecording == false`.
- Internal API surface is tracked too: `InternalAPI/InternalAPI.Unshipped.txt` (parallel to `PublicAPI.*.txt`) — adding an internal forwarding method via `InternalsVisibleTo` requires an entry there or RS0051 fails the build.
- Analyzers area reconfirmed saturated (apparent gaps are just filename/project differences, e.g. source-generator variants live in `MSTest.SourceGeneration.UnitTests`). AzureFoundry, GitHubActionsReport/HtmlReport/JUnitReport, RandomId/RetryExtensions all confirmed resolved and removed from backlog.
## Runs 2026-09-23 to 2026-09-30 — condensed summary (ServerMode/IPC sweep)

PRs from this window (all merged): TestHostProcessPIDRequestSerializer, RpcIdParser.TryParseNumericId, DotnetTestHelper (PR #11545), PerRequestServerDataConsumer (PR #11552), ServerModeManager.Build, ServerControlMessage/WaitForServerControlRequest serializers, PassiveNode invalid-params/attachments (PR #11685).

Key lasting gotchas:
- `[Embedded]`-linked source files need explicit `<Compile Include=...>` entries in the test csproj — `InternalsVisibleTo` alone is not sufficient.
- `RpcIdParser` lives in namespace `Microsoft.Testing.Platform.ServerMode`, not the top-level namespace.
- Jsonite/`Json.*` family deprioritized as low-value (trivial wrappers or already covered indirectly).
- Hand-maintained resource accessors (`PlatformResources.cs` `IS_MTP_UNIT_TESTS` block) must be updated when a unit test needs a newly-referenced resource string (hit for `MissingClientPortFoJsonRpc`).
- `ServerModeManager`/ServerMode top-level + IPC serializer + `PassiveNode` sweep now largely exhausted.

## Run 2026-10-06 (run 37543546908) — MSBuild ModuleInfoRequest/RunSummaryInfoRequest serializer tests

- Task reconciliation: PR #11741 (HangDump serializers) and #11750 (FailedTestHelper) merged. PR #11728 (Retry serializers) merged. PR #11767 "Add direct unit tests for MSBuildCompatibilityHelper" is open (state/approved, blocked mergeable_state — likely branch-protection/review gate, not a CI failure) — not actionable by this agent, left for maintainer.
- Task 2/3: swept remaining Platform extension projects for zero-direct-test classes (CrashDump, VideoRecorder, GitHubActionsReport, Hosting, Configuration, Telemetry, OpenTelemetry, AzureFoundry all confirmed already covered). Found `ModuleInfoRequestSerializer` and `RunSummaryInfoRequestSerializer` (`src/Platform/Microsoft.Testing.Extensions.MSBuild/Serializers/`) — MSBuild IPC serializers analogous to the already-covered Retry/HangDump ones — had zero direct tests (only `FailedTestInfoRequestSerializer` was covered, via `FailedTestHelperTests.cs` indirectly through `FailedTestInfoRequest`).
- Added `MSBuildSerializersTests.cs` (7 tests) in `Microsoft.Testing.Platform.MSBuild.UnitTests`: serializer ID assertions, `ModuleInfoRequest` round-trip (all fields + empty strings), `RunSummaryInfoRequest` round-trip (all fields, null-duration→empty-string normalization, AllowSkipped=false). Reused the `extern alias PlatformMSBuild` + reflection-based Serialize/Deserialize invocation pattern from `RetrySerializersTests.cs`/`HangDumpSerializersTests.cs` (serializer base type is `[Embedded]`, source-linked into both `Microsoft.Testing.Extensions.MSBuild` and `Microsoft.Testing.Platform.MSBuild`).
- Build succeeded (0 warnings) net8.0+net9.0. Full `Microsoft.Testing.Platform.MSBuild.UnitTests` suite: 219 total (was 216), 0 failed, 1 skipped (pre-existing), no regressions. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for MSBuild ModuleInfoRequest/RunSummaryInfoRequest serializers" on branch `test-assist/msbuild-serializers-tests`.
- Task 7: updated October issue #11698 — new Run History entry prepended, added new PR to Suggested Actions, noted #11728 (Retry serializers) merged, and #11767 (MSBuildCompatibilityHelper) is open+approved but blocked (not actionable here).
- Remaining candidates for future runs: MSTest.Engine internal classes (architecturally blocked, unchanged); `Microsoft.Testing.Platform.MSBuild` + `Microsoft.Testing.Extensions.MSBuild` area now considered well-covered (FailedTestHelper, MSBuildCompatibilityHelper, and both MSBuild serializers done); broader `src/Platform/Microsoft.Testing.Platform` core (Messages/, Requests/, OutputDevice/Terminal/, Logging/, Configurations/, Helpers/) has many zero-direct-test internal classes but most are large/complex orchestration types (TestFrameworkManager, OutputDeviceManager, terminal rendering) better suited for a dedicated future deep-dive rather than a quick sweep; pivot next run to Task 5 (issue comments) or Task 6 (test infrastructure) given the extension-project low-hanging fruit is now largely exhausted.

## Run 2026-10-05 (run 37385432650) — MSBuildCompatibilityHelper tests

- Task reconciliation: no open `[test-improver]`-prefixed PRs found (search returned 0 open; PRs #11741/#11728/#11685/etc. all previously merged/closed) — nothing to maintain this run.
- Task 2/3: picked up standing backlog item `MSBuildCompatibilityHelper` (`src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/MSBuildCompatibilityHelper.cs`) — had zero direct tests.
- Added `MSBuildCompatibilityHelperTests.cs` (3 tests): `SupportsMultiLine`/`SupportsTerminalLoggerWithExtendedMessages` assert `true` against the real loaded MSBuild 17.11.48 assembly (repo-pinned version already exceeds the 17.10.0 feature thresholds — faking an older MSBuild version isn't practical, documented via comment instead); `TryWriteExtendedMessage` happy-path test uses a mocked `IBuildEngine` (same Moq pattern as `MSBuildTests.cs`) and asserts the logged `ExtendedBuildMessageEventArgs` type/message/importance/metadata.
- Verified via scratch console app that `Features.CheckFeatureAvailability("TerminalLogger_MultiLineHandler")` returns `Available` and `AssemblyFileVersionAttribute` on `Microsoft.Build.Framework` resolves to `17.11.48.46605` in this sandbox — confirms the "Supports*" tests exercise the true positive branch deterministically.
- Build succeeded (0 warnings) net8.0+net9.0. Full `Microsoft.Testing.Platform.MSBuild.UnitTests` suite: 216 total (was 213), 0 failed, 1 skipped (pre-existing), no regressions. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for MSBuildCompatibilityHelper" on branch `test-assist/msbuild-compatibility-helper-tests`.
- Task 7: updated October issue #11698 — new Run History entry prepended, added new PR to Suggested Actions, removed the now-addressed MSBuildCompatibilityHelper backlog item.
- Remaining candidates for future runs: MSTest.Engine internal classes (architecturally blocked, unchanged); other report-provider command-line providers not yet swept; consider Task 5 (issue comments) or Task 6 (test infrastructure) next run since MSBuild platform area is now well-covered.

## Run 2026-10-04 (run 37241816633) — MSBuild FailedTestHelper tests

- Task reconciliation: no open `[test-improver]`-prefixed PRs needed maintenance; PR #11741 (HangDump serializers) is green/approved by Copilot reviewer, awaiting maintainer merge. PR #11728 (Retry serializers) was merged; PR #11708 (TcpMessageHandler) was merged.
- Task 2/3: ServerMode/IPC/Retry/HangDump serializer sweep confirmed exhausted (checked CrashDump/VideoRecorder — no named-pipe serializers there; checked Configuration/AI/OpenTelemetry extension areas — all already covered). Found `FailedTestHelper.FromFailedTest` (`src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/FailedTestHelper.cs`) — builds the MSBuild failed-test error message/location — had zero direct tests.
- Added `FailedTestHelperTests.cs` (9 tests): stack-trace location resolution + CodeFilePath/targetPath fallbacks, canceled vs failed error code, multiline message (expected/actual/stack trace sections), non-multiline message (lowPriorityMessage, single-line stripping, place omission, long-message shortening).
- **New gotcha**: `FailedTestInfoRequest` (in `Microsoft.Testing.Extensions.MSBuild.Serializers` namespace) is source-linked (`<Compile Include>`) into both `Microsoft.Testing.Extensions.MSBuild.csproj` and `Microsoft.Testing.Platform.MSBuild.csproj`. The `Microsoft.Testing.Platform.MSBuild.UnitTests` test project references both assemblies unaliased, so an unaliased reference to this type is CS0433-ambiguous. Fix: alias the `Microsoft.Testing.Platform.MSBuild` ProjectReference with `Aliases="global,PlatformMSBuild"` (keeps it usable unaliased for other files) and in the new test file add `extern alias PlatformMSBuild;` + `using FailedTestInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.FailedTestInfoRequest;`.
- MSTEST0046 requires `Assert.Contains(substring, value)` (not `StringAssert.Contains(value, substring)`) for new substring checks in this repo; MSTEST0037 requires `Assert.IsLessThanOrEqualTo(upperBound, value)` instead of `Assert.IsTrue(value <= bound)`.
- Build succeeded (0 warnings) net8.0+net9.0. Full `Microsoft.Testing.Platform.MSBuild.UnitTests` suite: 148 total (was 139), 0 failed, 1 skipped (pre-existing, platform-gated), no regressions on both TFMs. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for FailedTestHelper.FromFailedTest" on branch `test-assist/msbuild-failed-test-helper-tests`.
- Task 7: updated October issue #11698 — new Run History entry prepended, added new PR to Suggested Actions, refined backlog (MSBuild area: FailedTestHelper done, MSBuildCompatibilityHelper remains a candidate — needs faking MSBuild's assembly version / Features.CheckFeatureAvailability, moderate effort).
- Remaining candidates for future runs: `MSBuildCompatibilityHelper` (version/feature-gate helper in same project); MSTest.Engine internal classes (architecturally blocked, unchanged); other report-provider command-line providers not yet swept.

## Run 2026-10-03 (run 37162750314) — HangDump named-pipe serializer tests

- Task reconciliation: no open `[test-improver]`-prefixed PRs needed maintenance (PR #11708 Retry serializers still pending review).
- Task 2/3: picked up standing backlog item (HangDump IPC serializers) — found `ActivitySignalRequestSerializer`, `ConsumerPipeNameRequestSerializer`, `GetInProgressTestsRequestSerializer`, `GetInProgressTestsResponseSerializer` (all in `src/Platform/Microsoft.Testing.Extensions.HangDump/Serializers/`) had zero direct serializer-level tests.
- Added `HangDumpSerializersTests.cs` (6 tests): singleton round-trip, pipe-name round-trip, empty-record request, response with multiple entries/empty array/Unicode test name.
- Build succeeded (0 warnings). Full `Microsoft.Testing.Extensions.UnitTests` net9.0 suite: 2081 total (was 2075), 0 failed, 51 skipped (pre-existing), no regressions. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for HangDump named-pipe serializers" on branch `test-assist/hangdump-serializer-tests`.
- Task 7: updated October issue #11698 — new Run History entry prepended, added new PR to Suggested Actions, removed the now-addressed HangDump backlog item, noted ServerMode/IPC/Retry/HangDump sweep as fully exhausted.
- Remaining candidates for future runs: MSTest.Engine internal classes (architecturally blocked, unchanged); pivot to Task 5 (issue comments) or Task 6 (test infrastructure), or re-scan other extension projects (CrashDump, VideoRecorder) for similar thin-plumbing zero-coverage gaps.

## Run 2026-10-02 (run 37075004936) — Retry named-pipe serializer tests

- Task reconciliation: no open `[test-improver]`-prefixed PRs; PR #11708 (TcpMessageHandler, from run 36937877564) was merged by maintainer on 2026-10-02.
- Task 2/3: picked up standing backlog item (HangDump/Retry IPC serializers, flagged "low priority, thin plumbing" but never actually checked) — found `FailedTestRequest`, `GetListOfFailedTestsRequest`/`Response`, `TestRunCountsRequest`, `ArtifactRequest` (all in `src/Platform/Microsoft.Testing.Extensions.Retry/Serializers/`) had zero direct serializer-level tests (only incidental exercise via `RetryTests.cs` pipe-client/server integration tests).
- Added `RetrySerializersTests.cs` (9 tests): round-trip tests for all 5 serializer types, including empty-array/null-kind/empty-recovered-uids edge cases.
- **New gotcha**: `NamedPipeSerializer<T>`/`INamedPipeSerializer` are `[Embedded]` linked-source types — each consuming project (`Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.Retry`, etc.) compiles its own private copy via `<Compile Include>` linking, so they are NOT type-identical across assemblies even with `InternalsVisibleTo`/ProjectReference. A test project referencing `Microsoft.Testing.Extensions.Retry` cannot declare a parameter of type `NamedPipeSerializer<T>` or `INamedPipeSerializer` and pass a `Retry`-assembly serializer instance to it (CS1503). Fix: use the same reflection-based `Serialize`/`Deserialize` invocation pattern as `Microsoft.Testing.Platform.UnitTests`' `ProtocolSerializerTestHelper` (reach the non-public instance methods via `GetMethods(...).Single(...)`), not a shared generic helper typed on the embedded base class.
- Build succeeded (0 warnings). Full `Microsoft.Testing.Extensions.UnitTests` net9.0 suite: 2075 total (was 2066), 0 failed, 51 skipped (pre-existing), no regressions. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for Retry named-pipe serializers" on branch `test-assist/retry-serializer-tests`.
- Task 7: updated October issue #11698 — new Run History entry prepended, added new PR to Suggested Actions, refined backlog item (HangDump/Retry IPC serializers → Retry done, HangDump still open for a future run).
- Remaining candidates for future runs: HangDump IPC serializers (`ActivitySignalRequestSerializer`, `ConsumerPipeNameSerializer`, `GetInProgressTestsRequest`/`Response` in `src/Platform/Microsoft.Testing.Extensions.HangDump/Serializers/`) — same zero-direct-test gap, same reflection-helper pattern should apply; MSTest.Engine internal classes (architecturally blocked, unchanged).

## Run 2026-10-01 (run 36937877564) — TcpMessageHandler reset/bare-LF tests

- Task reconciliation: confirmed PR #11685 (PassiveNode) was merged by maintainer; no open `[test-improver]`-prefixed PRs needed maintenance.
- Task 2/3: picked up standing backlog item `TcpMessageHandler` edge cases beyond existing `TcpMessageHandlerTests.cs` — found the `IOException`-wrapping-`SocketException` catch-filter arm (the shape a real `NetworkStream` reset actually throws) and the documented bare-LF header-terminator tolerance both had zero coverage.
- Added 2 tests to `TcpMessageHandlerTests.cs` (`ReadAsync_IOExceptionWrappingConnectionReset_ReturnsNull`, `ReadAsync_BareLineFeedHeaderTerminator_IsTolerated`) plus a small `ThrowingStream` helper (parallel to existing `ConnectionResetStream`).
- Build succeeded (0 warnings). Full `Microsoft.Testing.Platform.UnitTests` net8.0 suite: 2776 total (was 2774), 0 failed, 22 skipped (pre-existing), no regressions. `dotnet format whitespace --verify-no-changes` clean.
- Created PR "Add unit tests for TcpMessageHandler IOException-wrapped reset and bare-LF tolerance" on branch `test-assist/tcp-message-handler-tests`.
- Task 7: closed September issue #10920, created October issue; Run History entry added for this run.
- Remaining candidates for future runs: HangDump/Retry IPC serializers (low priority, thin plumbing); MSTest.Engine internal classes (architecturally blocked); consider pivoting to a fresh area (Retry/HangDump extensions, or Task 5/6) if ServerMode/IPC area yields nothing new next run.
