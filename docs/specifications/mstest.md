# MSTest observable behavioral contracts

| Metadata | Value |
| --- | --- |
| Status | Current implementation baseline (scoped) |
| Baseline revision | `c5e1f5c613fa02ffa507db333e4702da6c97e488` |
| Source-inspected | 2026-10-09 |
| Assurance | Source inspection and existing-test mapping only; no tests executed |

See the [specification guide](README.md) and [MSTest architecture](../architecture/mstest.md).
“Shall” below describes the inspected implementation, not unimplemented RFC intent.
Every evidence entry identifies source inspected and existing tests located; it is **not** a passing conformance result.
No release branch, packed consumer, target-framework matrix or servicing policy was validated.
Stable identifiers can be used in follow-up changes without turning this document into an API catalog.

## Discovery and host boundaries

### MSTEST-001 — Native MTP and VSTest share the engine, not a host object model

MSTest's native MTP registration shall route discovery/run requests directly to the neutral engine,
publishing nodes through its own sink/recorder. VSTest shall retain its separate discoverer/executor entry points.
Checked: registration, request dispatch and model conversion in [MSTestTestFramework](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestFramework.cs),
[MSTestEngine](../../src/Adapter/MSTest.TestAdapter/MSTestEngine.cs) and [converter](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestNodeConverter.cs).
Existing tests: [MSTestTestNodeConverterTests](../../test/UnitTests/MSTestAdapter.UnitTests/MSTestTestNodeConverterTests.cs), `MtpUnitTestElementSink_PublishesDiscoveredTestNode`, `MtpTestResultRecorder_RecordResult_PublishesResultNodeAndReturnsFailedFlag`.
Excludes host protocol/exit aggregation, all capability/session races, and claimed byte-for-byte bridge parity.

### MSTEST-002 — Reflection discovery validates containers and methods

Ordinary C# test methods shall require an attributed eligible container and an accessible, nonabstract instance method
with a supported return shape (non-async `void`, `Task`, or nongeneric `ValueTask`).
`DiscoverInternals` shall permit internal classes/methods; private methods remain ineligible.
F# modules shall use the dedicated static-method exception.
Checked: [TypeValidator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TypeValidator.cs) and
[TestMethodValidator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TestMethodValidator.cs).
Existing tests: [TypeValidatorTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Discovery/TypeValidatorTests.cs), `IsValidTestClassShouldNotInferCSharpContainersFromTestMethods`; [TestMethodValidatorTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Discovery/TestMethodValidatorTests.cs), `IsValidTestMethodShouldReturnTrueForStaticMethodsOnFSharpModules`, `IsValidTestMethodShouldReturnFalseAndReportTargetedWarningForGenericValueTaskReturnType`.
Excludes the generic type-inference matrix, by-ref invocation and generated-descriptor eligibility.

### MSTEST-003 — Traits are extensible metadata

`TestPropertyAttribute` shall be subclassable and applicable multiple times to methods/classes.
Discovery shall carry properties as traits; filter property names not recognized as built-ins shall be looked up in traits.
Checked: [attribute](../../src/TestFramework/TestFramework/Attributes/TestMethod/TestPropertyAttribute.cs),
[TypeEnumerator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TypeEnumerator.cs) and [native filter](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpTestElementFilter.cs).
Existing tests: [TestPropertyAttributeTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/TestPropertyAttributeTests.cs), `GetTestMethodInfoShouldAddPropertiesFromContainingClassCorrectly`; [MSTestTestNodeConverterTests](../../test/UnitTests/MSTestAdapter.UnitTests/MSTestTestNodeConverterTests.cs), `ToDiscoveredTestNode_AddsCategoriesAndTraitsAsMetadata`.
Excludes VS UI queries, logger/TRX schema fidelity and duplicate-key precedence across every host.

### MSTEST-004 — Host filters preserve selected identity

Fully qualified name and identity filters shall select matching neutral elements during discovery and execution,
including execution reconstructed from VSTest cases without local adapter payload.
Checked: [TestMethodFilter](../../src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs) and
[source execution](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.SourceExecution.cs).
Existing tests: [TestCaseFilteringTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/TestCaseFilteringTests.cs), `FilterByFullyQualifiedNameSelectsExactlyTheMatchingTestDuringExecution`, `FilterByIdSelectsExactlyTheMatchingTestDuringDiscovery`.
Excludes an exhaustive grammar/escaping/tree-node/UID combination matrix and in-test `ITestFilter` policy.

## Invocation and data

### MSTEST-005 — Custom execution is asynchronous

The default `TestMethodAttribute.ExecuteAsync` shall invoke `ITestMethod.InvokeAsync` and return its result.
Custom method attributes may override execution; a custom class attribute may select the method executor.
Checked: [TestMethodAttribute](../../src/TestFramework/TestFramework/Attributes/TestMethod/TestMethodAttribute.cs),
[TestClassAttribute](../../src/TestFramework/TestFramework/Attributes/TestMethod/TestClassAttribute.cs) and [runner](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodRunner.Execution.cs).
Existing tests: [LegacyExtensibilityTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyExtensibilityTests.cs), `CustomTestMethodAndClassAttributesReturnEveryExecutionResult`, `CustomTestMethodAttributeComposesWithDataRows`.
Excludes custom executors that violate context/lifecycle contracts and compatibility with old synchronous overrides.

### MSTEST-006 — Lifecycle ordering is separate from the test body

For the ordinary initialized instance path, inherited test initialization shall run base-first;
test cleanup shall run derived-first, with subsequent cleanup methods stopped by an earlier cleanup failure.
Assembly/class fixtures shall be managed independently of per-invocation lifecycle.
Checked: [TestMethodInfo.Lifecycle](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.Lifecycle.cs) and
[ClassCleanupManager](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/ClassCleanupManager.cs).
Existing tests: [LegacyLifecycleObjectModelTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyLifecycleObjectModelTests.cs), `LifecycleInheritanceModes_HaveExactPerTestObjectModelMessages`; [ClassCleanupManagerTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/ClassCleanupManagerTests.cs), `AssemblyCleanupRunsAfterAllTestsFinishEvenIfWeScheduleTheSameTestMultipleTime`.
Excludes the complete fixture-inheritance, initialization-failure, global-fixture, disposal and cancellation matrix.

### MSTEST-007 — Data-source folding controls discovery granularity

Framework `ITestDataSource` rows shall supply invocation arguments; `Auto` unfolding shall defer to assembly policy,
normally unfolding, while explicit `Fold` shall retain a parent discovery element.
Folded execution shall clone per-row test context and still produce row results.
An empty source shall fail by default, with `ConsiderEmptyDataSourceAsInconclusive` permitting an inconclusive result.
Checked: [AssemblyEnumerator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/AssemblyEnumerator.cs) and
[TestMethodRunner.DataRow](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodRunner.DataRow.cs).
Existing tests: [LegacyExtensibilityTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyExtensibilityTests.cs), `FoldedCustomTestDataSourceDiscoversOneParentAndExecutesTwoRows`, `DynamicDataPropertyMethodAndExternalSourcesExecute`.
Excludes the complete data serialization/duplicate-name fallback matrix and aggregate folded-row outcome policy.

### MSTEST-008 — Dynamic data supports more than properties

The name-only `DynamicData` constructor shall select automatic source-kind detection, not property-only lookup.
Supported sources include static properties, methods and fields; explicit declaring types and display-name callbacks are supported.
Checked: [DynamicDataAttribute](../../src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataAttribute.cs).
Existing tests: [DynamicDataAttributeTests](../../test/UnitTests/TestFramework.UnitTests/Attributes/DynamicDataAttributeTests.cs), `GetDataShouldReadDataFromFieldInAutoDetectMode`, `GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayNameInDifferentClass`; [LegacyDynamicDataBehaviorTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyDynamicDataBehaviorTests.cs), `DynamicDataSourcesInheritanceDisplayNamesAndOverloadsExecute`.
Excludes full member-overload resolution, source arguments, generic inference and trimming preservation.

### MSTEST-009 — Legacy DataSource is a distinct compatibility path

Legacy `DataSourceAttribute` execution shall obtain adapter data rows and expose each through `TestContext.DataRow`,
rather than treating it as framework `ITestDataSource` argument data. Its backing provider implementation is .NET Framework-only.
Checked: [DataSourceAttribute](../../src/TestFramework/TestFramework/Attributes/DataSource/DataSourceAttribute.cs),
[TestMethodRunner.DataSource](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodRunner.DataSource.cs) and [provider](../../src/Adapter/MSTestAdapter.PlatformServices/Services/TestDataSource.cs).
Existing tests: [DataSourceTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/DataSourceTests.cs), `TestDataSourceFromAppConfig`; [DesktopTestDataSourceTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Services/DesktopTestDataSourceTests.cs).
Excludes database/OleDb availability, provider-specific behavior and every conflicting-data-attribute combination.

## Concurrency and configuration

### MSTEST-010 — In-assembly parallelism is chunk-based

The parallelizable set shall be chunked by class or by element according to `ExecutionScope`.
Configured worker/scope values shall override assembly values; configured/attribute zero workers normalize to processor count.
`DoNotParallelize` elements shall run in the sequential tail after the parallel set drains.
Unfolded rows may therefore run independently at method scope; folded rows share one scheduled element.
Checked: [assembly settings](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestAssemblySettingsProvider.cs),
[source execution](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.SourceExecution.cs) and [parallel execution](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.ParallelExecution.cs).
Existing tests: [ParallelExecutionTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/ParallelExecutionTests.cs), `AllMethodsShouldRunInParallel`, `DoNotParallelizeTestRunsAfterAllParallelizableTests`; [MSTestSettingsTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/MSTestSettingsTests.cs), `ParallelizationWorkersShouldBeSetToProcessorCountWhenSetToZero`.
Excludes host process scheduling, guaranteed speedup and a nonexistent `ExecutionScope.Custom`.

### MSTEST-011 — Resource locks apply to scheduling chunks

Resource names shall compare ordinally and case-sensitively. Readers may coexist; writers shall exclude other holders.
A chunk shall acquire its union of keys in sorted order, using the strongest declared mode per key, and release in reverse order.
Class-level scheduling therefore widens method locks to the class chunk. Waiting for a lock consumes a worker.
Checked: [ResourceLockManager](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/ResourceLockManager.cs) and [parallel scheduler](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.ParallelExecution.cs).
Existing tests: [ResourceLockManagerTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/ResourceLockManagerTests.cs), `GetChunkLocks_ReadWriteWinsOverRead_ForSameKey`; [ResourceLockExecutionTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/ResourceLockExecutionTests.cs), `ResourceLock_UnderClassLevelScope_LocksWholeClassChunk_AndMergesModes`.
Excludes automatic resource access detection, cross-process locks, lock-aware dispatch and undeclared conflicting accesses.

### MSTEST-012 — Dependencies constrain selected tests within a source

Resolved prerequisites shall complete before dependents run. A nonpassing prerequisite shall skip its dependents
unless the merged declarations permit proceeding. Unmatched selected-run targets shall warn and be ignored.
Real cycle members shall fail; class-projection-only cycles shall demote affected tests to sequential ordering.
Configured MTP JSON chains/nodes shall merge with attribute declarations rather than overriding them.
Checked: [graph](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestDependencyGraph.cs),
[coordinator](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestDependencyCoordinator.cs) and [settings parser](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs).
Existing tests: [TestDependencyGraphTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/TestDependencyGraphTests.cs), `Build_WhenADependencyMatchesNoTestInTheRun_WarnsAndIgnoresTheEdge`; [TestDependencyExecutionTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/TestDependencyExecutionTests.cs), `DependsOn_WhenAPrerequisiteFails_SkipsDependentsTransitively_ButHonorsProceedOnFailure`, `DependencyConfiguration_OrdersTestsThatDeclareNoAttribute`.
Excludes automatic inclusion of filtered prerequisites, row-to-row matching and cross-assembly dependencies.

### MSTEST-013 — MSTest settings have an explicit format boundary

XML shall recognize `RunSettings` with `MSTest` or its `MSTestV2` alias.
On the native configuration path, providing both a JSON `mstest` section and an XML MSTest section shall be rejected.
An XML file without an MSTest section may coexist with JSON MSTest configuration.
Checked: [MSTestSettings.Configuration](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs).
Existing tests: [MSTestSettingsTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/MSTestSettingsTests.cs), `PopulateSettingsShouldInitializeSettingsFromMSTestV2Section`; [ConfigurationSettingsTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/ConfigurationSettingsTests.cs), `TestConfigJson_AndRunSettingsWithoutMstest_OverrideRunConfigration`.
Excludes the MTP configuration-provider precedence matrix and schema enforcement by editors.

### MSTEST-014 — Timeout omission differs from an explicit zero

Explicit JSON/XML timeout settings shall be strictly positive milliseconds; zero/negative values shall warn and be ignored.
Omission shall retain the no-timeout default. Method attributes shall override configured timeouts.
Dedicated global-test fixture timeouts shall fall back to corresponding test-initialize/cleanup settings when unset.
Checked: [JSON parser](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.Configuration.cs),
[XML parser](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.RunSettingsXml.cs), [fixture timeout selection](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TypeCache.ClassInfo.cs) and [TimeoutInfo](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TimeoutInfo.cs).
Existing tests: [MSTestSettingsTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/MSTestSettingsTests.cs), `ConfigJson_WithZeroTimeout_GettingAWarningAndTimeoutIsNotSet`, `GlobalFixtureTimeout_WhenDedicatedKeyNotSet_FallsBackToSharedTestKey`; [LegacyTimeoutBehaviorTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyTimeoutBehaviorTests.cs), `TimeoutExpiration_CancelsTestContextTokenAndAllowsBackgroundOutput`.
Excludes guaranteed termination of noncooperative user code, all timeout/cancel races and the full STA/framework-abort matrix.

### MSTEST-015 — Outcome defaults are not discovery inclusion rules

MSTest settings shall default `MapNotRunnableToFailed` and `TreatDiscoveryWarningsAsErrors` to true,
and `MapInconclusiveToFailed` to false. Mapping an execution outcome shall not imply every invalid signature is discovered as a runnable test.
Checked: [MSTestSettings](../../src/Adapter/MSTestAdapter.PlatformServices/MSTestSettings.cs) and [method validator](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/TestMethodValidator.cs).
Existing tests: [MSTestSettingsTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/MSTestSettingsTests.cs), `MapNotRunnableToFailedIsByDefaultTrueWhenNotSpecified`, `TreatDiscoveryWarningsAsErrorsShouldBeTrueByDefault`.
Excludes a complete host outcome/exit-code mapping table and classification of every discovery warning.

## Context, assertions and artifacts

### MSTEST-016 — Per-test scratch directories are lazy and outcome-aware

Executing tests shall receive a unique, lazily created scratch directory when requested; fixture contexts shall not create one.
Disposal shall best-effort delete passing-test directories, retaining nonpassing tests, result attachments under the directory
and explicit `MSTEST_TEST_TEMP_DIRECTORY_RETAIN=1`/`true` requests. Creation shall not change process current directory.
Checked: [creation](../../src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.TempDirectory.cs)
and [cleanup](../../src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.TempDirectoryCleanup.cs).
Existing tests: [PerTestTempDirectoryTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/PerTestTempDirectoryTests.cs), `TestTempDirectory_IsUnique_CleansUpOnPass_And_RetainsOnFailure`, `TestTempDirectory_IsRetained_OnPass_WhenResultFileRegisteredUnderIt`.
Excludes bounded disposal time on stalled filesystems and availability on UWP/WinUI; retry/diagnostic retention has additional rules.

### MSTEST-017 — Custom assertions use the singleton extension point

`Assert.That` shall expose a singleton for C# extension methods. Custom assertion authors can throw `AssertFailedException`;
this does not automatically enroll their failures in a soft scope.
Checked: [Assert](../../src/TestFramework/TestFramework/Assertions/Assert.cs).
Existing tests: [LegacyExtensibilityTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/LegacyExtensibilityTests.cs), `CustomAssertExtensionsReportExactPassesAndFailures`.
Excludes a built-in fluent chaining protocol and a public soft-failure reporting API.

### MSTEST-018 — Soft scopes defer reportable failures, not postconditions

Reportable assertion failures inside `Assert.Scope()` shall collect until disposal, throwing the original single failure
or an `AssertFailedException` with an aggregate inner exception for multiple failures. Nested scopes shall be rejected.
`Fail`, `Inconclusive` and parameter validation shall remain hard; nullable/type postconditions are not enforced in scoped mode.
Checked: [AssertScope](../../src/TestFramework/TestFramework/Assertions/AssertScope.cs) and [Assert](../../src/TestFramework/TestFramework/Assertions/Assert.cs).
Existing tests: [AssertTests.ScopeTests](../../test/UnitTests/TestFramework.UnitTests/Assertions/AssertTests.ScopeTests.cs), `Scope_MultipleFailures_CollectsAllErrors`, `Scope_NestedScope_ThrowsInvalidOperationException`, `Scope_AssertIsNotNull_IsSoftFailure`.
Excludes safe use of value-returning failed assertions, arbitrary worker execution-context suppression and future hard-assert opt-ins.

### MSTEST-019 — Structured messages carry separate machine-readable values

The structured formatter shall order prefix/summary, optional user message, evidence blocks and optional call-site expression,
separating evidence/expression blocks with blank lines. Expected/actual text shall also be carried on assertion exceptions when supplied.
Checked: [StructuredAssertionMessage](../../src/TestFramework/TestFramework/Assertions/StructuredAssertionMessage.cs) and [Assert](../../src/TestFramework/TestFramework/Assertions/Assert.cs).
Existing tests: [StructuredAssertionMessageTests](../../test/UnitTests/TestFramework.UnitTests/Assertions/StructuredAssertionMessageTests.cs), `Format_FullMessage_CorrectLayout`, `WithExpectedAndActual_SetsProperties`.
Excludes a universal wording guarantee for all legacy/`Assert.That` methods and RFC 012's unimplemented truncation settings.

### MSTEST-020 — Assertion diagnostic snapshots are opt-in and bounded

Enabled supported-runtime capture shall record at most three assertion failures per execution attempt,
using version-1 JSON with bounded messages/values, active tests and stack frames.
Passing-test finalization shall discard its captured snapshots; nonpassing results shall attach available artifacts.
Checked: [capture/finalization](../../src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.AssertionFailureDiagnostics.cs)
and [writer](../../src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.AssertionFailureDiagnostics.Writer.cs);
the [schema](../mstest-assertion-failure-state.schema.md) remains the payload reference.
Existing tests: [AssertionFailureDiagnosticsTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Services/AssertionFailureDiagnosticsTests.cs), `CaptureShouldLimitAssertionFailuresPerAttempt`, `CaptureShouldBeDiscardedWhenTestPasses`; [acceptance test](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/AssertionFailureDiagnosticsTests.cs), `AssertionFailureDiagnostics_WhenEnabled_AttachesPersistedArtifactToFailedTest`.
Excludes local-variable inspection, suppressed-execution-context attribution, UWP/WinUI/browser/WASI/NativeAOT and cross-platform metric accuracy.

## Build-time composition

### MSTEST-021 — The SDK composes a runner and aligned packages

MSTest.Sdk shall default to the MTP runner and use the VSTest targets when `UseVSTest=true`.
`IsTestApplication=false` shall distinguish a library; NativeAOT shall add source generation, otherwise it is opt-in.
SDK source generation on .NET Standard shall fail with a runtime-hook availability diagnostic.
Checked: [Sdk.targets](../../src/Package/MSTest.Sdk/Sdk/Sdk.targets),
[ClassicEngine.targets](../../src/Package/MSTest.Sdk/Sdk/Runner/ClassicEngine.targets) and [NativeAOT.targets](../../src/Package/MSTest.Sdk/Sdk/Runner/NativeAOT.targets).
Existing tests: [SdkTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/SdkTests.cs), `SettingIsTestApplicationToFalseReducesAddedExtensionsAndMakesProjectNotExecutable`; [SourceGenerationNonAotTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/SourceGenerationNonAotTests.cs), `MSTestSdk_SourceGenerationOptIn_RejectsNetStandardTestLibrary`.
Excludes every profile/extension/CPM/SDK-layering permutation and compatibility of independently versioned installed packages.

### MSTEST-022 — Generated metadata has explicit fallback boundaries

Reflection-free generation shall register materializable attributes, supported invocation delegates and resolvable dynamic-data accessors.
Complete entries shall be authoritative; missing entries/general reflection contracts shall retain fallback.
Native MTP descriptor discovery shall bypass method enumeration only for supported descriptors, retaining fallback when incomplete.
Unsupported reflection-free shapes shall surface applicable `AOTSG0001`–`AOTSG0005` diagnostics.
Checked: [generator](../../src/Analyzers/MSTest.SourceGeneration/Generators/MSTestReflectionMetadataGenerator.cs),
[model builder](../../src/Analyzers/MSTest.SourceGeneration/Generators/TestClassModelBuilder.cs) and [runtime operations](../../src/Adapter/MSTestAdapter.PlatformServices/SourceGeneration/SourceGeneratedReflectionOperations.cs).
Existing tests: [generator tests](../../test/UnitTests/MSTest.SourceGeneration.UnitTests/MSTestReflectionMetadataGeneratorTests.cs), `Generator_DeclaresDescriptorSupportOnlyForBoundedSynchronousSubset`, `Generator_EmitsDynamicDataAccessor_ForPropertySource`; [runtime tests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/SourceGeneration/SourceGeneratedReflectionOperationsTests.cs), `TryGetTestMethodDescriptors_RetainsPerMethodFallbackWhenRegistrationIsIncomplete`.
Excludes universal reflection elimination, generated source locations and guaranteed recovery of omitted classes through trimming roots.

### MSTEST-023 — Analyzer diagnostics are authoring feedback

The resource-lock analyzer shall report bare literal keys on attributed classes/methods and accept shared constants,
without implementing runtime synchronization itself.
Checked: [PreferConstantForResourceLockAnalyzer](../../src/Analyzers/MSTest.Analyzers/PreferConstantForResourceLockAnalyzer.cs), `MSTEST0073`.
Existing tests: [PreferConstantForResourceLockAnalyzerTests](../../test/UnitTests/MSTest.Analyzers.UnitTests/PreferConstantForResourceLockAnalyzerTests.cs), `WhenResourceKeyIsBareLiteralOnMethod_Diagnostic`, `WhenResourceKeyIsUserConstant_NoDiagnostic`.
Excludes the complete analyzer/code-fix catalog, configured severities, all languages and proof that constants actually identify the same resource.

## Planned-run visibility

### MSTEST-024 — Planned-test snapshots describe selection, not execution

`TestRun.Current` shall start with a non-null empty snapshot. The runner shall publish the
host-filter-selected planned tests for a source before assembly initialization and retain that
snapshot until replacement in the same process/AppDomain. Subsequent programmatic filtering,
dependency failures, skips and cancellation can prevent planned tests from executing.
The planned snapshot shall not be interpreted as running/completed state or cleanup-time proof
that a test actually ran.
Checked: [TestRun](../../src/TestFramework/TestFramework.Extensions/TestRun.cs),
[UnitTestRunner](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/UnitTestRunner.cs) and
[source execution](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.SourceExecution.cs).
Existing tests: [TestRunInfoTests](../../test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/TestRunInfoTests.cs),
`TestRunCurrentIsNeverNull`, `SetCurrentNullResetsToEmpty`, `CreateFromPopulatesCategoriesAndProperties`,
`PlannedTestCopiesInputCollections`.
Excludes executed fixture-visibility and multi-source/AppDomain conformance, deep immutability,
and future running/completed snapshots or events. See [RFC 014](../RFCs/014-TestRun-Current-PlannedTests.md).

## Missing detailed contracts

The scoped baseline still lacks exhaustive contracts for public overload/source compatibility, assertion equivalence/diff/formatter semantics,
discovery serialization/IDs, row metadata precedence, ignore/filter-provider behavior, lifecycle error and cancellation propagation,
retry aggregation, output capture modes, deployment/assembly resolution, UI/STA/context flow, generated metadata completeness,
trim/AOT preservation, SDK/extension composition and cross-version host/package interoperability.
Historical proposals in the [RFC directory](../RFCs) are not substitutes for those implementation-backed contracts.
