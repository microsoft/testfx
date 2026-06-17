// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHost;
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
    private async Task<IHost> BuildHostAsync(BuildContext context)
    {
        if (context.EarlyHost is not null)
        {
            return context.EarlyHost;
        }

        IReadOnlyList<ITool> toolsInformation = await BuildToolsInformationAsync(context).ConfigureAwait(false);
        if (context.LoggingState.CommandLineParseResult.HasTool)
        {
            return await BuildToolsHostAsync(context, toolsInformation).ConfigureAwait(false);
        }

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        DotnetTestConnection pushOnlyProtocol = new(context.CommandLineHandler, _environment, _testApplicationModuleInfo, context.TestApplicationCancellationTokenSource);
        await pushOnlyProtocol.AfterCommonServiceSetupAsync().ConfigureAwait(false);
        if (pushOnlyProtocol.IsServerMode)
        {
            context.ServiceProvider.AddService(pushOnlyProtocol);
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
                bool isProtocolCompatible = await pushOnlyProtocol.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost).ConfigureAwait(false);
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

        return await TryBuildTestHostOrchestratorHostAsync(context).ConfigureAwait(false) is { } testHostOrchestratorHost
            ? testHostOrchestratorHost
            : await TryBuildTestHostControllersHostAsync(context).ConfigureAwait(false) is { } testHostControllerHost
            ? testHostControllerHost
            : await BuildTestHostModeAsync(context).ConfigureAwait(false);
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

    private async Task<IHost?> TryBuildTestHostOrchestratorHostAsync(BuildContext context)
    {
        TestHostOrchestratorConfiguration testHostOrchestratorConfiguration = await _testHostOrchestratorManager.BuildAsync(context.ServiceProvider).ConfigureAwait(false);
        if (testHostOrchestratorConfiguration.TestHostOrchestrators.Length == 0
            || context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return null;
        }

        context.PoliciesService.ProcessRole = TestProcessRole.TestHostOrchestrator;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostOrchestrator, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        ITestHostOrchestratorApplicationLifetime[] orchestratorLifetimes =
            await _testHostOrchestratorManager.BuildTestHostOrchestratorApplicationLifetimesAsync(context.ServiceProvider).ConfigureAwait(false);
        context.ServiceProvider.AddServices(orchestratorLifetimes);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostOrchestratorHost));
        return new TestHostOrchestratorHost(testHostOrchestratorConfiguration, context.ServiceProvider);
    }

    private async Task<IHost?> TryBuildTestHostControllersHostAsync(BuildContext context)
    {
        if ((context.TestHostControllerInfo.HasTestHostController
                && context.SystemEnvironment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{context.TestHostControllerInfo.GetTestHostControllerPID()}") == "1")
            || context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return null;
        }

        PassiveNode? passiveNode = null;
        if (context.HasServerFlag && context.IsJsonRpcProtocol)
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

        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        context.TestHostControllerInfo.IsCurrentProcessTestHostController = true;
        context.PoliciesService.ProcessRole = TestProcessRole.TestHostController;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostController, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        TestHostControllersTestHost testHostControllersHost = new(
            testHostControllers,
            testHostControllersServiceProvider,
            passiveNode,
            context.SystemEnvironment,
            context.LoggerFactory,
            context.SystemClock);

        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.TestHostControllers,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostControllersTestHost));
        return testHostControllersHost;
    }

    private async Task<IHost> BuildTestHostModeAsync(BuildContext context)
    {
        context.PoliciesService.ProcessRole = TestProcessRole.TestHost;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHost, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        string? currentWorkingDirectory = context.Configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ApplicationStateGuard.Ensure(currentWorkingDirectory is not null);

        context.TestHostControllerInfo.IsCurrentProcessTestHostController = false;

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        NamedPipeClient? testControllerConnection = await ConnectToTestHostProcessMonitorIfAvailableAsync(
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

        return context.HasServerFlag && context.IsJsonRpcProtocol
            ? await BuildServerTestHostAsync(context, testControllerConnection).ConfigureAwait(false)
            : await BuildConsoleTestHostAsync(context, testControllerConnection).ConfigureAwait(false);
    }

    private async Task<IHost> BuildServerTestHostAsync(BuildContext context, NamedPipeClient? testControllerConnection)
    {
        IMessageHandlerFactory messageHandlerFactory = ServerModeManager.Build(context.ServiceProvider);
        ServerTestHost serverTestHost = new(
            context.ServiceProvider,
            BuildTestFrameworkAsync,
            messageHandlerFactory,
            (TestFrameworkManager)TestFramework!,
            (TestHostManager)TestHost);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        IHost actualTestHost = testControllerConnection is not null
            ? new TestHostControlledHost(testControllerConnection, serverTestHost, context.TestApplicationCancellationTokenSource.CancellationToken)
            : serverTestHost;
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

    private async Task<IHost> BuildConsoleTestHostAsync(BuildContext context, NamedPipeClient? testControllerConnection)
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

        ConsoleTestHost consoleHost = CreateConsoleTestHost(
            context.ServiceProvider,
            BuildTestFrameworkAsync,
            (TestFrameworkManager)TestFramework!,
            (TestHostManager)TestHost);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        IHost actualTestHost = testControllerConnection is not null
            ? new TestHostControlledHost(testControllerConnection, consoleHost, context.TestApplicationCancellationTokenSource.CancellationToken)
            : consoleHost;
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
