// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Hosts;

/// <summary>
/// This represents either a test host (console or server), or a test host controller.
/// This doesn't represent an orchestrator host.
/// </summary>
[StackTraceHidden]
internal abstract class CommonHost(ServiceProvider serviceProvider) : IHost
{
    public ServiceProvider ServiceProvider => serviceProvider;

    protected IPushOnlyProtocol? PushOnlyProtocol => ServiceProvider.GetService<IPushOnlyProtocol>();

    protected abstract bool RunTestApplicationLifeCycleCallbacks { get; }

    public async Task<int> RunAsync()
    {
        CancellationToken testApplicationCancellationToken = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        List<object> alreadyDisposed = [];

        int exitCode = (int)ExitCode.GenericFailure;
        IPlatformOpenTelemetryService? platformOTelService = null;
        IPlatformActivity? activity = null;
        try
        {
            platformOTelService = ServiceProvider.GetPlatformOTelService();
            string hostType = GetHostType();
            activity = platformOTelService?.StartActivity(hostType);

            if (PushOnlyProtocol is null || PushOnlyProtocol?.IsServerMode == false)
            {
                exitCode = await RunTestAppAsync(platformOTelService, testApplicationCancellationToken, alreadyDisposed).ConfigureAwait(false);

                if (testApplicationCancellationToken.IsCancellationRequested)
                {
                    exitCode = (int)ExitCode.TestSessionAborted;
                }

                return exitCode;
            }

            try
            {
                RoslynDebug.Assert(PushOnlyProtocol is not null);

                IReadOnlyDictionary<byte, string>? additionalHandshakeProperties =
                    SupportsArtifactPostProcessing(hostType)
                        ? ArtifactPostProcessingHandshakeProperties.Create(ServiceProvider.GetServicesInternal<IArtifactPostProcessor>())
                        : null;
                bool isValidProtocol = await PushOnlyProtocol.IsCompatibleProtocolAsync(hostType, additionalHandshakeProperties).ConfigureAwait(false);

                if (isValidProtocol && PushOnlyProtocol.IsServerControlChannelSupported)
                {
                    // Start listening for server-initiated signals (e.g. session cancellation) before running tests
                    // so a signal that arrives mid-run is observed. React by stopping gracefully where possible.
                    await PushOnlyProtocol.StartServerControlChannelAsync(RequestGracefulSessionStopAsync).ConfigureAwait(false);
                }

                exitCode = isValidProtocol
                    ? await RunTestAppAsync(platformOTelService, testApplicationCancellationToken, alreadyDisposed).ConfigureAwait(false)
                    : (int)ExitCode.IncompatibleProtocolVersion;
            }
            finally
            {
                if (PushOnlyProtocol is not null)
                {
                    await PushOnlyProtocol.OnExitAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (testApplicationCancellationToken.IsCancellationRequested)
        {
            // We do nothing we're canceling
        }
        finally
        {
            // Dispose the activity
            activity?.Dispose();

            await DisposeServiceProviderAsync(ServiceProvider, alreadyDisposed: alreadyDisposed, isProcessShutdown: true).ConfigureAwait(false);
            await DisposeHelper.DisposeAsync(ServiceProvider.GetService<FileLoggerProvider>()).ConfigureAwait(false);

            // Dispose the LoggerFactoryProxy last so that all user-registered logger providers
            // (e.g., Microsoft.Extensions.Logging providers added via the Microsoft.Testing.Extensions.Logging
            // bridge such as Serilog, Application Insights, OpenTelemetry) get a chance to flush their buffers.
            // The proxy is skipped by DisposeServiceProviderAsync for ordering reasons.
            await DisposeHelper.DisposeAsync(ServiceProvider.GetService<LoggerFactoryProxy>()).ConfigureAwait(false);

            if (PushOnlyProtocol is not null && !alreadyDisposed.Contains(PushOnlyProtocol))
            {
                await DisposeHelper.DisposeAsync(PushOnlyProtocol).ConfigureAwait(false);
            }

            // This is intentional that we are not disposing the CTS.
            // An unobserved task exception could be raised after the dispose, and we want to use OutputDevice there
            // which needs CTS down the path.
            // await DisposeHelper.DisposeAsync(ServiceProvider.GetTestApplicationCancellationTokenSource());
        }

        if (testApplicationCancellationToken.IsCancellationRequested)
        {
            exitCode = (int)ExitCode.TestSessionAborted;
        }

        return exitCode;
    }

    internal static bool SupportsArtifactPostProcessing(string hostType)
        => hostType is HandshakeMessageHostTypes.TestHost
            or HandshakeMessageHostTypes.ServerTestHost
            or HandshakeMessageHostTypes.TestHostController;

    protected virtual string HostType
        => this switch
        {
            ConsoleTestHost => HandshakeMessageHostTypes.TestHost,
            TestHostControllersTestHost => HandshakeMessageHostTypes.TestHostController,
            ServerTestHost => HandshakeMessageHostTypes.ServerTestHost,
            _ => throw new InvalidOperationException($"Unknown host type '{GetType().FullName}'"),
        };

    private string GetHostType()
    {
        // TestHostOrchestratorHost does not inherit from CommonHost, so the orchestrator handshake is
        // performed there directly (see TestHostOrchestratorHost.RunAsync) rather than going through
        // this path. This method only covers the test host and test host controller roles.
        string hostType = HostType;
        return hostType;
    }

    // Reaction to a server-initiated session cancellation coming over the reverse control pipe. Prefer a graceful
    // stop so the framework stops scheduling new tests but still emits trx/logs/artifacts for whatever completed
    // (mirroring the local '--maximum-failed-tests' behavior). Fall back to hard cancellation when the running
    // framework has no graceful-stop capability (e.g. the test host controller), which is the only lever left.
    private async Task RequestGracefulSessionStopAsync(CancellationToken cancellationToken)
    {
        IGracefulStopTestExecutionCapability? capability =
            ServiceProvider.GetService<ITestFrameworkCapabilities>()?.GetCapability<IGracefulStopTestExecutionCapability>();

        if (capability is not null)
        {
            await capability.StopTestExecutionAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            ServiceProvider.GetTestApplicationCancellationTokenSource().Cancel();
        }
    }

    private async Task<int> RunTestAppAsync(IPlatformOpenTelemetryService? platformOTelService, CancellationToken testApplicationCancellationToken, List<object> alreadyDisposed)
    {
        if (RunTestApplicationLifeCycleCallbacks)
        {
            using (platformOTelService?.StartActivity("BeforeRunCallbacks"))
            {
                // Get the test application lifecycle callbacks to be able to call the before run
                foreach (ITestHostApplicationLifetime testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestHostApplicationLifetime>())
                {
                    using IPlatformActivity? activity = platformOTelService?.StartActivity(testApplicationLifecycleCallbacks.Uid, testApplicationLifecycleCallbacks.ToOTelTags());
                    await testApplicationLifecycleCallbacks.BeforeRunAsync(testApplicationCancellationToken).ConfigureAwait(false);
                }
            }
        }

        int exitCode;
        using (platformOTelService?.StartActivity("Run"))
        {
            exitCode = await InternalRunAsync(testApplicationCancellationToken).ConfigureAwait(false);
        }

        if (RunTestApplicationLifeCycleCallbacks)
        {
            using (platformOTelService?.StartActivity("AfterRunCallbacks"))
            {
                foreach (ITestHostApplicationLifetime testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestHostApplicationLifetime>())
                {
                    using IPlatformActivity? activity = platformOTelService?.StartActivity(testApplicationLifecycleCallbacks.Uid, testApplicationLifecycleCallbacks.ToOTelTags());
                    await testApplicationLifecycleCallbacks.AfterRunAsync(exitCode, testApplicationCancellationToken).ConfigureAwait(false);
                    await DisposeHelper.DisposeAsync(testApplicationLifecycleCallbacks).ConfigureAwait(false);
                    alreadyDisposed.Add(testApplicationLifecycleCallbacks);
                }
            }
        }

        return exitCode;
    }

    protected abstract Task<int> InternalRunAsync(CancellationToken cancellationToken);

    protected static async Task ExecuteRequestAsync(ProxyOutputDevice outputDevice, ITestSessionContext testSessionInfo,
        ServiceProvider serviceProvider, BaseMessageBus baseMessageBus, ITestFramework testFramework, TestHost.ClientInfo client)
    {
        // Reset the shared, application-scoped coverage accumulator at the start of every request here, in the
        // common host/request lifecycle, so it happens for all output modes (terminal, pipe, server, custom)
        // rather than only when the terminal device renders. Without this a prior session's coverage rows and
        // thresholds would be reprinted and its threshold-failure verdict could poison a later session.
        serviceProvider.GetRequiredService<TestCoverageResult>().Reset();

        await DisplayBeforeSessionStartAsync(outputDevice, testSessionInfo).ConfigureAwait(false);
        CancellationToken cancellationToken = testSessionInfo.CancellationToken;
        try
        {
            IPlatformOpenTelemetryService? otelService = serviceProvider.GetPlatformOTelService();
            using (otelService?.StartActivity("OnTestSessionStarting"))
            {
                await NotifyTestSessionStartAsync(testSessionInfo, baseMessageBus, serviceProvider, otelService).ConfigureAwait(false);
            }

            using (otelService?.StartActivity("TestFrameworkInvoker"))
            {
                await serviceProvider.GetTestFrameworkInvoker().ExecuteAsync(testFramework, client, cancellationToken).ConfigureAwait(false);
            }

            using (otelService?.StartActivity("OnTestSessionEnding"))
            {
                await NotifyTestSessionEndAsync(testSessionInfo, baseMessageBus, serviceProvider, otelService).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Do nothing we're canceled
        }

        // We keep the display after session out of the OperationCanceledException catch because we want to notify the IPlatformOutputDevice
        // also in case of cancellation. Most likely it needs to notify users that the session was canceled.
        await DisplayAfterSessionEndRunAsync(outputDevice, testSessionInfo).ConfigureAwait(false);
    }

    private static async Task DisplayBeforeSessionStartAsync(ProxyOutputDevice outputDevice, ITestSessionContext sessionInfo)
    {
        // Display before session start
        await outputDevice.DisplayBeforeSessionStartAsync(sessionInfo.CancellationToken).ConfigureAwait(false);

        if (outputDevice.OriginalOutputDevice is ITestSessionLifetimeHandler testSessionLifetimeHandler)
        {
            await testSessionLifetimeHandler.OnTestSessionStartingAsync(sessionInfo).ConfigureAwait(false);
        }
    }

    private static async Task DisplayAfterSessionEndRunAsync(ProxyOutputDevice outputDevice, ITestSessionContext sessionInfo)
    {
        // Display after session end even when the session cancellation token is canceled.
        // We intentionally pass a non-cancelable token so final output/cleanup notifications are not skipped.
        await outputDevice.DisplayAfterSessionEndRunAsync(CancellationToken.None).ConfigureAwait(false);

        // We want to ensure that the output service is the last one to run
        if (outputDevice.OriginalOutputDevice is ITestSessionLifetimeHandler testSessionLifetimeHandlerFinishing)
        {
            await testSessionLifetimeHandlerFinishing.OnTestSessionFinishingAsync(sessionInfo).ConfigureAwait(false);
        }
    }

    private static async Task NotifyTestSessionStartAsync(ITestSessionContext testSessionContext, BaseMessageBus baseMessageBus, ServiceProvider serviceProvider, IPlatformOpenTelemetryService? otelService)
    {
        TestSessionLifetimeHandlersContainer? testSessionLifetimeHandlersContainer = serviceProvider.GetService<TestSessionLifetimeHandlersContainer>();
        if (testSessionLifetimeHandlersContainer is null)
        {
            return;
        }

        foreach (ITestSessionLifetimeHandler testSessionLifetimeHandler in testSessionLifetimeHandlersContainer.TestSessionLifetimeHandlers)
        {
            using IPlatformActivity? activity = otelService?.StartActivity(testSessionLifetimeHandler.Uid, testSessionLifetimeHandler.ToOTelTags());
            await testSessionLifetimeHandler.OnTestSessionStartingAsync(testSessionContext).ConfigureAwait(false);
        }

        // Drain messages generated by the session start notification before to start test execution.
        await baseMessageBus.DrainDataAsync().ConfigureAwait(false);
    }

    private static async Task NotifyTestSessionEndAsync(ITestSessionContext testSessionContext, BaseMessageBus baseMessageBus, ServiceProvider serviceProvider, IPlatformOpenTelemetryService? otelService)
    {
        // Drain messages generated by the test session execution before to process the session end notification.
        await baseMessageBus.DrainDataAsync().ConfigureAwait(false);

        TestSessionLifetimeHandlersContainer? testSessionLifetimeHandlersContainer = serviceProvider.GetService<TestSessionLifetimeHandlersContainer>();
        if (testSessionLifetimeHandlersContainer is null)
        {
            // TODO: Is this reachable? If so, are we missing await baseMessageBus.DisableAsync() here? Tracked by https://github.com/microsoft/testfx/issues/8086.
            return;
        }

        IShutdownProgressReporter? shutdownProgressReporter = serviceProvider.GetService<IShutdownProgressReporter>();

        // First, we call OnTestSessionFinishingAsync on all non-consumers.
        bool hasNonDataConsumers = false;
        foreach (ITestSessionLifetimeHandler testSessionLifetimeHandler in testSessionLifetimeHandlersContainer.TestSessionLifetimeHandlers)
        {
            if (testSessionLifetimeHandler is IDataConsumer)
            {
                // At first, we don't call this for data consumers.
                // We want to do this for potentially publishers-only handlers.
                // The order here matters, because one handler can produce a message that is consumed by another.
                // By making the consumers last, we reduce the likelihood of issues related to ordering.
                // One case where this is important is the dependency between Code Coverage's ITestSessionLifetimeHandler and TRX's ITestSessionLifetimeHandler.
                // We must run Code Coverage ITestSessionLifetimeHandler implementation first, as it will publish SessionFileArtifact.
                // The SessionFileArtifact is expected to be consumed by TRX's implementation of ITestSessionLifetimeHandler.
                // In that case, ITestSessionLifetimeHandler of CC is a producer-only handler which we will run first.
                // Then we will drain the message bus to ensure TRX handler have consumed the SessionFileArtifact.
                // Then we run TRX OnTestSessionFinishingAsync.
                continue;
            }

            hasNonDataConsumers = true;

            using (otelService?.StartActivity(testSessionLifetimeHandler.Uid, testSessionLifetimeHandler.ToOTelTags()))
            using (shutdownProgressReporter?.Track(testSessionLifetimeHandler.Uid, testSessionLifetimeHandler.DisplayName, nameof(ITestSessionLifetimeHandler.OnTestSessionFinishingAsync)))
            {
                await testSessionLifetimeHandler.OnTestSessionFinishingAsync(testSessionContext).ConfigureAwait(false);
            }
        }

        if (hasNonDataConsumers)
        {
            // At this point, we called all non-consumer handlers.
            // Now, we want to make sure to drain the message bus before calling the consumer handlers.
            // Messages produced by non-consumer handlers could be consumed by consumer handlers.
            await baseMessageBus.DrainDataAsync().ConfigureAwait(false);
        }

        foreach (ITestSessionLifetimeHandler testSessionLifetimeHandler in testSessionLifetimeHandlersContainer.TestSessionLifetimeHandlers)
        {
            if (testSessionLifetimeHandler is not IDataConsumer)
            {
                // We already called this for non-consumers.
                continue;
            }

            using (otelService?.StartActivity(testSessionLifetimeHandler.Uid, testSessionLifetimeHandler.ToOTelTags()))
            using (shutdownProgressReporter?.Track(testSessionLifetimeHandler.Uid, testSessionLifetimeHandler.DisplayName, nameof(ITestSessionLifetimeHandler.OnTestSessionFinishingAsync)))
            {
                await testSessionLifetimeHandler.OnTestSessionFinishingAsync(testSessionContext).ConfigureAwait(false);
            }

            // OnTestSessionFinishingAsync could produce information that needs to be handled by others.
            // While in many cases we already handled this by calling all non-consumers first, it's possible that
            // two consumers might depend on each other. In that case, we solely rely on registration order.
            // And we drain in between.
            await baseMessageBus.DrainDataAsync().ConfigureAwait(false);
        }

        // We disable after the drain because it's possible that the drain will produce more messages
        await baseMessageBus.DrainDataAsync().ConfigureAwait(false);
        await baseMessageBus.DisableAsync().ConfigureAwait(false);
    }

    protected static async Task DisposeServiceProviderAsync(ServiceProvider serviceProvider, Func<object, bool>? filter = null, List<object>? alreadyDisposed = null, bool isProcessShutdown = false)
    {
        alreadyDisposed ??= [];
        foreach (object service in serviceProvider.Services)
        {
            // Logger is the most special service and we dispose it manually as last one, we want to be able to
            // collect logs till the end of the process.
            if (service is FileLoggerProvider)
            {
                continue;
            }

            // The LoggerFactoryProxy owns the real ILoggerFactory and is disposed manually after the rest of
            // the services so that providers registered through it (including bridges to
            // Microsoft.Extensions.Logging) can flush at the very end of the process.
            if (service is LoggerFactoryProxy)
            {
                continue;
            }

            // The ITestApplicationCancellationTokenSource contains the cancellation token and can be used by other services during the shutdown
            // we will collect manually in the correct moment.
            if (service is ITestApplicationCancellationTokenSource)
            {
                continue;
            }

            if (filter is not null && !filter(service))
            {
                continue;
            }

            // We need to ensure that we won't dispose special services till the shutdown
#pragma warning disable CS0618 // Type or member is obsolete
            if (!isProcessShutdown &&
                service is ITelemetryCollector or
                 ITestHostApplicationLifetime or
                 IPushOnlyProtocol or
                 IPlatformOpenTelemetryService or
                 IOpenTelemetryProvider)
            {
                continue;
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (!alreadyDisposed.Contains(service))
            {
                await DisposeHelper.DisposeAsync(service).ConfigureAwait(false);
                alreadyDisposed.Add(service);
            }

            if (service is BaseMessageBus messageBus)
            {
                foreach (IDataConsumer dataConsumer in messageBus.DataConsumerServices)
                {
                    if (filter is not null && !filter(dataConsumer))
                    {
                        continue;
                    }

                    if (!alreadyDisposed.Contains(dataConsumer))
                    {
                        await DisposeHelper.DisposeAsync(dataConsumer).ConfigureAwait(false);
                        alreadyDisposed.Add(dataConsumer);
                    }
                }
            }
        }
    }
}
