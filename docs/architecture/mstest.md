# MSTest architecture

| Metadata | Value |
| --- | --- |
| Status | Current implementation baseline (scoped) |
| Baseline revision | `c5e1f5c613fa02ffa507db333e4702da6c97e488` |
| Source-inspected | 2026-10-09 |
| Assurance | Source inspection and existing-test mapping only; no tests executed |

This describes MSTest's implementation at the revision above, not a release or servicing guarantee. Read the [specification guide](../specifications/README.md) and the [observable contracts](../specifications/mstest.md) for the evidence boundary.
RFC checklists retain their historical meaning; implementation presence does not establish approval or shipment.

## Components and ownership

```text
User test assembly
  attributes + assertions + TestContext
       |
       +-- compile time: MSTest.Analyzers / MSTest.SourceGeneration
       |
       +-- VSTest host -> MSTestDiscoverer / MSTestExecutor --+
       |                                                   |
       +-- MTP host -> MSTestTestFramework -----------------+
                                                           |
                                                      MSTestEngine
                                                           |
                         discovery <-> neutral UnitTestElement model
                                                           |
                             TestExecutionManager -> UnitTestRunner
                                                           |
                                TestMethodRunner -> TestMethodInfo
                                                           |
                             framework TestResult -> host-boundary recorder
```

| Component | Responsibility and boundary |
| --- | --- |
| [TestFramework](../../src/TestFramework/TestFramework) | Public `Microsoft.VisualStudio.TestTools.UnitTesting` attributes, assertions, data-source contracts and test context abstraction; not the test scheduler. |
| [TestFramework.Extensions](../../src/TestFramework/TestFramework.Extensions) | Platform-specific additions, including UI test execution attributes. |
| [MSTest.TestAdapter](../../src/Adapter/MSTest.TestAdapter) | Host registration, host-specific filters, discovery/result conversion, and the neutral `MSTestEngine` entry point. |
| [MSTestAdapter.PlatformServices](../../src/Adapter/MSTestAdapter.PlatformServices) | Shared discovery/execution engine, reflection abstractions, settings, deployment, lifecycle, scheduling, output and context implementations; bundled with the adapter package. |
| [MSTest.Analyzers](../../src/Analyzers/MSTest.Analyzers) and [code fixes](../../src/Analyzers/MSTest.Analyzers.CodeFixes) | Roslyn diagnostics and authoring assistance. Static diagnostics do not replace runtime validation or prove a test ran. |
| [MSTest.SourceGeneration](../../src/Analyzers/MSTest.SourceGeneration) | Compile-time metadata, rooting and invocation delegates, consumed through runtime hooks in platform services. |
| [MSTest.Sdk](../../src/Package/MSTest.Sdk) | MSBuild composition of test framework, adapter, runner and optional extensions; not a second execution engine. |

The assembly named platform services is an MSTest component, not the MTP host. The adapter [project](../../src/Adapter/MSTest.TestAdapter/MSTest.TestAdapter.csproj) packages it under `buildTransitive`, and still references `Microsoft.TestPlatform.ObjectModel` for its VSTest-facing surface.

## Host entry points

[MSTestEngine](../../src/Adapter/MSTest.TestAdapter/MSTestEngine.cs) accepts neutral source lists, settings XML, configuration, logging, element sinks, filters and result recorders. It initializes discovery/settings, manages telemetry lifetime and the run's apartment/cancellation wrapper, then calls the shared engine. Selected VSTest cases can enter through its materialized-element path.

[VSTest entry points](../../src/Adapter/MSTest.TestAdapter/VSTestAdapter) adapt real VSTest contexts and handles. The native [MTP framework](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestFramework.cs) handles discovery/run requests directly and calls `MSTestEngine`; it does not invoke the VSTest entry points. [AddMSTest](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/TestApplicationBuilderExtensions.cs) registers that framework, options, runsettings providers and capabilities.

Native [discovery sink](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpUnitTestElementSink.cs) and [result recorder](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpTestResultRecorder.cs) publish converted nodes. [MSTestTestNodeConverter](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestNodeConverter.cs) owns MSTest-to-MTP state/metadata conversion.
The host owns its reporting pipeline, protocol transport and aggregate process exit decision:
this document does not specify those MTP internals or establish byte-for-byte VSTest/MTP parity.
[RFC 018](../RFCs/018-Native-MTP-Integration-For-MSTest.md) describes the earlier bridge architecture and migration proposal.

## Discovery and identity

[AssemblyEnumerator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/AssemblyEnumerator.cs) loads a source, obtains types through `IReflectionOperations`, resolves internal discovery and data-source unfolding, and delegates each type to [TypeEnumerator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TypeEnumerator.cs).
[TypeValidator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TypeValidator.cs) and [TestMethodValidator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TestMethodValidator.cs) validate container accessibility and method shape. Abstract classes are not directly enumerated as runnable containers.
F# modules have a dedicated static-method/container exception; C# static classes do not.

The neutral [UnitTestElement](../../src/Adapter/MSTestAdapter.PlatformServices/ObjectModel/UnitTestElement.cs) combines method identity with categories, traits, deployment items, parallelization flags, locks and dependencies. Overload signatures and managed names participate in discovery; a display name is not a complete method identity. The host boundary transports this data to VSTest cases or MTP nodes.
Trait extensibility comes from non-sealed `TestPropertyAttribute`, now allowed on classes as well as methods.

Data-driven discovery can unfold one method into multiple elements.
Serialization, duplicate display names and unsupported metadata can require fallback instead of independent row discovery;
the exact identity/serialization matrix is not exhaustively specified here.

## Execution and lifecycle

[TestExecutionManager.SourceExecution](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.SourceExecution.cs) groups tests by source, resolves source-host isolation and assembly settings, applies host filters, then constructs a runner for the selected elements. Source groups are visited sequentially by this engine; in-assembly concurrency is separate from any host-level process concurrency.

The runtime layers separate distinct jobs:

1. `UnitTestRunner` resolves methods, ignore conditions, assembly/class initialization and cleanup accounting.
2. `TestMethodRunner` selects regular, unfolded, folded or legacy data-source execution and invokes the selected executor.
3. `TestMethodInfo` activates the test instance, sets context, invokes per-test lifecycle/body methods and aggregates failures.
4. A host recorder reports start/result/end or empty-execution completion without changing who invokes user code.

[TestMethodInfo.Lifecycle](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.Lifecycle.cs) initializes inherited fixtures base-first and cleans them derived-first; cleanup loops stop at the first failure.
Global test initialization/cleanup and instance leases are additional stages, not synonyms for assembly fixtures.
[ClassCleanupManager](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/ClassCleanupManager.cs) counts selected tests and gates end-of-assembly cleanup on completion of every class.
Skipped/never-invoked selected tests still need bookkeeping.
Failure, cancellation and fixture-inheritance details exceed the normal-path contract in this baseline.

`TestRun.Current` is a separate process/AppDomain ambient snapshot: the runner publishes host-filter-selected planned tests before assembly initialization and retains them until the next snapshot replaces them. It does not track later skips, drops, dependency failures or actual outcomes; see [MSTEST-024](../specifications/mstest.md#mstest-024--planned-test-snapshots-describe-selection-not-execution) and [RFC 014's implementation note](../RFCs/014-TestRun-Current-PlannedTests.md).

`TestContext.Current` flows through scoped execution context.
Context output, properties, attachments, outcome and diagnostic state are mutable execution data.
Folded data-source rows [clone their context](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodRunner.DataRow.cs) so one row does not accumulate another row's per-test state.
`TestTempDirectory` is lazy private scratch space, unlike run/deployment directories shared across tests.

## Data-source and extensibility boundaries

[ITestDataSource](../../src/TestFramework/TestFramework/Interfaces/ITestDataSource.cs) returns arguments for invocation; `DataRow` and `DynamicData` implement this framework-level extension point.
[DynamicDataAttribute](../../src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataAttribute.cs) supports static properties, methods and fields, automatic kind detection, external declaring types and custom display names.
Row wrappers can supply skip reasons, categories, complete display names or argument labels.
Folding is an execution/discovery strategy, not a promise that one method produces only one result.

Legacy [DataSourceAttribute](../../src/TestFramework/TestFramework/Attributes/DataSource/DataSourceAttribute.cs) is distinct: the [adapter data-source path](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodRunner.DataSource.cs) sets `TestContext.DataRow`/connection state, and supported backing providers are .NET Framework-specific.
[Custom execution](../../src/TestFramework/TestFramework/Attributes/TestMethod/TestMethodAttribute.cs) uses `ExecuteAsync(ITestMethod)` and `InvokeAsync`, with class-level selection via `GetTestMethodAttribute`.
The synchronous signatures in the early extensibility RFC are historical.

## Scheduling and selection

[Ordinary scheduling](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.ParallelExecution.cs) chunks the parallelizable set by class or by element, drains it with worker tasks, then runs the nonparallelizable tail.
Configured worker/scope values override assembly `Parallelize`; `DoNotParallelize` still prevents participation.
Unfolded rows can be independent chunks under `MethodLevel`; folded rows execute inside one scheduled element.

[ResourceLockManager](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/ResourceLockManager.cs) coordinates ordinal, case-sensitive names with reader/writer locks.
It unions each chunk's keys, promotes mixed modes to exclusive, acquires sorted keys and releases in reverse order.
A blocked chunk consumes a worker. This is cooperative declaration-based coordination, not OS isolation or cross-process locking.

[TestDependencyGraph](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestDependencyGraph.cs) projects per-source test dependencies onto chunks.
It orders prerequisites, detects real cycles and recovers class-projection cycles by sequential demotion.
[TestDependencyCoordinator](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestDependencyCoordinator.cs) gates dependents on recorded outcomes; unmatched selected-run targets warn rather than pulling filtered tests back into the run.
Attribute and MTP JSON declarations merge. Per-row and cross-assembly dependencies are not provided by this model.

Host selection is distinct from in-test `ITestFilter` decisions.
[VSTest filtering](../../src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs) uses host expressions/cases; [native MTP filtering](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpTestElementFilter.cs) reads neutral element properties, while [MSTestFilterContext](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestFilterContext.cs) combines the request and expression-filter inputs.
The complete grammar, escaping and combination matrix remains outside this baseline.

## Settings, assertions and artifacts

[MSTestSettings.Configuration](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs) selects JSON or XML MSTest settings, rejects a simultaneous MSTest section in both, and supports the `MSTestV2` XML alias.
The [schema guide](../testconfig.schema.md) describes authoring; editor schema constraints are not themselves runtime validation.
Explicit timeout settings must be positive; absence uses the internal no-timeout default.
Method timeout attributes override configuration. Cooperative cancellation depends on user code observing the token;
noncooperative timeout reporting is not a guarantee that background code has stopped.

[Assert](../../src/TestFramework/TestFramework/Assertions/Assert.cs) supplies a singleton for custom extensions. Soft scopes collect reportable failures but leave `Fail`, `Inconclusive` and parameter-precondition failures hard. Nullable annotations remain; inside a soft scope they do not enforce postconditions.
Structured messages and typed expected/actual values are distinct from diagnostic snapshots.
The [assertion artifact schema](../mstest-assertion-failure-state.schema.md) describes bounded, opt-in snapshots attached to nonpassing results; these can contain sensitive test values and paths.

## SDK and generated metadata

[SDK targets](../../src/Package/MSTest.Sdk/Sdk/Sdk.targets) default to MTP and select VSTest with `UseVSTest=true`. Runner targets distinguish ordinary and NativeAOT composition; `IsTestApplication=false` builds a test library.
Source generation is automatic on the NativeAOT runner path and otherwise opt-in.
Framework, adapter and generator versions must align because emitted registration calls depend on adapter runtime hooks. Feature flags and extension profiles configure composition, not evidence of downstream compatibility.

The [source-generator design](../source-generator/design.md) remains the detailed design reference, with a current-implementation note. Generated entries and invokers are authoritative when complete, and missing/general-contract entries retain reflection fallback. Native MTP can bypass method scans for a bounded descriptor subset; incomplete descriptors retain legacy discovery.
Dynamic-data accessors cover resolvable supported sources, not every custom data-source or parameter-binding shape. Compile-time diagnostics identify several unsupported generation shapes. Preserving an assembly with trimming roots is not equivalent to registering its omitted test classes.

## Deliberate limits

This baseline is not an exhaustive specification of assertions/overloads, analyzers/code fixes, retry/timeout races, all fixture inheritance/failure paths, deployment/resolution, data identity serialization, UI/STA/threading, NativeAOT/trimming, SDK extension profiles, host reporting or cross-version package interoperability. Existing tests are mapped, not executed conformance evidence. The [contract document](../specifications/mstest.md) makes these exclusions explicit per topic.
