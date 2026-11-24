// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.InvocationLifetime;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private const string RetryPipeNameOptionKey = "internal-retry-pipename";

    private async Task<IHost> BuildHostAsync(BuildContext context)
    {
        string? orchestratorChain = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN);
        if (orchestratorChain is not null)
        {
            _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN, null);
        }

        if (context.EarlyHost is not null)
        {
            return context.EarlyHost;
        }

        IReadOnlyList<IArtifactPostProcessor> artifactPostProcessors =
            await ((ArtifactPostProcessingManager)ArtifactPostProcessing).BuildAsync(context.ServiceProvider).ConfigureAwait(false);
        context.ServiceProvider.AddServices([.. artifactPostProcessors]);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        DotnetTestConnection pushOnlyProtocol = new(context.CommandLineHandler, _environment, _testApplicationModuleInfo, context.TestApplicationCancellationTokenSource, context.LoggerFactory.CreateLogger<DotnetTestConnection>());
        if (!context.LoggingState.CommandLineParseResult.HasTool
            || context.LoggingState.CommandLineParseResult.ToolName == ArtifactPostProcessingDispatcherTool.ToolName)
        {
            await pushOnlyProtocol.AfterCommonServiceSetupAsync().ConfigureAwait(false);
        }

        if (pushOnlyProtocol.IsServerMode)
        {
            context.ServiceProvider.AddService(pushOnlyProtocol);
        }

        IReadOnlyList<ITool> toolsInformation = await BuildToolsInformationAsync(context).ConfigureAwait(false);
        if (context.LoggingState.CommandLineParseResult.HasTool)
        {
            return await BuildToolsHostAsync(context, toolsInformation).ConfigureAwait(false);
        }

        if (context.IsHelpCommand)
        {
            if (pushOnlyProtocol.IsServerMode)
            {
                // Always perform the handshake (even though we are about to print help) so that
                // dotnet test on the SDK side can validate the test host's execution mode and
                // detect mismatches such as --help being injected via RunArguments during a
                // normal run. The handshake is needed for the SDK to know that the test host
                // is in help mode, so it can ignore any incoming CommandLineOptionMessages.
                bool isProtocolCompatible = await pushOnlyProtocol.IsCompatibleProtocolAsync(
                    HandshakeMessageHostTypes.TestHost,
                    ArtifactPostProcessingHandshakeProperties.Create(artifactPostProcessors)).ConfigureAwait(false);
                if (!isProtocolCompatible)
                {
                    CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
                    return new InformativeCommandLineHost((int)ExitCode.IncompatibleProtocolVersion, context.ServiceProvider);
                }

                await pushOnlyProtocol.HelpInvokedAsync().ConfigureAwait(false);
            }
            else
            {
                await context.CommandLineHandler.PrintHelpAsync(context.ProxyOutputDevice, toolsInformation, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            }

            CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
            return new InformativeCommandLineHost(0, context.ServiceProvider);
        }
#pragma warning restore CA1416 // Preserve existing browser behavior while splitting the method.

        if (context.IsInfoCommand)
        {
            await context.CommandLineHandler.PrintInfoAsync(context.ProxyOutputDevice, toolsInformation, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
            return new InformativeCommandLineHost(0, context.ServiceProvider);
        }

        context.InvocationLifetimeHandlers = await BuildInvocationLifetimeHandlersIfOwnerAsync(context).ConfigureAwait(false);

        try
        {
            return await TryBuildTestHostOrchestratorHostAsync(context, orchestratorChain).ConfigureAwait(false) is { } testHostOrchestratorHost
                ? testHostOrchestratorHost
                : await TryBuildTestHostControllersHostAsync(context, orchestratorChain).ConfigureAwait(false) is { } testHostControllerHost
                ? testHostControllerHost
                : await BuildTestHostModeAsync(context).ConfigureAwait(false);
        }
        catch
        {
            await ReleaseInvocationLifetimeHandlersAsync(context.InvocationLifetimeHandlers).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<IReadOnlyList<ITool>> BuildToolsInformationAsync(BuildContext context)
    {
        var toolsServiceProvider = (ServiceProvider)context.ServiceProvider.Clone();
        toolsServiceProvider.TryAddService(context.ProxyOutputDevice);
        return await ((ToolsManager)Tools).BuildAsync(toolsServiceProvider).ConfigureAwait(false);
    }

    private async Task<IHost> BuildToolsHostAsync(BuildContext context, IReadOnlyList<ITool> toolsInformation)
    {
        context.ServiceProvider.TryAddService(context.ProxyOutputDevice);

        ToolsHost toolsHost = new(toolsInformation, context.ServiceProvider, context.CommandLineHandler, context.ProxyOutputDevice);
        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.Tool,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        CompleteBuilderActivity(context.BuilderActivity, nameof(ToolsHost));
        return toolsHost;
    }

    private async Task<IHost?> TryBuildTestHostOrchestratorHostAsync(BuildContext context, string? orchestratorChain)
    {
        if (context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return null;
        }

        TestHostOrchestratorConfiguration testHostOrchestratorConfiguration = await _testHostOrchestratorManager.BuildAsync(context.ServiceProvider, orchestratorChain).ConfigureAwait(false);

        // Middleware only wraps an existing orchestrator invocation (see
        // ITestHostExecutionOrchestratorMiddleware); it does not stand in for one. Registering middleware
        // without also registering exactly one orchestrator is a misconfiguration, not silently ignored.
        bool hasActiveMiddleware = testHostOrchestratorConfiguration.Middleware.Length > 0;

        if (testHostOrchestratorConfiguration.TestHostOrchestrators.Length == 0 && !hasActiveMiddleware)
        {
            return null;
        }

        ValidateTestHostOrchestratorMiddlewareConfiguration(
            testHostOrchestratorConfiguration.TestHostOrchestrators.Length,
            testHostOrchestratorConfiguration.Middleware.Length);

        if (testHostOrchestratorConfiguration.TestHostOrchestrators.Any(
            static orchestrator => orchestrator is ITestHostControllerConnectionAuthorizationConsumer))
        {
            ITestHostLauncher? testHostLauncher =
                await ((TestHostControllersManager)TestHostControllers).BuildTestHostLauncherAsync(context.ServiceProvider).ConfigureAwait(false);
            ExecutableInfo executableInfo = context.ServiceProvider.GetTestApplicationModuleInfo().GetCurrentExecutableInfo();
            await context.ServiceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
                testHostLauncher,
                executableInfo.FilePath,
                context.LoggerFactory.CreateLogger<TestHostBuilder>(),
                context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }

        context.PoliciesService.ProcessRole = TestProcessRole.TestHostOrchestrator;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostOrchestrator, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        ITestHostOrchestratorApplicationLifetime[] orchestratorLifetimes = testHostOrchestratorConfiguration.IsOutermostProcess
            ? await _testHostOrchestratorManager.BuildTestHostOrchestratorApplicationLifetimesAsync(context.ServiceProvider).ConfigureAwait(false)
            : [];
        context.ServiceProvider.AddServices(orchestratorLifetimes);
        AddInvocationLifetimeRunner(
            context,
            context.ServiceProvider,
            setOwnerMarker: true,
            setupTimeout: context.GlobalTestTimeout);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostOrchestratorHost));
        return new TestHostOrchestratorHost(testHostOrchestratorConfiguration, context.ServiceProvider);
    }

    internal static void ValidateTestHostOrchestratorMiddlewareConfiguration(int orchestratorCount, int middlewareCount)
    {
        if (middlewareCount > 0 && orchestratorCount != 1)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.TestHostExecutionOrchestratorMiddlewareRequiresExactlyOneOrchestratorErrorMessage,
                    orchestratorCount));
        }
    }

    private async Task<IHost?> TryBuildTestHostControllersHostAsync(BuildContext context, string? orchestratorChain)
    {
        if (ShouldSkipTestHostControllersHost(
            context.TestHostControllerInfo,
            context.CommandLineHandler,
            context.SystemEnvironment))
        {
            return null;
        }

        PassiveNode? passiveNode = null;
        if (context.IsJsonRpcProtocol)
        {
            IMessageHandlerFactory messageHandlerFactory = ServerModeManager.Build(context.ServiceProvider);
            passiveNode = new PassiveNode(
                messageHandlerFactory,
                context.TestApplicationCancellationTokenSource,
                context.SystemEnvironment,
                context.SystemMonitorAsyncFactory,
                context.LoggerFactory.CreateLogger<PassiveNode>());
        }

        var testHostControllersServiceProvider = (ServiceProvider)context.ServiceProvider.Clone();
        testHostControllersServiceProvider.TryAddService(context.ProxyOutputDevice);
        testHostControllersServiceProvider.TryAddService(new MessageBusProxy());

        TestHostControllerConfiguration testHostControllers = await ((TestHostControllersManager)TestHostControllers).BuildAsync(testHostControllersServiceProvider).ConfigureAwait(false);
        if (!testHostControllers.RequireProcessRestart)
        {
            return null;
        }

        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        context.TestHostControllerInfo.IsCurrentProcessTestHostController = true;
        AddInvocationLifetimeRunner(
            context,
            testHostControllersServiceProvider,
            setOwnerMarker: true,
            setupTimeout: context.GlobalTestTimeout);
        context.PoliciesService.ProcessRole = TestProcessRole.TestHostController;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostController, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        TestHostControllersTestHost testHostControllersHost = new(
            testHostControllers,
            testHostControllersServiceProvider,
            passiveNode,
            context.SystemEnvironment,
            context.LoggerFactory,
            context.SystemClock)
        {
            OrchestratorChain = orchestratorChain,
        };

        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.TestHostControllers,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostControllersTestHost));
        return testHostControllersHost;
    }

    private async Task<IReadOnlyList<IInvocationLifetimeHandler>?> BuildInvocationLifetimeHandlersIfOwnerAsync(BuildContext context)
    {
        // The controller PID and retry pipe identify children even when the launch does not inherit the
        // owner's environment (for example an AUMID-activated packaged application).
        if (!((InvocationLifetimeManager)InvocationLifetime).HasHandlers
            || context.IsJsonRpcProtocol
            || context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            || context.TestHostControllerInfo.HasTestHostController
            || context.CommandLineHandler.IsOptionSet(RetryPipeNameOptionKey)
            || !RoslynString.IsNullOrEmpty(context.SystemEnvironment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER)))
        {
            return null;
        }

        // Kept even when every handler is disabled: the owner still publishes its marker, so a handler that a child
        // process sees as enabled (for example through a variable set by an environment provider) does not run there
        // once per test host process.
        return await ((InvocationLifetimeManager)InvocationLifetime).BuildAsync(context.ServiceProvider).ConfigureAwait(false);
    }

    private static void AddInvocationLifetimeRunner(
        BuildContext context,
        ServiceProvider serviceProvider,
        bool setOwnerMarker,
        TimeSpan? setupTimeout = null)
    {
        if (context.InvocationLifetimeHandlers is not { } handlers)
        {
            return;
        }

        serviceProvider.AddService(new InvocationLifetimeRunner(
            handlers,
            context.SystemEnvironment,
            context.ProxyOutputDevice,
            context.LoggerFactory.CreateLogger<InvocationLifetimeRunner>(),
            setOwnerMarker,
            restoreEnvironment: false,
            setupTimeout: setupTimeout));
    }

    private static async Task ReleaseInvocationLifetimeHandlersAsync(IReadOnlyList<IInvocationLifetimeHandler>? handlers)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (IInvocationLifetimeHandler handler in handlers)
        {
            try
            {
                await DisposeHelper.DisposeAsync(handler).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNonFatalInvocationLifetimeException(ex))
            {
                // Host construction already failed; release every handler without replacing that failure.
            }
        }
    }

    private static bool IsNonFatalInvocationLifetimeException(Exception exception)
        => exception is AggregateException aggregateException
            ? aggregateException.Flatten().InnerExceptions.All(IsNonFatalInvocationLifetimeException)
            : exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException
                and not AppDomainUnloadedException
                and not BadImageFormatException;

    private static void ClearInvocationLifetimeEnvironmentVariables(IEnvironment environment)
    {
        List<string>? names = null;
        foreach (DictionaryEntry entry in environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name
                && name.StartsWith(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                (names ??= []).Add(name);
            }
        }

        if (names is null)
        {
            return;
        }

        foreach (string name in names)
        {
            environment.SetEnvironmentVariable(name, null);
        }
    }

    internal static bool ShouldSkipTestHostControllersHost(
        ITestHostControllerInfo testHostControllerInfo,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment)
        => (testHostControllerInfo.HasTestHostController
                && environment.GetEnvironmentVariable(
                    $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{testHostControllerInfo.GetTestHostControllerPID()}") == "1")
            || (commandLineOptions.IsOptionSet(RetryPipeNameOptionKey)
                && environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION) == "1")
            || commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);

    private async Task<IHost> BuildTestHostModeAsync(BuildContext context)
    {
        // A test host is a leaf of the invocation: clear the reserved variables it inherited, so an application
        // launched by a test starts its own invocation instead of believing it is a participant of this one.
        ClearInvocationLifetimeEnvironmentVariables(context.SystemEnvironment);

        context.PoliciesService.ProcessRole = TestProcessRole.TestHost;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHost, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        string? currentWorkingDirectory = context.Configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ApplicationStateGuard.Ensure(currentWorkingDirectory is not null);

        context.TestHostControllerInfo.IsCurrentProcessTestHostController = false;

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        TestHostControllerCancellationListener? testHostControllerCancellationListener =
            CreateTestHostControllerCancellationListenerIfAvailable(
                context.TestApplicationCancellationTokenSource,
                context.LoggerFactory.CreateLogger(nameof(TestHostControllerCancellationListener)),
                context.TestHostControllerInfo,
                context.SystemEnvironment);
        NamedPipeClient? testControllerConnection = null;
        try
        {
            // Start the auxiliary listener before awaiting the primary handshake so cancellation can reach
            // the child even while its primary connection or PID request is still in progress.
            testControllerConnection = await ConnectToTestHostProcessMonitorIfAvailableAsync(
                context.TestApplicationCancellationTokenSource,
                context.LoggerFactory.CreateLogger(nameof(ConnectToTestHostProcessMonitorIfAvailableAsync)),
                context.TestHostControllerInfo,
                context.Configuration,
                context.SystemEnvironment).ConfigureAwait(false);
#pragma warning restore CA1416 // Preserve existing browser behavior while splitting the method.

#pragma warning disable CS0618 // Type or member is obsolete
            ITestHostApplicationLifetime[] testApplicationLifecycleCallback =
                await ((TestHostManager)TestHost).BuildTestApplicationLifecycleCallbackAsync(context.ServiceProvider).ConfigureAwait(false);
#pragma warning restore CS0618 // Type or member is obsolete
            context.ServiceProvider.AddServices(testApplicationLifecycleCallback);

            ITestExecutionFilterProvider[] testExecutionFilterProviders =
                await ((TestHostManager)TestHost).BuildTestExecutionFilterProvidersAsync(context.ServiceProvider).ConfigureAwait(false);
            context.ServiceProvider.AddServices(testExecutionFilterProviders);

            return context.IsJsonRpcProtocol
                ? await BuildServerTestHostAsync(context, testControllerConnection, testHostControllerCancellationListener).ConfigureAwait(false)
                : await BuildConsoleTestHostAsync(context, testControllerConnection, testHostControllerCancellationListener).ConfigureAwait(false);
        }
        catch
        {
            await DisposeHelper.DisposeAsync(testHostControllerCancellationListener).ConfigureAwait(false);
#pragma warning disable CA1416 // This method only creates named-pipe connections on supported platforms.
            testControllerConnection?.Dispose();
#pragma warning restore CA1416
            throw;
        }
    }

    private async Task<IHost> BuildServerTestHostAsync(
        BuildContext context,
        NamedPipeClient? testControllerConnection,
        TestHostControllerCancellationListener? testHostControllerCancellationListener)
    {
        IMessageHandlerFactory messageHandlerFactory = ServerModeManager.Build(context.ServiceProvider);
        ServerTestHost serverTestHost = new(
            context.ServiceProvider,
            BuildTestFrameworkAsync,
            messageHandlerFactory,
            (TestFrameworkManager)TestFramework!,
            (TestHostManager)TestHost);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        IHost actualTestHost = serverTestHost;
        if (testControllerConnection is not null)
        {
            var controlledHost = new TestHostControlledHost(
                testControllerConnection,
                serverTestHost,
                context.TestApplicationCancellationTokenSource.CancellationToken,
                context.ServiceProvider.GetRequiredService<TestApplicationResult>());
            controlledHost.SetCoverageResult(context.ServiceProvider.GetRequiredService<TestCoverageResult>());
            controlledHost.SetCancellationListener(testHostControllerCancellationListener);
            actualTestHost = controlledHost;
        }
#pragma warning restore CA1416 // Preserve existing browser behavior while splitting the method.

        // The TestHostBuilt telemetry event must be sent through the collector previously registered
        // by TelemetryManager in SetupCommonServicesAsync (a real collector when telemetry is opted in,
        // or NopTelemetryService when opted out) because the ServerTestHost JSON-RPC message handler is
        // not yet initialized at this point (it is created later inside ServerTestHost.InternalRunAsync).
        // Sending it through ServerTelemetry here would throw InvalidOperationException via
        // ServerTestHost.AssertInitialized.
        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.Server,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        // Register ServerTelemetry in the service provider so that all telemetry events raised at runtime
        // in server mode are forwarded to the client via JSON-RPC. This replaces the collector registered
        // earlier by TelemetryManager in SetupCommonServicesAsync. This is always registered regardless of
        // whether platform telemetry is enabled or disabled, because server-mode telemetry is a protocol
        // concern (JSON-RPC forwarding to the client), not a data-collection concern.
        context.ServiceProvider.ReplaceService<ITelemetryCollector>(new ServerTelemetry(serverTestHost));

        CompleteBuilderActivity(context.BuilderActivity, testControllerConnection is not null ? nameof(TestHostControlledHost) : nameof(ServerTestHost));
        return actualTestHost;
    }

    private async Task<IHost> BuildConsoleTestHostAsync(
        BuildContext context,
        NamedPipeClient? testControllerConnection,
        TestHostControllerCancellationListener? testHostControllerCancellationListener)
    {
        ActionResult<ITestExecutionFilterFactory> testExecutionFilterFactoryResult = await ((TestHostManager)TestHost).TryBuildTestExecutionFilterFactoryAsync(context.ServiceProvider).ConfigureAwait(false);
        if (testExecutionFilterFactoryResult.IsSuccess)
        {
            context.ServiceProvider.TryAddService(testExecutionFilterFactoryResult.Result);
        }

        ActionResult<ITestFrameworkInvoker> testAdapterInvokerBuilderResult = await ((TestHostManager)TestHost).TryBuildTestAdapterInvokerAsync(context.ServiceProvider).ConfigureAwait(false);
        if (testAdapterInvokerBuilderResult.IsSuccess)
        {
            context.ServiceProvider.TryAddService(testAdapterInvokerBuilderResult.Result);
        }

        ITelemetryInformation telemetryInformation = context.ServiceProvider.GetTelemetryInformation();
        if (telemetryInformation.IsEnabled)
        {
            context.BuilderMetrics[TelemetryProperties.HostProperties.IsHotReloadPropertyName] = context.ServiceProvider.GetRuntimeFeature().IsHotReloadEnabled.AsTelemetryBool();
        }

        AddInvocationLifetimeRunner(context, context.ServiceProvider, setOwnerMarker: false);

        ConsoleTestHost consoleHost = CreateConsoleTestHost(
            context.ServiceProvider,
            BuildTestFrameworkAsync,
            (TestFrameworkManager)TestFramework!,
            (TestHostManager)TestHost);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        IHost actualTestHost = consoleHost;
        if (testControllerConnection is not null)
        {
            var controlledHost = new TestHostControlledHost(
                testControllerConnection,
                consoleHost,
                context.TestApplicationCancellationTokenSource.CancellationToken,
                context.ServiceProvider.GetRequiredService<TestApplicationResult>());
            controlledHost.SetCoverageResult(context.ServiceProvider.GetRequiredService<TestCoverageResult>());
            controlledHost.SetCancellationListener(testHostControllerCancellationListener);
            actualTestHost = controlledHost;
        }
#pragma warning restore CA1416 // Preserve existing browser behavior while splitting the method.

#pragma warning disable SA1118 // Parameter should not span multiple lines
        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.Console,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
#pragma warning restore SA1118 // Parameter should not span multiple lines

        CompleteBuilderActivity(context.BuilderActivity, testControllerConnection is not null ? nameof(TestHostControlledHost) : nameof(ConsoleTestHost));
        return actualTestHost;
    }
}
