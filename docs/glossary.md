# Glossary

This glossary defines key terms and concepts used throughout the MSTest and Microsoft.Testing.Platform (MTP) documentation and codebase.

## A

### ArchitectureConditionAttribute

An MSTest attribute (`[ArchitectureConditionAttribute]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that conditionally controls whether a test class or test method runs based on the current process architecture. Available only on .NET (not .NET Framework). Accepts a [ConditionMode](#conditionmode) argument and a [TestArchitectures](#testarchitectures) flags value; the single-argument overload defaults to `ConditionMode.Include`. Detection uses `RuntimeInformation.ProcessArchitecture`. The attribute is not inherited — applying it to a base class does not affect derived classes. Because its `GroupName` (`"ArchitectureCondition"`) differs from that of [OSConditionAttribute](#osconditionattribute) (`"OSCondition"`), the two compose with logical AND, making it easy to gate a test on both OS and architecture:

```csharp
[TestMethod, OSCondition(OperatingSystems.Windows), ArchitectureCondition(TestArchitectures.X64)]
public void RunsOnlyOnWindowsX64() { }
```

Introduced in [PR #9233](https://github.com/microsoft/testfx/pull/9233). Inherits from [ConditionBaseAttribute](#conditionbaseattribute).

### AwesomeAssertions

A .NET fluent-assertion library ([NuGet: AwesomeAssertions](https://www.nuget.org/packages/AwesomeAssertions)) used in some unit and integration test suites in this repository. It provides a human-readable, chainable assertion API (e.g., `result.Should().Be(42)`). AwesomeAssertions is the community-maintained fork of `FluentAssertions` created after FluentAssertions changed its license from Apache 2.0 to a commercial model; the two libraries share the same API surface. Assertion policy in this repository varies by project: some `BannedSymbols.txt` files require `AwesomeAssertions` instead of the built-in MSTest `Assert`, `CollectionAssert`, and `StringAssert` types, while others ban `AwesomeAssertions`.

### ArgumentArity

An MTP struct (`ArgumentArity.cs`) that defines the minimum and maximum number of values a command-line option accepts. Provides five predefined constants: `Zero` (0,0), `ZeroOrOne` (0,1), `ZeroOrMore` (0,∞), `ExactlyOne` (1,1), and `OneOrMore` (1,∞). Used by `ICommandLineOptionsProvider` implementations to declare option shapes.

### ArtifactNamingHelper

A shared static helper compiled into MTP extensions via file linking (no NuGet service registration or InternalsVisibleTo required) that provides template-based naming for test artifact files (dump files, report files, etc.). Templates are strings containing `{placeholder}` tokens (case-sensitive, lowercase): `{pname}` (process name), `{pid}` (process ID), `{asm}` (entry-assembly name), `{tfm}` (target framework moniker, best-effort runtime detection), `{arch}` (process architecture), and `{time}` (high-precision UTC timestamp). Custom per-call overrides can replace default placeholder values via a `Dictionary<string, string>`. Used directly by the [HangDump](#hangdump) and [CrashDump](#crashdump) extensions, and indirectly by the report extensions ([HtmlReport](#htmlreport), [JUnitReport](#junitreport), and [TrxReport](#trxreport)) via the shared `ReportFileNameHelper`. The legacy `%p` pattern is not handled here; it is substituted by the [HangDump](#hangdump) extension as a separate post-processing step for backward compatibility. The [CrashDump](#crashdump) consumer passes the .NET runtime's `%e` and `%p` placeholders as the `processName` and `processId` arguments so `{pname}` and `{pid}` resolve to `%e` and `%p` respectively — those are then expanded by the runtime's `createdump` at crash-write time (the testhost PID is not yet known when the environment variables are configured).

### AssemblyFixtureProviderAttribute

An MSTest assembly-level attribute (`[assembly: AssemblyFixtureProvider(typeof(T))]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that enables cross-assembly assembly fixtures. When applied to a library assembly, it causes any `[AssemblyInitialize]` and `[AssemblyCleanup]` methods on the specified `FixtureType` to be discovered and executed once per consuming test assembly that loads the library at runtime. This allows shared test infrastructure (e.g., database setup, container lifecycle) to be co-located in a test-helper library rather than repeated across every test project. Local `[AssemblyInitialize]`/`[AssemblyCleanup]` declarations always take precedence over provider-contributed ones; the attribute may be applied multiple times on the same assembly to expose multiple fixture types; and it can also be applied on the consuming test assembly to opt into fixtures from a third-party library. Introduced in [PR #8677](https://github.com/microsoft/testfx/pull/8677).

### Assert.Scope (soft assertions)

An experimental MSTest feature (`[MSTESTEXP]`) that defers assertion failures instead of throwing immediately. Calling `Assert.Scope()` returns an `IDisposable` scope; while the scope is active, assertion failures are collected rather than thrown. When the scope is disposed, all collected failures are reported together as a single `AssertFailedException`. Nesting scopes is not allowed. `Assert.Fail()` and `Assert.Inconclusive()` still throw immediately inside a scope; all other assertions participate in soft collection. Use this to check multiple conditions in one test pass without stopping on the first failure:

```csharp
using (Assert.Scope())
{
    Assert.AreEqual(1, actual.X);  // collected, execution continues
    Assert.AreEqual(2, actual.Y);  // collected, execution continues
}
// Dispose() throws a single AssertFailedException containing all failures
```

See `docs/RFCs/011-Soft-Assertions-Nullability-Design.md` for the nullability-annotation design decisions.

### AzureDevOpsReport

An MTP extension (`Microsoft.Testing.Extensions.AzureDevOpsReport`) that formats and reports test results to Azure DevOps pipelines. It generates pipeline-compatible output including TFM and test name details for richer CI reporting. Its optional Extensions-tab Markdown summary is aggregated across modules through artifact post-processing when the SDK advertises the required capability; live publishing to the Azure DevOps Tests tab remains a separate path. When using [MSTest.Sdk](#mstestsdk), opt in with `<EnableMicrosoftTestingExtensionsAzureDevOpsReport>true</EnableMicrosoftTestingExtensionsAzureDevOpsReport>`; the extension is enabled automatically by the `AllMicrosoft` profile. It is not supported in NativeAOT mode (MSTest.Sdk emits a build warning) or VSTest mode.

### Aspire testing

The `Aspire.Hosting.Testing` integration for testing .NET Aspire distributed applications. When using [MSTest.Sdk](#mstestsdk), set `<EnableAspireTesting>true</EnableAspireTesting>` to add the package and its implicit using. This feature is supported in the default MSTest runner and VSTest modes, but not when `PublishAot=true`; MSTest.Sdk emits a build error for that combination.

### AzureFoundry

An MTP extension (`Microsoft.Testing.Extensions.AzureFoundry`) that integrates [Azure AI Foundry](https://azure.microsoft.com/products/ai-foundry) (Azure OpenAI) with Microsoft.Testing.Platform as an [IChatClientProvider](#ichatclientprovider) implementation. It reads Azure OpenAI connection settings from three environment variables — `AZURE_OPENAI_ENDPOINT` (required), `AZURE_OPENAI_DEPLOYMENT_NAME` (required), and `AZURE_OPENAI_API_KEY` (optional) — and supplies AI chat-client capabilities to any testing extension that consumes the [Microsoft.Testing.Platform.AI](#microsofttestingplatformai) abstractions. Authentication uses `DefaultAzureCredential` (Managed Identity, Workload Identity, Azure CLI, Visual Studio, and other credential-chain sources) by default, which is recommended for Azure-hosted scenarios so no secret needs to be provisioned; providing `AZURE_OPENAI_API_KEY` switches to key-based authentication instead. To target a specific user-assigned managed identity, set `AZURE_CLIENT_ID` to its client ID. This is the reference implementation of the `Microsoft.Testing.Platform.AI` abstractions.

## C

### CIConditionAttribute

An MSTest attribute (`[CIConditionAttribute]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that conditionally controls whether a test class or test method runs based on whether the test is executing in a CI environment. Accepts a `ConditionMode` argument: `Include` (run only in CI) or `Exclude` (skip in CI). Detection is delegated to `CIEnvironmentDetector`, which checks well-known CI environment variables (e.g., `CI`, `TF_BUILD`). The attribute is not inherited — applying it to a base class does not affect derived classes. Inherits from [ConditionBaseAttribute](#conditionbaseattribute).

### CodeCoverage

An MTP extension (`Microsoft.Testing.Extensions.CodeCoverage`) that instruments .NET assemblies and collects code-coverage data during a test run. It is developed and maintained in the `devdiv/DevDiv/vs-code-coverage` repository and consumed by this project as a Maestro-managed dependency. The extension supports the `--coverage` command-line option; VSTest-compatible `--collect "XPlat Code Coverage"` and `--collect "Code Coverage"` forms are proposed via a [command-line option mapping](#commandlineoptionmapping) (see `docs/RFCs/015-Command-Line-Option-Mappings.md`).

### CommandLineOptionMapping

A proposed MTP extensibility point (see `docs/RFCs/015-Command-Line-Option-Mappings.md`) that would let an extension declaratively accept a user-facing option (e.g. `--collect "XPlat Code Coverage"`) and rewrite it at parse time into one or more first-class MTP options (e.g. `--coverage`). In RFC 015, this is expressed via `ICommandLineOptionMappingProvider`. Intended to smooth migration from VSTest by allowing legacy `--logger` and `--collect` argument forms to be forwarded to their MTP equivalents without polluting the canonical MTP option set.

### ConditionBaseAttribute

An abstract MSTest attribute base class in `Microsoft.VisualStudio.TestTools.UnitTesting` for implementing custom conditional test execution. Derived attributes override `IsConditionMet` (returns `true` when the condition is met) and `GroupName` (used to group multiple condition attributes on the same test). Multiple `ConditionBaseAttribute`-derived attributes are evaluated with OR logic within a group and AND logic across groups: a test is skipped only if every attribute in at least one group evaluates to `false`. The `IgnoreMessage` property supplies the skip reason displayed in test output. Built-in concrete implementations include [ArchitectureConditionAttribute](#architectureconditionattribute), [CIConditionAttribute](#ciconditionattribute), [ExecutableConditionAttribute](#executableconditionattribute), [MemberConditionAttribute](#memberconditionattribute), [OSConditionAttribute](#osconditionattribute), and `IgnoreAttribute`.

### ConditionMode

A public enum in `Microsoft.VisualStudio.TestTools.UnitTesting` used with [ConditionBaseAttribute](#conditionbaseattribute)-derived attributes to control whether the condition is reversed. `Include` (default): run the test only when the condition is met. `Exclude`: skip (ignore) the test when the condition is met, reversing the condition.

### CoverageAggregation

A public enum in `Microsoft.Testing.Platform.Extensions.Messages` (append-only, treat unrecognized values non-exhaustively) that specifies how per-scope coverage values are combined when evaluating a threshold. Values: `None` (0, not an aggregate — a single scope's own value), `Total` (aggregate covered / aggregate coverable across the population), `Minimum` (worst scope), `Average` (mean of per-scope percentages), `Maximum` (best scope). Used by [TestCoverageThresholdMessage](#testcoveragethresholdmessage) together with `AggregatedOver` (a [CoverageScopeLevel](#coveragescopelevel)) to express the threshold evaluation: for example, `Minimum` over `Module` is a distinct evaluation from `Minimum` over `File`. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageMetric

A public enum in `Microsoft.Testing.Platform.Extensions.Messages` (closed but append-only; consumers must handle unknown values non-exhaustively) that identifies a code-coverage metric. Well-known values: `Line` (0), `Statement` (1), `Branch` (2), `Method` (3), `Function` (4), `Block` (5, the primary metric of Microsoft.CodeCoverage / dotnet-coverage), `Instruction` (6, JaCoCo), `Region` (7, llvm-cov), `Class` (8, JaCoCo / PHPUnit), `Condition` (9), `Complexity` (10, JaCoCo cyclomatic complexity), and `Custom` (255, escape hatch for proprietary metrics such as MC/DC — carries its identifier in `CustomMetricName`). Used by [TestCoverageMessage](#testcoveragemessage) and [TestCoverageThresholdMessage](#testcoveragethresholdmessage). Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageMetricResult

A public sealed class in `Microsoft.Testing.Platform.Services` that represents a single correlated coverage measurement (one [CoverageMetric](#coveragemetric) for one [CoverageScope](#coveragescope)) exposed through [ITestCoverageResult](#itestcoverageresult). Carries `CoveredCount` and `CoverableCount` as `long` values; `Percentage` is derived (`CoveredCount / CoverableCount * 100`, or 0 when nothing is coverable). Also carries `ProducerId` (the collector), `Metric`, and optionally `CustomMetricName` when `Metric == CoverageMetric.Custom`. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageReportFormat

A public enum in `Microsoft.Testing.Platform.Extensions.Messages` that identifies the on-disk format of a coverage report artifact referenced by [TestCoverageReportMessage](#testcoveragereportmessage) and [CoverageReportReference](#coveragereportreference). Values: `Unknown` (0), `Cobertura` (1), `OpenCover` (2), `Lcov` (3), `CoverageXml` (4, Microsoft.CodeCoverage XML), `Custom` (255, proprietary format identified by `CustomFormatName`). Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageReportReference

A public sealed class in `Microsoft.Testing.Platform.Services` that is a pointer to a rich coverage report artifact, correlated through [ITestCoverageResult](#itestcoverageresult). Carries the report's `SessionUid`, `Path`, [CoverageReportFormat](#coveragereportformat), and collector `ProducerId`, plus an optional `CustomFormatName` when the format is `Custom`. Deep consumers (HTML / UI report generators) parse the referenced artifact for per-line data that the summary intentionally omits. Populated from [TestCoverageReportMessage](#testcoveragereportmessage) by the platform's coverage result aggregator. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageScope

A public readonly struct in `Microsoft.Testing.Platform.Extensions.Messages` that identifies the entity a coverage measurement or threshold entry describes. Combines a [CoverageScopeLevel](#coveragescopelevel) and an optional string `Name` (module path, type name, file path, etc.; `null` only for `Overall`). An optional `ContainerHint` carries a non-authoritative UI grouping hint (e.g. the enclosing module for a namespace scope) used by report generators; it is not part of scope identity or exact containment. The static `CoverageScope.Overall` convenience member creates an `Overall`-level scope. Used by [TestCoverageMessage](#testcoveragemessage), [TestCoverageThresholdMessage](#testcoveragethresholdmessage), and [CoverageScopeSummary](#coveragescopesummary). Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageScopeLevel

A public enum in `Microsoft.Testing.Platform.Extensions.Messages` (append-only) that specifies the granularity of a [CoverageScope](#coveragescope). Values: `Overall` (0, the whole run), `Module` (1, an assembly file on disk), `Assembly` (2), `Namespace` (3), `Type` (4), `File` (5, a source file). Used with [CoverageScope](#coveragescope) and as the `AggregatedOver` parameter of [TestCoverageThresholdMessage](#testcoveragethresholdmessage). Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CoverageScopeSummary

A public sealed class in `Microsoft.Testing.Platform.Services` that aggregates all coverage metrics for a single [CoverageScope](#coveragescope) in a single session. It is the primary summary-layer surface for report generators: one instance per scope, with every [CoverageMetricResult](#coveragemetricresult) that scope reported. Exposes an indexer `summary[CoverageMetric.Line]` for convenient well-known metric lookup (returns `null` if absent; throws for `CoverageMetric.Custom` — use `GetCustom(name)` instead). Populated and exposed through [ITestCoverageResult](#itestcoverageresult). Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### CrashDump

An MTP extension (`Microsoft.Testing.Extensions.CrashDump`) that automatically captures a process memory dump when the test host crashes. Useful for diagnosing unexpected process termination during test runs.

### CtrfReport

An MTP extension (`Microsoft.Testing.Extensions.CtrfReport`) that generates a [CTRF](https://github.com/ctrf-io/ctrf) (Common Test Report Format) JSON report at the end of a test run. CTRF is a vendor-neutral open standard for structured test results, consumed by GitHub Actions test-summary tools, Slack/Teams notifiers, dashboards, and other CI tooling. Enable via `--report-ctrf`; override the output filename with `--report-ctrf-filename`. When using [MSTest.Sdk](#mstestsdk), opt in with `<EnableMicrosoftTestingExtensionsCtrfReport>true</EnableMicrosoftTestingExtensionsCtrfReport>`. Currently **experimental** — the CLI options and output format may change without notice.

## D

### DelayBackoffType

A public enum in the `Microsoft.VisualStudio.TestTools.UnitTesting` namespace that specifies the delay strategy used between retries by the `[Retry]` attribute. Values: `Constant` (fixed delay between each attempt) and `Exponential` (delay doubles with each attempt: base × 2^(n−1)).

### DynamicData

An MSTest attribute (`[DynamicData]`) for data-driven tests where test data is sourced from a static property, method, or field rather than inline `[DataRow]` values. The data source name is passed as a constructor argument; `DynamicDataSourceType` controls how the source is located (`Property`, `Method`, `Field`, or `AutoDetect`). Unlike `[DataRow]`, a single `[DynamicData]` source can be shared across multiple test methods and can produce any number of test cases at runtime. See `docs/RFCs/006-DynamicData-Attribute.md` for the original design.

## E

### ExecutableConditionAttribute

An MSTest attribute (`[ExecutableConditionAttribute]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that conditionally controls whether a test class or test method runs based on executable availability. The presence-only overload (`[ExecutableCondition("dotnet")]`) resolves a command through `PATH` and, on Windows, `PATHEXT`; the command overload (`[ExecutableCondition("dotnet", "--info")]`) runs the executable with the supplied arguments and requires exit code 0. `TimeoutSeconds` controls how long command execution may take and defaults to 30 seconds. Each distinct executable, arguments, and `ConditionMode` combination has its own `GroupName`, so different commands or opposite modes compose with logical AND while repeated attributes for the same command and mode compose with logical OR. Presence checks and command executions use distinct group and cache keys, and command cache keys also include the timeout. The attribute is not inherited, and executable availability is evaluated immediately before test execution rather than validated statically. Introduced in [PR #9369](https://github.com/microsoft/testfx/pull/9369). Inherits from [ConditionBaseAttribute](#conditionbaseattribute).

## F

### FQN (Fully Qualified Name)

A unique string that identifies a test by its complete namespace, class, and method path (e.g., `MyNamespace.MyClass.MyTestMethod`). Used in IDE integration and JSON-RPC protocol messages to unambiguously reference individual tests.

### Formal Verification (FV)

The practice of using formal mathematical proofs to establish correctness properties of code. In this project, FV uses [Lean 4](#lean-4) to prove properties about selected MTP components (see [FV Target](#fv-target)). FV artifacts live in the `formal-verification/` directory.

### FV Target

A specific code component (function, struct, or class) selected for formal verification. Each FV target progresses through defined phases: (1) identified, (2) informal spec extracted, (3) Lean 4 formal spec written, (4) implementation model extracted, (5) proofs completed. Current targets are listed in `formal-verification/TARGETS.md`.

## G

### GitHubActionsReport

An MTP extension (`Microsoft.Testing.Extensions.GitHubActionsReport`) that emits GitHub Actions-native workflow commands so test runs on GitHub Actions produce a first-class CI experience. The extension activates only when the test run executes inside GitHub Actions (`GITHUB_ACTIONS=true`) and the `--report-gh` master switch is passed; it no-ops otherwise. When active, it provides five independently toggleable features:

- **Per-assembly log groups** (`--report-gh-groups`): emits `::group::`/`::endgroup::` workflow commands so each test assembly's output is collapsed by default in the runner UI.
- **Failure and skip annotations** (`--report-gh-annotations`): emits a `::error` workflow command for each failing test and a `::warning` workflow command for each skipped test; both surface in the workflow **Annotations** tab. The `file:line` location is taken from the failure's exception stack trace when a frame resolves inside the workspace, and otherwise from the test's own `TestFileLocationProperty` (the only location available for a skipped test), so annotations also appear on the PR "Files changed" diff gutter. `TestFileLocationProperty` is populated by the MSTest adapter and by the [VSTestBridge](#vstestbridge), which covers xUnit/NUnit and other bridged frameworks that report source information.
- **Job summary** (`--report-gh-step-summary`): writes a markdown roll-up to the file pointed to by `GITHUB_STEP_SUMMARY`, which GitHub renders on the workflow run summary page. Set it to `on-failure` to write only when the test invocation fails. `--report-gh-step-summary-sections` independently selects `test-results`, `coverage`, `slow-tests`, or any combination (`all`, the default); values may be repeated, space-separated, or comma-separated. A capable SDK aggregates multi-module `dotnet test` runs into one authoritative overall section with per-assembly detail through artifact post-processing; mixed module selections are combined so the aggregate includes every requested section, and older fragments retain full output. Each failed test is expanded into a collapsible `<details>` section carrying its failure message, exception type, source location and stack trace; turn this off with `--report-gh-failure-details off` to keep the compact one-line-per-failure list. Diagnostics are bounded on four axes — message and stack trace are each clipped by length *and* by line count, the failure list is capped, and expansion stops once the shared UTF-8 byte budget is spent — so this reporter's own contribution stays well inside GitHub's 1 MiB limit no matter how many test projects write to it, and every truncation is stated in the rendered output. The remaining headroom absorbs what other steps and test frameworks append to the same file, which this reporter can observe but not control.
- **Slow-test notices** (`--report-gh-slow-test-notices`): emits a `::notice` workflow command for any test running past a configured threshold (default 60 seconds; set with `--report-gh-slow-test-threshold`).
- **Test history** (`--report-gh-history`): reads and updates a bounded, versioned JSON snapshot at a workflow-managed local path. Prior default-branch results can then decorate failures with historical pass/fail context without exposing GitHub credentials to the test process. `--report-gh-history-window` controls the retained 1–90 day window (30 days by default).

When using [MSTest.Sdk](#mstestsdk), opt in with `<EnableMicrosoftTestingExtensionsGitHubActionsReport>true</EnableMicrosoftTestingExtensionsGitHubActionsReport>`; the extension is enabled automatically when `TestingExtensionsProfile` is set to `AllMicrosoft`. It is not supported in NativeAOT mode (MSTest.Sdk emits a build warning) or VSTest mode. Introduced in [PR #9541](https://github.com/microsoft/testfx/pull/9541); skipped-test `::warning` annotations were added in [PR #9641](https://github.com/microsoft/testfx/pull/9641).

## H

### HangDump

An MTP extension (`Microsoft.Testing.Extensions.HangDump`) that captures a process memory dump when a test exceeds a configured timeout. Helps diagnose deadlocks, infinite loops, or unexpectedly slow tests.

### HtmlReport

An MTP extension (`Microsoft.Testing.Extensions.HtmlReport`) that generates a self-contained HTML test report at the end of a test session. The report inlines all CSS, JavaScript, and test data into a single `.html` file with no external dependencies, making it suitable for archiving as a CI artifact, attaching to PR comments, or sharing by email. Features include failed-test-first ordering, free-text search, sort/filter by outcome or duration, an expandable per-test detail panel (error message, stack trace, stdout/stderr), and automatic light/dark theme following the system preference. Pagination keeps the report usable for very large test runs. Currently **experimental** — CLI option, layout, and on-disk format may change without notice. Enable via the `--report-html` CLI option.

## I

### IChatClientProvider

An MTP interface (`Microsoft.Testing.Platform.AI.IChatClientProvider`) that defines the contract for AI provider integrations in the testing platform. Exposes four members: `IsAvailable` (whether required configuration, e.g. environment variables, is present), `HasToolsCapability` (whether the provider supports tool/function calling, e.g. MCP tools), `ModelName` (the model in use), and `CreateChatClientAsync` (factory that returns an `IChatClient` from [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)). Extensions that need AI capabilities consume an injected `IChatClientProvider` rather than implementing provider-specific logic themselves. The interface is shipped in [Microsoft.Testing.Platform.AI](#microsofttestingplatformai); the reference implementation is [AzureFoundry](#azurefoundry).

### Informal Spec (FV)

An intermediate artifact in the [Formal Verification (FV)](#formal-verification-fv) workflow that documents the behavioural properties of an [FV Target](#fv-target) in plain English (or structured natural language), before writing formal [Lean 4](#lean-4) proofs. An informal spec lists preconditions, postconditions, edge-case expectations, and any confirmed bugs discovered during analysis. It corresponds to Phase 2 of the FV target lifecycle and lives in the `formal-verification/specs/` directory.

### IsTestingPlatformApplication

An MSBuild property (`<IsTestingPlatformApplication>true</IsTestingPlatformApplication>`) that marks a project as an MTP test application. When set, the project builds into a self-contained test runner executable rather than a class library consumed by a separate test host.

### ITestCoverageCapabilities

A public interface in `Microsoft.Testing.Platform.Services` that describes the test-coverage messaging capabilities available in the current test application. Exposes `SupportsTestCoverageMessages` (whether the platform supports the coverage message contract) and `EnabledProducerUids` (UIDs of registered data producers that declare at least one coverage message type, as a snapshot taken at query time). Extensions should query this from a test-session lifecycle callback rather than from their constructor. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896). See also [TestCoverageMessage](#testcoveragemessage) and [ITestCoverageResult](#itestcoverageresult).

### ITestCoverageResult

A public interface in `Microsoft.Testing.Platform.Services` that is the application-scoped read model for all coverage data published during a test run. It aggregates raw [TestCoverageMessage](#testcoveragemessage), [TestCoverageThresholdMessage](#testcoveragethresholdmessage), and [TestCoverageReportMessage](#testcoveragereportmessage) messages into three queryable collections: `Scopes` (list of [CoverageScopeSummary](#coveragescopesummary)), `Thresholds` (list of threshold evaluation results), and `Reports` (list of [CoverageReportReference](#coveragereportreference)). `GetOverall(SessionUid)` retrieves the whole-run summary for a specific session, and `HasThresholdFailure` is `true` when any threshold did not pass. The terminal output device and the exit-code policy both read from this interface rather than buffering their own copies. Retrieve via `IServiceProvider`. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896).

### ITestFilter

An MSTest interface (`Microsoft.VisualStudio.TestTools.UnitTesting.ITestFilter`, `[Experimental("MSTESTEXP")]` — suppress with `#pragma warning disable MSTESTEXP`) that enables programmatic test filtering, evaluated **before** any test type is loaded and before `[AssemblyInitialize]` or `[ClassInitialize]` runs. The single method is `TestFilterResult Filter(TestFilterContext context)`. Implement this interface and register it with `[assembly: TestFilterProvider(typeof(MyFilter))]` (or, when targeting .NET, the type-safe `[assembly: TestFilterProvider<MyFilter>]`) to include, drop, or skip tests based on categories, traits, priority, or name — without incurring type-loading cost. Exceptions thrown from `Filter` produce an error result (`UTA078`) rather than silently dropping tests. The filter instance is resolved once per source assembly and cached for the entire run. Introduced in [PR #8896](https://github.com/microsoft/testfx/pull/8896). See also [TestFilterContext](#testfiltercontext), [TestFilterProvider](#testfilterprovider), and [TestFilterResult](#testfilterresult).

### ITestHostHandle

An experimental MTP interface (`Microsoft.Testing.Platform.Extensions.TestHostControllers.ITestHostHandle`, `[Experimental("TPEXP")]`) representing the lifecycle of a test host started by an [ITestHostLauncher](#itesthostlauncher). Exposes: `WaitForExitAsync(CancellationToken)`, `ExitCode`, `HasExited`, `Terminate`, `IDisposable`, and an optional `string? Identifier` for diagnostics — free-form and could be a PID string, container id, AUMID token, `host:pid`, or `null`; the platform never uses it for control flow. Returned by `ITestHostLauncher.LaunchTestHostAsync`. Introduced in [PR #9454](https://github.com/microsoft/testfx/pull/9454).

### ITestHostLauncher

An experimental MTP interface (`Microsoft.Testing.Platform.Extensions.TestHostControllers.ITestHostLauncher`, `[Experimental("TPEXP")]`) that lets an extension replace the default `Process.Start` used to start the out-of-process test host. Register via `ITestHostControllersManager.AddTestHostLauncher(...)`. The platform assembles all arguments, environment variables, and the IPC pipe, then delegates the actual launch to `LaunchTestHostAsync(TestHostLaunchContext, CancellationToken)`; the launcher returns an [ITestHostHandle](#itesthosthandle) and the platform resumes ownership of monitoring and exit-code reconciliation. At most one launcher may be registered per run. Motivating scenario: packaged Windows apps (UWP / WinUI) that require AUMID activation rather than `Process.Start`. See `docs/RFCs/017-TestHost-Launcher.md` and [PR #9454](https://github.com/microsoft/testfx/pull/9454). See also [Microsoft.Testing.Extensions.PackagedApp](#microsofttestingextensionspackagedapp).

## J

### JSON-RPC Protocol

The communication protocol used between a test runner executable (server) and a client (IDE, CLI, or CI tool). Based on [JSON-RPC 2.0](https://www.jsonrpc.org/specification), it defines messages for test discovery (`testing/discoverTests`), test execution (`testing/runTests`), result reporting, debugger attachment, and telemetry.

### JUnitReport

An MTP extension (`Microsoft.Testing.Extensions.JUnitReport`) that emits a JUnit-style XML test report at the end of a test run. The report conforms to the Jenkins/Surefire `<testsuites><testsuite><testcase>` schema and is accepted by Jenkins (`junit` step), GitLab CI (`junit:` artifact reports), Azure DevOps (`PublishTestResults@2` with `testResultsFormat: 'JUnit'`), CircleCI, GitHub Actions test reporters, and most other CI tooling. MTP's hierarchical [TestNode](#testnode) tree is preserved as a `<property name="testpath" value="…"/>` element inside each `<testcase>`, allowing tools to reconstruct hierarchy. Auto-registers via the `TestingPlatformBuilderHook` MSBuild item declared in the package's `buildMultiTargeting` props (imported by the `build` and `buildTransitive` props), so adding a `<PackageReference>` to the package is sufficient — no opt-in property is required at the package level. When using [MSTest.Sdk](#mstestsdk), the package is not added by default (the extension is still experimental); opt in with `<EnableMicrosoftTestingExtensionsJUnitReport>true</EnableMicrosoftTestingExtensionsJUnitReport>` to have MSTest.Sdk add the `<PackageReference>` for you. It is not supported in NativeAOT mode (MSTest.Sdk emits a build warning) or VSTest mode. Currently **experimental** — the API, CLI options, and on-disk format may change without notice. Enable via `--report-junit`; override filename with `--report-junit-filename`.

## L

### Lean 4

A theorem prover and interactive proof assistant used in this project for [Formal Verification (FV)](#formal-verification-fv). Lean 4 proofs are written in the `formal-verification/lean/FVSquad/` directory and compiled via `lake build`. The CI workflow (`lean-proofs.yml`) automatically builds and checks these proofs.

### Lean–C# Correspondence (FV)

A document (`formal-verification/CORRESPONDENCE.md`) that records how each [Lean 4](#lean-4) formal model corresponds to its C# source counterpart. For every [FV Target](#fv-target) it captures the type/function mappings, deliberate approximations and simplifications, properties explicitly excluded from the model, and open questions for maintainer review. Auto-generated and maintained by the [Lean Squad](#lean-squad) FV agent.

### Lean Squad

An automated agentic workflow (`.github/workflows/lean-squad.md`) that manages the formal verification lifecycle for this project. It identifies [FV Targets](#fv-target), extracts informal specs, writes [Lean 4](#lean-4) formal models, and maintains the Lean–C# correspondence documentation.

### --list-tests json

An optional argument value for the MTP `--list-tests` command-line option that switches test discovery output from the default human-readable text to a machine-readable JSON document emitted on stdout. Introduced in [PR #8280](https://github.com/microsoft/testfx/pull/8280).

| Invocation | Behavior |
| --- | --- |
| `--list-tests` | Default human-readable text output (unchanged) |
| `--list-tests text` | Explicit alias for the default text mode |
| `--list-tests json` | JSON document on stdout; banner, progress, and per-test text are suppressed; errors go to stderr |

The JSON document conforms to **schema v1**: a top-level object with `schemaVersion` (integer) and `tests` (array). Each test entry always includes `uid` and `displayName`, and optionally includes `type` (assembly full name, namespace, type name, method name, arity, return type, parameter types — from `TestMethodIdentifierProperty`), `location` (file path, start/end lines — from `TestFileLocationProperty`), `traits` (from `TestMetadataProperty`), and `properties` (from `SerializableKeyValuePairStringProperty`). Absent fields are omitted rather than emitted as `null`; `traits` and `properties` are arrays of `{ key, value }` objects so duplicate keys are preserved. The `schemaVersion` field is incremented on any breaking schema change.

## M

### MemberConditionAttribute

An MSTest attribute (`[MemberConditionAttribute]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that conditionally controls whether a test class or test method runs based on the value of one or more `public static bool` members (property, field, or parameterless method) on a specified type. Accepts a [ConditionMode](#conditionmode) argument and one or more member names; when multiple names are supplied they are combined with logical AND — the condition is met only when every referenced member is `true`. Each `[MemberConditionAttribute]` instance forms its own group, so stacking multiple attributes on the same target is also combined with AND. Throws `InvalidOperationException` at test discovery time if a referenced member cannot be resolved, surfacing typos as errors rather than silent skips. The attribute is not inherited — applying it to a base class does not affect derived classes. Introduced in [PR #9071](https://github.com/microsoft/testfx/pull/9071). Inherits from [ConditionBaseAttribute](#conditionbaseattribute).

### MSTest

Microsoft's unit testing framework for .NET. Provides attributes (`[TestClass]`, `[TestMethod]`, `[DataRow]`, etc.), assertions (`Assert`, `CollectionAssert`), and lifecycle hooks for writing and organizing tests. Packaged as `MSTest.TestFramework`, `MSTest.TestAdapter`, `MSTest.Analyzers`, and `MSTest.Sdk`.

### MSTest Runner

The self-contained test runner mode for MSTest, built on top of Microsoft.Testing.Platform. When `IsTestingPlatformApplication` is set, the test project compiles into a standalone executable that runs tests directly without requiring `dotnet test` or `vstest.console`.

### MSTest.Sdk

An MSBuild project SDK that configures MSTest, its runner, and selected Microsoft.Testing.Platform extensions. A minimal project needs only the SDK and a target framework:

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Specify the SDK version in the `Sdk` attribute (`MSTest.Sdk/x.y.z`) or through the `msbuild-sdks` section of `global.json`.

#### Runner mode

Runner selection uses the following precedence:

| `UseVSTest` | `PublishAot` | Mode | Result |
| --- | --- | --- | --- |
| `true` | Any value | VSTest | Adds `Microsoft.NET.Test.Sdk` and uses `MSTest.TestAdapter`; MTP extension properties are not supported |
| `false` (default) | `true` | NativeAOT | Builds a self-contained MTP test application using `MSTest.SourceGeneration`; only the NativeAOT-compatible extension subset is available |
| `false` (default) | Unset or `false` | ClassicEngine (MSTest runner) | Builds an executable MTP test application and supports the full extension configuration |

When `UseUwp=true`, `UseVSTest` defaults to `true` because true UWP/AppContainer test hosts do not support MTP. WinUI keeps the MTP default; set `UseVSTest=true` only for a packaged WinUI test application.

`IsTestApplication` controls whether the project is an executable test application or a reusable test library. It defaults to `true`, except for .NET Standard targets where it defaults to `false`. Set it to `false` for a project that contains shared test helpers or inherited tests and is referenced by an executable test project:

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestApplication>false</IsTestApplication>
  </PropertyGroup>
</Project>
```

In ClassicEngine and VSTest modes, test libraries receive `MSTest.TestFramework` but do not receive the adapter, runner, or testing extensions. Reference the library from an `IsTestApplication=true` project that supplies the executable test host. NativeAOT is intended for executable test applications; its source-generation, platform MSBuild, TrxReport, and CodeCoverage references are added even if `IsTestApplication=false`.

#### Extension profiles

`TestingExtensionsProfile` controls the predefined extension set in ClassicEngine mode:

| Value | Extensions enabled automatically |
| --- | --- |
| `Default` (default) | TrxReport and CodeCoverage |
| `AllMicrosoft` | Everything in `Default`, plus CrashDump, HangDump, HotReload, Retry, AzureDevOpsReport, GitHubActionsReport, HtmlReport, and Fakes |
| `None` | No extensions |

Set an individual `Enable*` property to `false` to remove an extension supplied by a ClassicEngine profile, or to `true` to opt into an extension independently. CtrfReport and JUnitReport are experimental opt-ins; OpenTelemetry is also opt-in. None are enabled by any profile. `EnableMicrosoftTestingExtensionsPackagedApp` is independent of profiles and defaults to `true` for a packaged WinUI test application because it is required to register and activate the test host; set it to `false` only when a custom launcher owns activation. In NativeAOT mode, profiles enable only TrxReport and CodeCoverage.

| Property | `Default` | `AllMicrosoft` | `None` | NativeAOT | VSTest |
| --- | --- | --- | --- | --- | --- |
| `EnableMicrosoftTestingExtensionsTrxReport` | On | On | Off | Supported | Build error |
| `EnableMicrosoftTestingExtensionsCodeCoverage` | On | On | Off | Supported | Build error |
| `EnableMicrosoftTestingExtensionsCrashDump` | Off | On | Off | Not added; emits warning if enabled | Build error |
| `EnableMicrosoftTestingExtensionsHangDump` | Off | On | Off | Not added; emits warning if enabled | Build error |
| `EnableMicrosoftTestingExtensionsHotReload` | Off | On | Off | Not added; emits warning if enabled | Build error |
| `EnableMicrosoftTestingExtensionsRetry` | Off | On | Off | Not added; emits warning if enabled | Build error |
| `EnableMicrosoftTestingExtensionsAzureDevOpsReport` | Off | On | Off | Not added; emits unsupported warning | Build error |
| `EnableMicrosoftTestingExtensionsGitHubActionsReport` | Off | On | Off | Not added; emits unsupported warning | Build error |
| `EnableMicrosoftTestingExtensionsHtmlReport` | Off | On | Off | Not available | Build error |
| `EnableMicrosoftTestingExtensionsFakes` | Off | On | Off | Not available | Not added |
| `EnableMicrosoftTestingExtensionsCtrfReport` | Off | Off | Off | Not available | Build error |
| `EnableMicrosoftTestingExtensionsJUnitReport` | Off | Off | Off | Not added; emits unsupported warning | Build error |
| `EnableMicrosoftTestingExtensionsOpenTelemetry` | Off | Off | Off | Not available | Build error |
| `EnableMicrosoftTestingExtensionsPackagedApp` | Packaged WinUI only | Packaged WinUI only | Packaged WinUI only | Packaged WinUI only | Build error |
| `EnableAspireTesting` | Off | Off | Off | Build error | Supported |
| `EnablePlaywright` | Off | Off | Off | Build error | Supported |
| `EnableWindowsUIAutomation` | Off | Off | Off | Build error | Supported |

Individual extension toggles remain unset and disabled when a profile does not enable them. This differs from explicitly setting a toggle to `false`: VSTest rejects any non-empty MTP extension toggle. NativeAOT warnings and errors are reported during the build. Properties marked "Not available" or "Not added" do not add their package in NativeAOT mode.

#### Platform integration and version properties

| Property | Behavior |
| --- | --- |
| `TestingPlatformDotnetTestSupport` | Enables the compatibility integration used by `dotnet test` before the .NET SDK introduced native MTP runner selection. For test applications it defaults to `true` with .NET SDK 9 and earlier, and remains unset/disabled with .NET SDK 10 and later. |
| `EnableMicrosoftTestingPlatform` | Advanced version-alignment escape hatch. When `true` for an `IsTestApplication=true` ClassicEngine or NativeAOT project, adds an explicit `Microsoft.Testing.Platform` package reference using `MicrosoftTestingPlatformVersion`. It is ignored for test libraries and VSTest. It is normally unnecessary and does not select the runner or change `IsTestingPlatformApplication`. |
| `EnableMSTestSourceGeneration` | Adds `MSTest.SourceGeneration` to non-NativeAOT projects. For reusable test libraries, it also adds `MSTest.TestAdapter`, which provides the runtime hooks referenced by generated code. .NET Standard is not supported because the adapter does not ship compatible runtime hooks. NativeAOT projects always include source generation. |
| `MSTestVersion` | Overrides the versions of the MSTest framework, adapter, and source generator supplied by the SDK. |
| `MSTestWindowsUIAutomationVersion` | Overrides the `MSTest.Windows.UIAutomation` package version added when `EnableWindowsUIAutomation=true`. The package version defaults to the MSTest.Sdk version. |
| `MicrosoftTestingExtensionsPackagedAppVersion` | Overrides the packaged-app launcher version supplied to packaged WinUI MTP projects. |

`EnableMSTestRunner` is set by MSTest.Sdk and should not normally be set by projects. Component-specific properties such as `MicrosoftTestingPlatformVersion`, `MicrosoftTestingExtensionsCommonVersion`, and the individual `MicrosoftTestingExtensions*Version` properties are advanced version-alignment controls.

For assembly-level parallelization properties, see [MSTestParallelizeScope / MSTestParallelizeWorkers](#mstestparallelizescope--mstestparallelizeworkers).

### MSTest.SourceGeneration

A Roslyn C# source-generator package (`MSTest.SourceGeneration`) that enables MSTest test projects to be published with Native AOT (`PublishAot=true`) or trimming (`PublishTrimmed=true`) without IL2026/IL3050 warnings or `MissingMethodException` failures at runtime. At compile time the generator scans all `[TestClass]`-decorated types and emits a `[ModuleInitializer]`-decorated registration method containing `[DynamicDependency]` hints and a pre-resolved `MethodInfo` dictionary, replacing the per-startup `Assembly.GetTypes()` and `Type.GetMethods()` reflection scans. MSTest.Sdk includes the package automatically for NativeAOT projects; set `<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>` to opt in for other configurations. Without MSTest.Sdk, add a `<PackageReference>` to `MSTest.SourceGeneration`. Existing test code needs no changes. Several shapes are outside the generator's current scope (generic test classes, inherited `[TestClass]`, `file`-local types, etc.) — see `docs/source-generator/design.md` for the full scope and known limitations.

### MSTestParallelizeScope / MSTestParallelizeWorkers

MSBuild properties that let users opt in to MSTest assembly-level parallelization without authoring a C# source file. Setting `<MSTestParallelizeScope>` emits `[assembly: Parallelize(Scope = ExecutionScope.X)]`; setting `<MSTestParallelizeWorkers>` emits `[assembly: Parallelize(Workers = N)]`; both together emit `[assembly: Parallelize(Scope = …, Workers = …)]`. Setting scope to `None` emits `[assembly: DoNotParallelize]` instead. Both properties require `GenerateAssemblyInfo` to be `true` and act via the standard `AssemblyAttribute` MSBuild item. Introduced in [PR #8233](https://github.com/microsoft/testfx/pull/8233).

### MSTestTestFramework

The native `ITestFramework` implementation that drives MSTest directly on Microsoft.Testing.Platform without routing execution through the [VSTestBridge](#vstestbridge). Introduced as part of RFC 018 (`docs/RFCs/018-Native-MTP-Integration-For-MSTest.md`) and shipped across several phases in [PR #9706](https://github.com/microsoft/testfx/pull/9706), [#9743](https://github.com/microsoft/testfx/pull/9743), [#9748](https://github.com/microsoft/testfx/pull/9748), and [#9755](https://github.com/microsoft/testfx/pull/9755) (MSTest 4.4).

In the native path the engine (`TestExecutionManager`, `UnitTestDiscoverer`) still operates on MSTest's own neutral models (`UnitTestElement`, `FrameworkTestResult`). At the host boundary three native seams replace the former VSTest intermediaries:

| Native seam | Role |
| --- | --- |
| `MtpUnitTestElementSink` | Converts discovered `UnitTestElement`s to `TestNodeUpdateMessage` (replaces VSTest `ITestCaseDiscoverySink`) |
| `MtpTestResultRecorder` | Converts `FrameworkTestResult` to `TestNodeUpdateMessage` (replaces VSTest `IFrameworkHandle`) |
| `MSTestTestNodeConverter` | Shared converter mapping `UnitTestElement` + `FrameworkTestResult` to a fully-populated MTP `TestNode` |

`MSTestTestFramework` obtains `IConfiguration`, `IOutputDevice`, and `ICommandLineOptions` from MTP's `IServiceProvider`, and reads `IMessageBus` and `ITestExecutionFilter` from the per-request `ExecuteRequestContext`/request objects — eliminating the VSTest `IRunContext`/`IRunSettings` round-trip and the double object-model conversion that the bridge imposed. As of MSTest 4.4, MSTest no longer references `Microsoft.Testing.Extensions.VSTestBridge` on the MTP code path; the [VSTestBridge](#vstestbridge) extension is still used by NUnit, Expecto, and other third-party VSTest adapters. The VSTest adapter path (real VSTest host via `MSTestDiscoverer`/`MSTestExecutor`) is unaffected.

See RFC 018 (`docs/RFCs/018-Native-MTP-Integration-For-MSTest.md`) for the design.

### MTP

See **Microsoft.Testing.Platform**.

### Microsoft.Testing.Platform (MTP)

A lightweight, extensible test platform for .NET that serves as a modern alternative to VSTest. MTP ships as a NuGet package (`Microsoft.Testing.Platform`) and provides the core infrastructure for running tests: command-line parsing, test session management, result reporting, and an extension model. Test frameworks (e.g., MSTest, xUnit adapters) and extensions (e.g., CrashDump, HangDump) plug into MTP.

### Microsoft.Testing.Platform.AI

A NuGet package (`Microsoft.Testing.Platform.AI`) that provides AI extensibility abstractions for Microsoft.Testing.Platform. It defines the [IChatClientProvider](#ichatclientprovider) interface and leverages [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) types so that test frameworks and extensions can consume Large Language Model (LLM) capabilities — flaky test analysis, crash dump analysis, test failure root-cause analysis, and more — without implementing provider-specific logic. This package ships the **abstractions only**; an AI provider implementation such as [Microsoft.Testing.Extensions.AzureFoundry](#azurefoundry) must also be registered to supply actual AI capabilities. See `docs/microsoft.testing.platform/001-AI-Extensibility.md` for the design RFC.

### Microsoft.Testing.Platform.ServerMode.Client.Sources

A source-only NuGet package that provides a client implementation for launching and communicating with an MTP test host running in [Server Mode](#server-mode). Its C# sources are compiled as internal types directly into the consuming project, avoiding a runtime dependency or separate assembly while keeping the client wire-compatible with the MTP server protocol. Added in [PR #10085](https://github.com/microsoft/testfx/pull/10085).

### Microsoft.Testing.Extensions.Logging

An experimental MTP extension (`Microsoft.Testing.Extensions.Logging`, `[TPEXP]`) that bridges Microsoft Testing Platform diagnostic logs to any `Microsoft.Extensions.Logging` provider (e.g., Console, Serilog, Application Insights, OpenTelemetry exporters). Register via `AddMicrosoftExtensionsLogging()` on `ITestApplicationBuilder`, passing either an existing `ILoggerFactory` or a configuration delegate for the logging builder. The minimum log level is bounded by the platform's effective diagnostic level; per-category filters in the `ILoggingBuilder` can narrow but not widen it. MTP core (`Microsoft.Testing.Platform`) does not depend on `Microsoft.Extensions.Logging`; this package provides an additive opt-in bridge only. Currently **experimental** — API surface may change without notice. See `docs/RFCs/013-Microsoft-Extensions-Bridges.md` for the design.

### Microsoft.Testing.Extensions.PackagedApp

An MTP extension (`Microsoft.Testing.Extensions.PackagedApp`) that enables testing packaged Windows apps by registering the layout with the `PackageManager` and activating it by AUMID through the [ITestHostLauncher](#itesthostlauncher) extension point, rather than a plain `Process.Start`. `packagedClassicApp`/`win32App` hosts receive normal `argv` (including classic AppContainer hosts); `windowsApp`/UWP hosts call `PackagedAppExtensions.GetTestApplicationArguments(LaunchActivatedEventArgs.Arguments)` before creating the MTP builder to restore the same logical arguments. For a selected AppContainer application, the launcher also contributes its exact package SID through `ITestHostControllerConnectionAuthorizer`, and the platform grants only the minimum controller-pipe client rights. Register via `builder.AddPackagedAppDeployment()`, or simply by referencing the package (its MSBuild props register the hook). The launcher only enables itself when the test application is a packaged layout — a manifest in the app's own directory, or an ancestor `AppxManifest.xml` whose `Application/@Executable` resolves back to the app directory — so referencing it from an **unpackaged** app does not force the test host controller process model or copy the build output; `TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER` (`auto`/`always`/`never`) overrides that decision. These communication primitives do not change the current SDK/platform routing limitation: true UWP/AppContainer projects are still selected for VSTest rather than started as MTP test hosts. Introduced in [PR #9454](https://github.com/microsoft/testfx/pull/9454); AUMID activation added in [PR #9970](https://github.com/microsoft/testfx/pull/9970). See also [ITestHostLauncher](#itesthostlauncher) and [docs/winui-testing.md](winui-testing.md).

## N

### NopFilter

A built-in MTP test filter that matches no tests. Used primarily in scenarios where filtering is required by the API but no tests should be selected (e.g., for dry-run or diagnostic purposes).

## O

### Orchestrator

A component in MTP that coordinates multi-process test execution. The orchestrator manages the lifecycle of one or more test host processes, aggregates results, and handles communication between the outer process (e.g., `dotnet test`) and the inner test runner processes.

### OpenTelemetry extension

An MTP extension (`Microsoft.Testing.Extensions.OpenTelemetry`) that exports test session telemetry using the [OpenTelemetry](https://opentelemetry.io/) standard, enabling integration with distributed tracing and observability platforms.

### OSConditionAttribute

An MSTest attribute (`[OSConditionAttribute]`) in `Microsoft.VisualStudio.TestTools.UnitTesting` that conditionally controls whether a test class or test method runs based on the current operating system. Accepts a [ConditionMode](#conditionmode) argument and an `OperatingSystems` flags enum value (combinable values: `Linux`, `OSX`, `Windows`, `FreeBSD`). The single-argument overload defaults to `ConditionMode.Include`. The attribute is not inherited — applying it to a base class does not affect derived classes. Inherits from [ConditionBaseAttribute](#conditionbaseattribute).

### OutputCaptureMode

An internal MSTest output-capture mode selected through the RunSettings setting `<MSTestV2><CaptureTraceOutput>...</CaptureTraceOutput></MSTestV2>`. It controls how Console/Trace output produced while a test runs is handled and whether `TestContext.Write` and `TestContext.WriteLine` output is echoed live. Backed by the internal `TestOutputCaptureMode` enum with three values:

| Value | Behavior |
| --- | --- |
| `None` | Output is not captured; Console/Trace writes flow to their normal destination. Equivalent to the legacy `CaptureTraceOutput=false`. |
| `Result` (default) | Output is captured and attached to the test result, then surfaced once the test completes. Equivalent to the legacy `CaptureTraceOutput=true`. |
| `Live` | Console/Trace output is captured and attached to the test result **and** additionally echoed live to the original console as the test runs. `TestContext.Write` and `TestContext.WriteLine` output remains buffered in the completed result and is also echoed live, similar to xUnit's `showLiveOutput` option. |

The `CaptureTraceOutput` setting accepts the mode names as well as the legacy boolean form, with `true` mapped to `Result` and `false` mapped to `None`. The modes were introduced in [PR #10013](https://github.com/microsoft/testfx/pull/10013), and live streaming for `TestContext.Write` and `TestContext.WriteLine` was added in [PR #10040](https://github.com/microsoft/testfx/pull/10040).

## P

### Playwright

The `Microsoft.Playwright.MSTest.v4` integration for browser end-to-end tests. When using [MSTest.Sdk](#mstestsdk), set `<EnablePlaywright>true</EnablePlaywright>` to add the package and its implicit using. This feature is supported in the default MSTest runner and VSTest modes, but not when `PublishAot=true`; MSTest.Sdk emits a build error for that combination.

### PlannedTest

A sealed class (`Microsoft.VisualStudio.TestTools.UnitTesting.PlannedTest`, currently `[Experimental("MSTESTEXP")]`) that describes a test that has been discovered and passed the active filter for the current assembly, before execution begins. Exposes the test's `FullyQualifiedTestClassName`, `TestName`, `TestDisplayName`, `AssemblyPath`, `ManagedTypeName`, `ManagedMethodName`, source file location (`DeclaringFilePath`, `DeclaringLineNumber`), `TestCategories` (from `[TestCategory]`), and `TestProperties` (from `[TestProperty]`). Instances are accessed via `TestRun.Current.PlannedTests`. Data-driven tests whose rows are resolved only at execution time (non-serializable data, `TestDataSourceUnfoldingStrategy.Fold` set via `[TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]`, `[DataSource]`) appear as a single `PlannedTest` entry rather than one per row. See also [TestRun](#testrun) and RFC 014 (`docs/RFCs/014-TestRun-Current-PlannedTests.md`).

### --progress

An MTP terminal-reporter command-line option that controls whether animated progress output is shown during a test run. Accepts three values:

| Value | Behavior |
| --- | --- |
| `auto` (default) | Show progress unless the terminal cannot update in place (non-ANSI/simple terminal), or the session is a test-host controller, `--list-tests`, or server mode |
| `on` | Same as `auto` (a dedicated heartbeat renderer for non-cursor modes is tracked separately) |
| `off` | Suppress all progress output |

Introduced in [PR #9145](https://github.com/microsoft/testfx/pull/9145) as a replacement for the former `--no-progress` flag. `--no-progress` continues to work as a deprecated alias that routes to `--progress off`; outside of CI it emits a one-per-process deprecation warning on stderr. The warning is suppressed in CI environments, where it would be invisible noise and where build infrastructure (such as the Arcade SDK test runner) passes `--no-progress` unconditionally. `--progress` always wins when both are supplied.

### PropertyBag

An MTP class (`Microsoft.Testing.Platform.Extensions.Messages.PropertyBag`) that holds a typed collection of `IProperty` instances attached to a [TestNode](#testnode). Extension authors populate a `PropertyBag` with properties such as `TimingProperty`, `TestFileLocationProperty`, and `TestMetadataProperty` to communicate rich metadata about a test to the platform and to other extensions. A `PropertyBag` enforces that at most one `TestNodeStateProperty` may be present at a time.

## R

### Retry

An MTP extension (`Microsoft.Testing.Extensions.Retry`) that automatically re-runs failed tests a configurable number of times by restarting the test host and filtering each new run to the tests that failed previously. It is independent from MSTest's in-process [`RetryAttribute`](#retryattribute) and [`RetryBaseAttribute`](#retrybaseattribute). When both mechanisms are enabled, their attempt counts multiply: a test using `[Retry(n)]` can run up to `(n + 1) * (--retry-failed-tests + 1)` times, so avoid combining them unless that is intended.

### RetryAttribute

An MSTest attribute (`Microsoft.VisualStudio.TestTools.UnitTesting.RetryAttribute`) that retries a failed or timed-out test within the same test-host run. It can be applied to a test method or test class and supports a maximum retry count, a delay between attempts, and constant or exponential backoff. It derives from [`RetryBaseAttribute`](#retrybaseattribute), which can be used to implement a custom retry policy. See also the process-level [`Microsoft.Testing.Extensions.Retry`](#retry) extension.

### RetryBaseAttribute

An MSTest extensibility point (`Microsoft.VisualStudio.TestTools.UnitTesting.RetryBaseAttribute`) for implementing custom in-process retry policies. A derived attribute overrides the experimental `[Experimental("MSTESTEXP")]` method `ExecuteAsync(RetryContext)`, inspects the initial failed results, invokes the supplied test delegate for each retry attempt, and returns every attempt in a [`RetryResult`](#retryresult). The implementation owns the retry loop and decides when to stop. The attribute can be applied to a test method or test class; a method-level attribute takes precedence over a class-level attribute, and class-level attributes are not inherited.

### RetryContext

An experimental MSTest structure (`Microsoft.VisualStudio.TestTools.UnitTesting.RetryContext`, `[Experimental("MSTESTEXP")]`) passed to `RetryBaseAttribute.ExecuteAsync`. `FirstRunResults` contains the results from the initial failed or timed-out execution, and `ExecuteTaskGetter` re-executes the test and returns the next attempt's results. See also [`RetryBaseAttribute`](#retrybaseattribute) and [`RetryResult`](#retryresult).

### RetryResult

An experimental MSTest class (`Microsoft.VisualStudio.TestTools.UnitTesting.RetryResult`, `[Experimental("MSTESTEXP")]`) used by custom [`RetryBaseAttribute`](#retrybaseattribute) implementations to return retry attempts in execution order through `AddResult`. `AllResults` exposes every added attempt. Under Microsoft.Testing.Platform, earlier attempts are reported as superseded and the last attempt determines the test outcome; VSTest receives only the final attempt.

### RFC

Request for Comments document in the `docs/RFCs/` folder. RFCs describe design decisions, proposed features, and implementation details for MSTest and MTP.

## S

### SequenceOrder

A public enum in `Microsoft.VisualStudio.TestTools.UnitTesting` that controls whether elements must appear in the same position when comparing sequences with `Assert.AreSequenceEqual` and `Assert.AreNotSequenceEqual`. Values: `InOrder` (0, default) — elements must appear in the same order in both sequences (LINQ `SequenceEqual` semantics); `InAnyOrder` (1) — elements may appear in any order, but each element must appear the same number of times in both sequences (multiset equality). Introduced in [PR #8334](https://github.com/microsoft/testfx/pull/8334).

### Server Mode

An MTP execution mode in which the test host process runs as a server and communicates with a client (such as an IDE, CLI, or CI tool) over the [JSON-RPC Protocol](#json-rpc-protocol). The client can initialize the server, request test discovery or execution, receive test updates, and stop the server when finished.

## T

### TestArchitectures

A `[Flags]` public enum in `Microsoft.VisualStudio.TestTools.UnitTesting` that specifies one or more processor architectures used with [ArchitectureConditionAttribute](#architectureconditionattribute). Available only on .NET (not .NET Framework). Flag values: `X86`, `X64`, `Arm`, `Arm64`, `Wasm`, `S390x`, `LoongArch64`, `Armv6`, `Ppc64le`, and (on .NET 9+) `RiscV64`. Values may be combined with bitwise OR to target multiple architectures (e.g. `TestArchitectures.X64 | TestArchitectures.Arm64`). The enum is named `TestArchitectures` rather than `Architecture` to avoid clashing with `System.Runtime.InteropServices.Architecture`, following the same pluralised `[Flags]`-enum pattern as `OperatingSystems`. Introduced in [PR #9233](https://github.com/microsoft/testfx/pull/9233).

### testconfig.json

The per-project configuration file for Microsoft.Testing.Platform, placed at the project root and read at test startup. Supports multiple top-level sections; a key one is `environmentVariables`, which declares environment variables to set on the test host process — mirroring the `<EnvironmentVariables>` element of legacy `.runsettings` and removing the need to write a custom `ITestHostEnvironmentVariableProvider` (see `docs/microsoft.testing.platform/002-TestConfig-EnvironmentVariables.md`). When the `environmentVariables` section is present and non-empty, MTP activates the **controller process model**: the launching process becomes the controller, injects the declared variables into `ProcessStartInfo`, and spawns the actual test host as a child process.

### TestContainer

An abstract base class (`TestFramework.ForTestingMSTest.TestContainer`) in the internal [`TestFramework.ForTestingMSTest`](../test/Utilities/TestFramework.ForTestingMSTest) framework used to unit-test MSTest itself. Any class that inherits from `TestContainer` is treated as a test class; every `public` parameterless method on that class is treated as a test — no `[TestClass]` or `[TestMethod]` attributes are needed. The constructor runs before each test and `Dispose(bool)` runs after each test. This framework is used only in `test/UnitTests/TestFramework.UnitTests`; all other test projects in this repository use standard MSTest or MTP.

### TestCoverageMessage

A public sealed class in `Microsoft.Testing.Platform.Extensions.Messages` (extends `DataWithSessionUid`) published to the MTP message bus by a coverage collector to report a single coverage measurement. Carries: `Scope` ([CoverageScope](#coveragescope)), `Metric` ([CoverageMetric](#coveragemetric)), `CoveredCount`, `CoverableCount` (both `long`), `ProducerId` (a stable, non-empty collector identifier), and optionally `CustomMetricName` (when `Metric == CoverageMetric.Custom`). The platform's [ITestCoverageResult](#itestcoverageresult) aggregator consumes these messages using last-write-wins correlation on the full key `(SessionUid, ProducerId, Scope, Metric, CustomMetricName)`. `Percentage` is a convenience computed property. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896). See also [TestCoverageThresholdMessage](#testcoveragethresholdmessage), [TestCoverageReportMessage](#testcoveragereportmessage).

### TestCoverageReportMessage

A public sealed class in `Microsoft.Testing.Platform.Extensions.Messages` (extends `DataWithSessionUid`) published by a coverage collector to register a rich coverage report artifact. Carries `ReportPath` (path to the file on disk), `Format` ([CoverageReportFormat](#coveragereportformat)), `ProducerId`, and optionally `CustomFormatName` (when `Format == CoverageReportFormat.Custom`). The platform stores a [CoverageReportReference](#coveragereportreference) per unique `(SessionUid, ProducerId, ReportPath)` key and exposes it through [ITestCoverageResult](#itestcoverageresult). Deep consumers such as HTML/UI report generators use this reference to locate and parse the artifact. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896). See also [TestCoverageMessage](#testcoveragemessage), [TestCoverageThresholdMessage](#testcoveragethresholdmessage).

### TestCoverageThresholdMessage

A public sealed class in `Microsoft.Testing.Platform.Extensions.Messages` (extends `DataWithSessionUid`) published by a coverage collector to report the result of a coverage threshold evaluation. Carries the same metric identification as [TestCoverageMessage](#testcoveragemessage) plus: `Aggregation` ([CoverageAggregation](#coverageaggregation)), `AggregatedOver` (optional [CoverageScopeLevel](#coveragescopelevel)), `ActualPercentage`, `RequiredPercentage`, `HasCoverableData`, `TreatNoDataAsFailure`, and `Passed` (derived: a threshold with no coverable data passes unless `TreatNoDataAsFailure` is set). When any threshold message has `Passed == false`, `ITestCoverageResult.HasThresholdFailure` returns `true`; an otherwise-successful run then exits with `CoverageThresholdFailed`, while an existing non-success exit code retains precedence. Introduced in [PR #9896](https://github.com/microsoft/testfx/pull/9896). See also [TestCoverageMessage](#testcoveragemessage), [ITestCoverageResult](#itestcoverageresult).

### TestFilterContext

A sealed MSTest class (`Microsoft.VisualStudio.TestTools.UnitTesting.TestFilterContext`, `[Experimental("MSTESTEXP")]`) passed to `ITestFilter.Filter()`. Exposes test metadata available **without loading the test type**. Always-populated: `FullyQualifiedName`, `DisplayName`, `MethodName`, `Source` (assembly file path). Optionally populated — parsed from the managed name with no reflection or type-load: `Namespace`, `ClassName`, `ManagedTypeName`, `ManagedMethodName`, `MethodArity`, `ParameterTypeFullNames`. Test metadata: `Categories`, `Traits`, `Priority`. Uses a parameterless constructor with mutable properties (no `init`) so new fields can be added additively in future releases without breaking existing callers. Introduced in [PR #8896](https://github.com/microsoft/testfx/pull/8896). See also [ITestFilter](#itestfilter).

### TestFilterProvider

An MSTest assembly-level attribute (`[assembly: TestFilterProvider(typeof(T))]` in `Microsoft.VisualStudio.TestTools.UnitTesting`, `[Experimental("MSTESTEXP")]`) that registers an [ITestFilter](#itestfilter) implementation for the test assembly. `AllowMultiple = false` — applying more than one raises error `UTA079`. The filter type must be non-generic and instantiable (a concrete class or a struct, but not `ref struct`), implement `ITestFilter`, and have a public parameterless constructor; violations produce `UTA074`–`UTA077` (`UTA073` is reported separately when the `[TestFilterProvider]` marker itself fails to load from the assembly). Because the filter type is passed as a `Type`, none of those requirements are checked by the compiler; the `MSTEST0081` analyzer reports them at build time instead. When targeting .NET, the generic variant `[assembly: TestFilterProvider<T>]` (`TestFilterProviderAttribute<TFilter>`, constrained to `ITestFilter, new()`) enforces the interface and constructor requirements through the type system; the rest are still only reported by `MSTEST0081`. Both attributes are sealed and implement the internal `ITestFilterProviderAttribute` contract, so the set of shapes that can register a filter is closed to exactly those two. The adapter resolves the shipped non-generic attribute through its own concrete type and consults `ITestFilterProviderAttribute` only once its metadata probe has seen a generic marker, so a newer adapter running against an older `MSTest.TestFramework` — which has neither the contract nor the generic attribute — still discovers the non-generic registration instead of failing with `UTA073`. Mixing the two forms in one assembly is still `UTA079` (the compiler does not see it as a duplicate attribute because they are distinct types, so `MSTEST0081` reports it). The generic form is deliberately unavailable on .NET Framework, whose reflection stack throws `NotSupportedException` ("Generic types are not valid.") when it materializes a generic custom attribute. Introduced in [PR #8896](https://github.com/microsoft/testfx/pull/8896). See also [ITestFilter](#itestfilter).

### TestFilterResult

A readonly MSTest struct (`Microsoft.VisualStudio.TestTools.UnitTesting.TestFilterResult`, `[Experimental("MSTESTEXP")]`) returned by `ITestFilter.Filter()` that declares the filtering decision for a test. Three static members:

- `TestFilterResult.Run` — execute the test normally.
- `TestFilterResult.Drop` — silently exclude the test (zero `[AssemblyInitialize]` / `[ClassInitialize]` cost, no result emitted).
- `TestFilterResult.Skip(string reason)` — mark the test Skipped (outcome: `Skipped`) with the given reason (throws `ArgumentNullException` if `reason` is `null`, `ArgumentException` if empty or whitespace-only).

The underlying `TestFilterAction` enum values are `Run = 0`, `Drop = 1`, `Skip = 2`. Introduced in [PR #8896](https://github.com/microsoft/testfx/pull/8896). See also [ITestFilter](#itestfilter).

### TestNode

A core MTP class (`Microsoft.Testing.Platform.Extensions.Messages.TestNode`) that represents a single test item — either discovered or executed. Each `TestNode` carries a unique `Uid` (`TestNodeUid`), a human-readable `DisplayName`, and a [PropertyBag](#propertybag) of typed properties (state, timing, file location, metadata, etc.). `TestNode` instances are published to the `IMessageBus` by test framework adapters during discovery and execution phases.

### TestRun

A static ambient class (`Microsoft.VisualStudio.TestTools.UnitTesting.TestRun`, currently `[Experimental("MSTESTEXP")]`) that exposes run-wide information about the currently executing test session via `TestRun.Current` (type `ITestRunInfo`). `Current` is never `null`: before discovery is complete for an assembly it returns an empty `ITestRunInfo`; once the adapter has finished filtering, `Current.PlannedTests` contains one [PlannedTest](#plannedtest) per test that will run. Unlike `TestContext.Current` (which is `null` outside test execution), `TestRun.Current` is accessible from `[AssemblyInitialize]`, helper classes, fixtures, and extension code. Scoped per process and per AppDomain (on .NET Framework). See also [PlannedTest](#plannedtest) and RFC 014 (`docs/RFCs/014-TestRun-Current-PlannedTests.md`).

### TFM (Target Framework Moniker)

A short string that identifies a specific .NET target framework (e.g., `net9.0`, `net48`, `netstandard2.0`). Used to distinguish test runs across multiple frameworks in multi-targeted projects.

### TrxReport

An MTP extension (`Microsoft.Testing.Extensions.TrxReport`) that generates a `.trx` (Test Results XML) file upon test session completion. TRX files are the standard Visual Studio and Azure DevOps test result format.

### TRX (Test Results XML)

The XML-based test result file format used by Visual Studio, Azure DevOps, and `vstest.console`. Contains test run metadata, individual test outcomes, error messages, and stack traces. Generated by the **TrxReport** extension.

### TreeNodeFilter

An MTP component (`TreeNodeFilter.cs`) that evaluates filter expressions against test node properties to select which tests to run. Filter expressions support Boolean algebra: `&` (AND), `|` (OR), `!` (NOT), and property comparisons (e.g., `/**[Tag=Smoke]`). Wildcard patterns (`*`) are supported in both path segments and property values. Internally, filter expressions are parsed into a `FilterExpression` tree and evaluated recursively.

## V

### VideoRecorder

An experimental MTP extension (`Microsoft.Testing.Extensions.VideoRecorder`, `[Experimental("TPEXP")]`, ships as `1.0.0-alpha`) that records the screen during a test run using an external **ffmpeg** process and attaches the produced video clips as session artifacts. Recording is **continuous** — not started/stopped per test — to avoid races with parallel tests: the extension runs a rolling segment mux throughout the session and losslessly slices per-test clips (or a single chaptered session video) from timing data after each test completes.

| Option | Values | Description |
| --- | --- | --- |
| `--capture-video` | *(none)*, `on-failure`, `always` | Enable recording. Default retention: `on-failure` (keep only failing-test clips). |
| `--capture-video-granularity` | `test` (default), `session` | One clip per test, or one chaptered video for the whole session. |
| `--capture-video-source` | `screen` (default), `window` | Full screen, or the current-process window (Windows; falls back to screen on headless/CI). |
| `--capture-video-max-duration` | seconds | Rolling buffer cap — keep ~the last N seconds on disk. |
| `--capture-video-chapters` | `on` (default), `off` | Chapter bookmarks in the per-session video. |
| `--capture-video-args` | any string | Extra arguments forwarded to ffmpeg. |

Register via `builder.AddVideoRecorderProvider()`. See `src/Platform/Microsoft.Testing.Extensions.VideoRecorder/DESIGN.md` for format and licensing notes. Introduced in [PR #9377](https://github.com/microsoft/testfx/pull/9377).

### VSTest

Microsoft's previous-generation test platform (`vstest.console.exe`, `Microsoft.TestPlatform.*`). MSTest v2 originally ran on top of VSTest. MTP is the modern successor to VSTest, offering better performance and a simplified extension model.

### VSTestBridge

An MTP extension (`Microsoft.Testing.Extensions.VSTestBridge`) that provides backward compatibility for test adapters written against the VSTest API. Allows existing VSTest-based test frameworks and adapters (NUnit, Expecto, and third-party VSTest adapters) to run on MTP without a full rewrite. Note: as of MSTest 4.4, **MSTest no longer depends on VSTestBridge** on the MTP code path — MSTest uses [MSTestTestFramework](#mstesttestframework) as a native `ITestFramework` instead (see RFC 018 in `docs/RFCs/018-Native-MTP-Integration-For-MSTest.md` and [PR #9755](https://github.com/microsoft/testfx/pull/9755)).
