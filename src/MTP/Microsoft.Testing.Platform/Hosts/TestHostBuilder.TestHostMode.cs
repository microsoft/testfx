// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
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
