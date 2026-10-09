# Microsoft.Testing.Platform observable contracts

## Status and use

- Status: **current implementation baseline (scoped)**.
- Inspected revision: `c5e1f5c613fa02ffa507db333e4702da6c97e488`.
- Inspected date: **2026-10-09**.
- Assurance for **every contract below**: source inspection and existing test mapping only.
  **No tests were executed** for this documentation change.
- Release/servicing verification and real composed SDK/IDE/CI validation: **not performed**.

Use with the [architecture](../architecture/mtp.md) and [maintenance guide](README.md).
“Must” describes the inspected local contract, not an unverified promise about an external
consumer. Named test links point to source lines at this baseline; tests are evidence mappings,
not assertions of current test success or exhaustive coverage.

## MTP-001 — Builder and framework ownership

An application registers a framework/capabilities factory before `BuildAsync`.
The builder rejects a second framework registration, a missing framework at build time, and
another successful build on the same builder. Application code owns registration; extensions
must not use service registration to substitute the framework.

- Implementation: [`TestApplicationBuilder`](../../src/Platform/Microsoft.Testing.Platform/Builder/TestApplicationBuilder.cs),
  [`ServiceProvider`](../../src/Platform/Microsoft.Testing.Platform/Services/ServiceProvider.cs).
- Existing tests: [`AddService_TestFramework_ShouldFail`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Services/ServiceProviderTests.cs#L134).
- Limit: service-level framework ownership is tested; no dedicated test was identified for every
  public-builder rejection path.

## MTP-002 — Enabled extensions and composite roles

Managers instantiate factories, validate identity/type uniqueness in their registration group,
check enablement and initialize enabled initializable extensions. A composite factory can supply
the same instance to multiple extension roles instead of creating independent consumers/lifetimes.
The platform registry is instance-based, not an `IServiceCollection` conversion.

- Implementation: [`ExtensionBuilderHelper`](../../src/Platform/Microsoft.Testing.Platform/Helpers/ExtensionBuilderHelper.cs),
  [`TestHostManager`](../../src/Platform/Microsoft.Testing.Platform/TestHost/TestHostManager.cs).
- Existing tests: [`DataConsumer_DuplicatedId_ShouldFail`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestApplicationBuilderTests.cs#L51),
  [`TestHost_ComposeFactory_ShouldSucceed`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestApplicationBuilderTests.cs#L95).

## MTP-003 — Discovery, run and explicit completion

The console invoker creates a framework session, constructs either a discovery or run request,
executes it, and closes the framework session on the normal path. The framework must call
`ExecuteRequestContext.Complete()`; returning its task alone does not complete the request.
Unsuccessful framework session results are recorded as session failures. Exception paths do
not imply that `CloseTestSessionAsync` always runs.

- Implementation: [`TestHostTestFrameworkInvoker`](../../src/Platform/Microsoft.Testing.Platform/Requests/TestHostTestFrameworkInvoker.cs),
  [`ConsoleTestExecutionRequestFactory`](../../src/Platform/Microsoft.Testing.Platform/Requests/ConsoleTestExecutionRequestFactory.cs).
- Existing tests: [`Exec_Honor_Request_Complete`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/ExecutionRequestCompleteTests.cs#L13),
  [`TestExecutionTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestFramework/TestExecutionTests.cs).

## MTP-004 — Message delivery and session finishing

Producers must declare the concrete types they publish. Unknown produced types fail; a valid
message with no consumer is dropped. Ordinary consumers run asynchronously; a blocking consumer
holds up publication until consumption finishes. Session finishing drains execution messages,
notifies non-consumers before consumers and drains between consumer finishers.
Teardown disables the bus before disposing consumers; a consumer still running after bounded
canceled shutdown is deliberately not disposed concurrently.

- Implementation: [`AsynchronousMessageBus`](../../src/Platform/Microsoft.Testing.Platform/Messages/AsynchronousMessageBus.cs),
  [`session notifications`](../../src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.SessionNotifications.cs),
  [`disposal`](../../src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.Disposal.cs).
- Existing tests: [`UnexpectedTypePublished_ShouldFail`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Messages/AsynchronousMessageBusTests.cs#L21),
  [`BlockingDataConsumer_PublishAsync_DoesNotReturnUntilConsumeCompletes`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Messages/AsynchronousMessageBusTests.cs#L319),
  [`DisposeServiceProviderAsync_WhenConsumerIsStillRunning_DoesNotDisposeIt`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Hosts/CommonHostTests.cs#L351).

## MTP-005 — Configuration and CLI validation

Lower source `Order` has higher priority; first matching provider wins. Built-in orders are CLI
`0`, environment `1`, JSON `3`; a configuration bridge defaults to `2`.
Explicit CLI option data shadows JSON data for the same option rather than mixing argument lists.
Passive `commandLineOptionDefaults` supply arguments without activating the feature.
Validation rejects duplicate/reserved/unknown options, wrong arity/values, invalid combinations
and bootstrap-only options placed in JSON. VSTest migration diagnostics do not rewrite options.

- Implementation: [`ConfigurationManager`](../../src/Platform/Microsoft.Testing.Platform/Configurations/ConfigurationManager.cs),
  [`AggregatedConfiguration`](../../src/Platform/Microsoft.Testing.Platform/Configurations/AggregatedConfiguration.cs),
  [`CommandLineOptionsValidator`](../../src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.cs),
  [`migration diagnostics`](../../src/Platform/Microsoft.Testing.Platform/CommandLine/CommandLineOptionsValidator.UnknownAndBootstrapValidation.cs).
- Existing tests: [`BuiltInSources_UseExpectedCommonDefaults`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Configuration/ConfigurationSourceDefaultsTests.cs#L14),
  [`ProviderAwareResolution_CliZeroArityShadowsJsonIndexedArgs`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Configuration/CommandLineConfigurationProviderTests.cs#L140),
  [`JsonCommandLineOptionsTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Configuration/JsonCommandLineOptionsTests.cs),
  [`ParseAndValidateAsync_VSTestOption_SuggestsMTPReplacement`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/CommandLine/CommandLineHandlerTests.cs#L672).

## MTP-006 — Configured child environment

A non-empty flat `testconfig.json.environmentVariables` section enables the built-in controller
provider on supported platforms. Its unlocked values run first; later providers can override them.
Writing over a locked provider value fails, rather than silently applying “last wins.”
Declarations override inherited values in the child, not in the controller's environment.
Absent/empty declarations do not themselves force a child; `--list-tests` skips this controller path.

- Implementation: [`TestConfigurationEnvironmentVariableProvider`](../../src/Platform/Microsoft.Testing.Platform/TestHostControllers/TestConfigurationEnvironmentVariableProvider.cs),
  [`EnvironmentVariables`](../../src/Platform/Microsoft.Testing.Platform/TestHostControllers/EnvironmentVariables.cs),
  [`host mode selection`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostBuilder.Modes.cs).
- Existing tests: [`UpdateAsync_AppliesAllEntries`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestHostControllers/TestConfigurationEnvironmentVariableProviderTests.cs#L53),
  [`EnvironmentVariablesSection_Absent_TestHostRunsInProcess`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/TestConfigEnvironmentVariablesTests.cs#L46).

## MTP-007 — Selection and filter composition

Console selection is no-op, UID-list or tree-query selection. Enabled providers may add known
constraints: null/no-op opts out, UID lists intersect with ordinal equality, nested ANDs flatten,
and disjoint UID selections remain match-none. No contributions preserves the original filter.
JSON-RPC `tests` (including an empty array) takes precedence over `filter`; omitted selection
means no-op. Server providers are called but non-no-op contributions fail.
Frameworks own evaluation and must not silently discard unsupported representations.
Tree property matching uses `TestMetadataProperty`; `**` cannot occur in the middle of a path.

- Implementation: [`composer`](../../src/Platform/Microsoft.Testing.Platform/Requests/TestExecutionFilterComposer.cs),
  [`server selection`](../../src/Platform/Microsoft.Testing.Platform/Hosts/ServerTestHost.RequestExecution.cs),
  [`tree matching`](../../src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.Matching.cs),
  [`VSTestBridge translation`](../../src/Platform/Microsoft.Testing.Extensions.VSTestBridge/ObjectModel/ContextAdapterBase.cs).
- Existing tests: [`ComposeAsync_WithTwoUidProviders_IntersectsIndependentlyOfRegistrationOrder`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Requests/TestExecutionFilterComposerTests.cs#L127),
  [`ComposeAsync_ForServerWithProviderConstraint_ThrowsActionableError`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Requests/TestExecutionFilterComposerTests.cs#L269),
  [`RunRequestWithEmptyTests_PreservesEmptyUidSelection`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/ServerMode/ServerTests.cs#L907),
  [`TreeNodeFilterTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Requests/TreeNodeFilterTests.cs),
  [`GetTestCaseFilter_WithAndComposite_TranslatesChildrenRecursively`](../../test/UnitTests/Microsoft.Testing.Extensions.VSTestBridge.UnitTests/ObjectModel/RunContextAdapterFilterTests.cs#L184).

## MTP-008 — Cancellation and process verdicts

Caller cancellation, Ctrl+C and host timeout feed application cancellation; canceled common-host
runs normalize to `TestSessionAborted` (`3`) during cleanup. A reverse-control stop prefers the
framework's graceful-stop capability, falling back to application cancellation; it is not always
equivalent to immediately canceling every test.
The result service prioritizes maximum-failure stop, framework-session failure, test failure,
abort, deadline truncation, count policy, then coverage failure. Explicit minimum count supersedes
zero-tests policy. Default `allow-skipped` permits all-skipped success; `strict` yields `8`.
Ignoring a configured exit code is an explicit opt-out, not proof that tests executed.

- Implementation: [`CommonHost`](../../src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs),
  [`TestApplicationResult`](../../src/Platform/Microsoft.Testing.Platform/Services/TestApplicationResult.cs),
  [`ExitCodeIgnorePolicy`](../../src/Platform/Microsoft.Testing.Platform/Services/ExitCodeIgnorePolicy.cs).
- Existing tests: [`RequestGracefulSessionStopAsync_UsesActiveRequestCapabilitiesBeforeApplicationCapability`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Hosts/CommonHostTests.cs#L27),
  [`GetProcessExitCodeAsync_If_All_Skipped_With_StrictPolicy_Returns_ZeroTestsRan`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Services/TestApplicationResultTests.cs#L468),
  [`GetProcessExitCodeAsync_If_MinimumExpectedTests_Set_And_No_Tests_Ran_Returns_MinimumExpectedTestsPolicyViolation`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Services/TestApplicationResultTests.cs#L645).

## MTP-009 — Controller, launcher and process isolation

Enabled controller requirements select an out-of-process host; an enabled custom launcher alone
also forces this path. Only one enabled launcher is allowed. The platform supplies finalized
arguments/environment/working directory, owns IPC/lifetime notifications and monitors the returned
handle. The launcher substitutes creation/start, not the entire run loop.
Controller cancellation requests cooperative child shutdown with a bounded termination fallback.
An application service-provider clone does not transfer live objects into the child.

- Implementation: [`TestHostControllersManager`](../../src/Platform/Microsoft.Testing.Platform/TestHostControllers/TestHostControllersManager.cs),
  [`custom launch`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostControllersTestHost.CustomLauncher.cs),
  [`cancellation teardown`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostControllersTestHost.CancellationTeardown.cs).
- Existing tests: [`CustomLauncher_IsUsedToStartTestHost_AndRunSucceeds`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/TestHostLauncherTests.cs#L13),
  [`TestHostControllerCancellationTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Hosts/TestHostControllerCancellationTests.cs).
- Limit: local launch fixtures do not establish RFC remote/container/deployment examples.

## MTP-010 — JSON-RPC initialization and request scope

Initialization negotiates a supported JSON-RPC protocol version before normal requests.
The server advertises `multiRequestSupport: false` and `multiConnectionProvider: false`;
the protocol's illustrative parallel-request possibilities are not current advertised capabilities.
Each discovery/run invocation receives request-owned session/cancellation/result services.
Request cancellation is distinct from terminating the application; empty selections stay empty.

- Implementation: [`request handling`](../../src/Platform/Microsoft.Testing.Platform/Hosts/ServerTestHost.RequestHandling.cs),
  [`request execution`](../../src/Platform/Microsoft.Testing.Platform/Hosts/ServerTestHost.RequestExecution.cs),
  [`completion`](../../src/Platform/Microsoft.Testing.Platform/Hosts/ServerTestHost.RequestCompletion.cs).
- Existing tests: [`ServerEnforcesLifecycleAndNegotiatesProtocolVersion`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/ServerMode/ServerTests.cs#L217),
  [`NumericAndStringRequestIdsHaveIndependentCancellation`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/ServerMode/ServerTests.cs#L779).

## MTP-011 — Native binary channel versus legacy MSBuild

`--server dotnettestcli` selects the host-initiated binary pipe/HTTP channel, not JSON-RPC.
The host advertises versions and validates the version returned by the peer; incompatibility
prevents normal common-host execution. Execution ID follows a root application's process tree;
it is not a solution-wide identifier. Control-pipe support requires an advertised pipe capability.
Legacy `TestingPlatformDotnetTestSupport` instead routes the VSTest target to `InvokeTestingPlatform`
and its MSBuild consumer/task IPC; the two paths must not be conflated.

- Implementation: [`DotnetTestConnection`](../../src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs),
  [`wire constants`](../../src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/Constants.cs),
  [`legacy target`](../../src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.VSTest.targets).
- Existing tests: [`DotnetTestPipe_TestAppAdvertisesAllSupportedVersions_NegotiatesDownToV100WithOldSdk`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/DotnetTestPipeBaselineTests.cs#L35),
  [`MSBuildTests.Test`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuildTests.Test.cs).
- Limit: fake-SDK fixtures test local wire behavior, not an installed SDK's negotiation/election.

## MTP-012 — Report artifacts and post-processing

Reports consume test/artifact messages and publish file artifacts with optional format kinds.
The dispatcher selects processors for the requested mode/truncation context, matches a declared
kind exactly, and uses extension fallback only for untyped inputs. It validates that output exists,
is under the output directory and is not an input file, then reports inputs represented by that output.
Processor failure warns and gives the dispatcher a nonzero result; external orchestration decides
whether that failure affects the parent run. Local dispatcher behavior does not prove SDK election
or merged-summary replacement. JUnit uses bounded parent-chain keys, not the RFC's uncapped keys.

- Implementation: [`dispatcher`](../../src/Platform/Microsoft.Testing.Platform/Extensions/ArtifactPostProcessing/ArtifactPostProcessingDispatcherTool.cs),
  [`capability advertisement`](../../src/Platform/Microsoft.Testing.Platform/Extensions/ArtifactPostProcessing/ArtifactPostProcessingHandshakeProperties.cs),
  [`JUnit generator`](../../src/Platform/Microsoft.Testing.Extensions.JUnitReport/JUnitReportGenerator.cs).
- Existing tests: [`ValidateProcessedArtifact_RejectsInvalidPaths`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Extensions/ArtifactPostProcessing/ArtifactPostProcessingTests.cs#L195),
  [`Dispatcher_AdvertisesCapabilityAndReturnsMergedArtifact`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/DotnetTestPipeArtifactPostProcessingTests.cs#L16).

## MTP-013 — Coverage gates consume measurements, not report files

The platform correlates coverage/threshold/report messages in `ITestCoverageResult`.
Line/branch gates use overall covered/coverable measurements; missing/empty/ambiguous metric data
fails a requested gate, even at zero threshold. A file artifact alone is insufficient.
Coverage failure returns `14` only when the run otherwise succeeds, including controller-finalized
coverage. Each application is gated separately; this is not solution-wide measurement aggregation.

- Implementation: [`TestCoverageResult`](../../src/Platform/Microsoft.Testing.Platform/Services/TestCoverageResult.cs),
  [`CoverageThresholdPolicy`](../../src/Platform/Microsoft.Testing.Platform/Services/CoverageThresholdPolicy.cs),
  [`controller exit policy`](../../src/Platform/Microsoft.Testing.Platform/Services/CoverageThresholdExitCodePolicy.cs).
- Existing tests: [`CoverageThresholdPolicyTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/Services/CoverageThresholdPolicyTests.cs),
  [`CoverageThresholdControllerExitCodeTests`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/CoverageThresholdControllerExitCodeTests.cs).
- Limit: no external collector/version interoperability was executed or verified.

## MTP-014 — Microsoft.Extensions ownership remains explicit

Logging bridges are additive; caller-owned factories are not disposed by MTP and no diagnostic
pipeline is constructed by the delegate overload when effective logging is `None`.
Configuration is a read-only build-time snapshot, not a live reload bridge.
Hosting starts the outer host before MTP build, stops it after the run, and leaves disposal to the
caller. Caller cancellation/`ApplicationStopping` request MTP cancellation; MTP does not call
`StopApplication`. These objects are process-local, not forwarded to child processes.

- Implementation: [`logging bridge`](../../src/Platform/Microsoft.Testing.Extensions.Logging/MicrosoftExtensionsLoggingBuilderExtensions.cs),
  [`snapshot source`](../../src/Platform/Microsoft.Testing.Extensions.Configuration/MicrosoftExtensionsConfigurationSnapshotSource.cs),
  [`hosting bridge`](../../src/Platform/Microsoft.Testing.Extensions.Hosting/MicrosoftExtensionsHostingExtensions.cs).
- Existing tests: [`MicrosoftExtensionsLoggingBuilderExtensionsTests`](../../test/UnitTests/Microsoft.Testing.Extensions.UnitTests/MicrosoftExtensionsLoggingBuilderExtensionsTests.cs),
  [`RunTestingPlatformAsync_StartsHostBeforeBuildingMtpAndStopsAfterRun`](../../test/UnitTests/Microsoft.Testing.Extensions.UnitTests/MicrosoftExtensionsHostingExtensionsTests.cs#L147),
  [`RunTestingPlatformAsync_WhenMtpStops_DoesNotRequestHostStopBeforeCleanup`](../../test/UnitTests/Microsoft.Testing.Extensions.UnitTests/MicrosoftExtensionsHostingExtensionsTests.cs#L487).

## MTP-015 — Dynamic hooks are explicit application code

Without `--enable-dynamic-extensions`, manifests are not inspected. Discovery uses the application
directory, not working directory; hooks run before caller/static registration on the real builder.
They must be synchronous public static `void AddExtensions(ITestApplicationBuilder, string[])`
and cannot register a test framework. Errors fail loading; .NET load-context isolation is not a
security sandbox, and the netstandard/.NET Framework path does not provide that isolation.

- Implementation: [`loader`](../../src/Platform/Microsoft.Testing.Platform/DynamicExtensions/DynamicExtensionLoader.cs),
  [`assembly loader`](../../src/Platform/Microsoft.Testing.Platform/DynamicExtensions/DynamicExtensionAssemblyLoader.cs),
  [`builder guard`](../../src/Platform/Microsoft.Testing.Platform/Builder/TestApplicationBuilder.cs).
- Existing tests: [`DiscoverManifests_LooksInTheApplicationDirectoryNotTheWorkingDirectory`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/DynamicExtensions/DynamicExtensionLoaderTests.cs#L94),
  [`LoadAsync_HookCannotRegisterATestFramework`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/DynamicExtensions/DynamicExtensionLoaderTests.cs#L447),
  [`DynamicExtensionAssemblyLoaderTests`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/DynamicExtensions/DynamicExtensionAssemblyLoaderTests.cs).

## MTP-016 — No-handshake and undeclared-capability fallback

Required post-processing support is true only after the peer explicitly returns a parseable `true`
capability. Missing/false values are unsupported, not inferred from protocol version or a connected
pipe. CI summary deferral additionally has local retry/controller conditions; it must not be described
as unconditional for native `dotnet test`. Without downstream ownership, the controller summary
finalizer processes matching fragments locally. This is not a claim that an absent handshake on a
requested binary connection succeeds: incompatible/missing version replies fail negotiation.

- Implementation: [`handshake capability parsing`](../../src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs),
  [`GitHub summary ownership`](../../src/Platform/Microsoft.Testing.Extensions.GitHubActionsReport/GitHubActionsExtensions.cs),
  [`controller finalizer`](../../src/Platform/SharedExtensionHelpers/CiCoverageSummaryControllerHandler.cs).
- Existing tests: [`AddProvider_FinalizesMatchingFragmentAndPreservesProviderDeferral`](../../test/UnitTests/Microsoft.Testing.Extensions.UnitTests/CiCoverageSummaryControllerHandlerRegistrationTests.cs#L118),
  [`DotnetTestPipeArtifactPostProcessingTests`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/DotnetTestPipeArtifactPostProcessingTests.cs).
- Limit: direct parsing/no-connection reasoning is source evidence; this mapping is not a complete
  normal/legacy/cancel/partial/multi-module composed-product compatibility matrix.

## MTP-017 — Optional AI provider availability

Only one chat-client provider can be registered; `GetChatClientAsync` returns null when the provider
is missing or unavailable. The Microsoft.Extensions.AI-facing contract belongs to
`Microsoft.Testing.Platform.AI`; the core stores the provider without taking that external dependency.
Availability does not establish remote credentials, model capability or analysis correctness.

- Implementation: [`ChatClientProviderExtensions`](../../src/Platform/Microsoft.Testing.Platform.AI/ChatClientProviderExtensions.cs),
  [`ChatClientManager`](../../src/Platform/Microsoft.Testing.Platform/AI/ChatClientManager.cs).
- Existing tests: [`AddChatClientProvider_WhenProviderAlreadyRegistered_Throws`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/AI/ChatClientProviderExtensionsTests.cs#L69),
  [`GetChatClientAsync_WhenProviderIsUnavailable_ReturnsNull`](../../test/UnitTests/Microsoft.Testing.Platform.UnitTests/AI/ChatClientProviderExtensionsTests.cs#L92).

## Missing detailed contracts

This initial slice does not specify all wire fields, report schemas/recovery/attachment copying,
retry/hot-reload scheduling, dump/video platform behavior, package generation/import ordering,
all CLI option combinations, AI-provider networking or telemetry semantics.
See [architecture coverage limits](../architecture/mtp.md#deliberate-coverage-limits).
Changes spanning SDK/IDE/collector packages need real shipping-layout validation with exact
versions, exit codes, selected-versus-executed counts and artifacts before claiming compatibility.
