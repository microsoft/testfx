// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal abstract class CommonTestHost(ServiceProvider serviceProvider, NamedPipeClient? dotnetTestPipeClient = null, ITestApplicationModuleInfo? testApplicationModuleInfo = null) : ITestHost
{
    public ServiceProvider ServiceProvider { get; } = serviceProvider;

    protected abstract bool RunTestApplicationLifeCycleCallbacks { get; }

    public async Task<int> RunAsync()
    {
        CancellationToken testApplicationCancellationToken = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;

        int exitCode;
        try
        {
            bool isDotnetTestHandshakeSuccessful = default;
            if (dotnetTestPipeClient is not null)
            {
                isDotnetTestHandshakeSuccessful = await IsDotnetTestHandshakeSuccessfulAsync(dotnetTestPipeClient, testApplicationModuleInfo, testApplicationCancellationToken);
            }

            if (isDotnetTestHandshakeSuccessful)
            {
                if (RunTestApplicationLifeCycleCallbacks)
                {
                    // Get the test application lifecycle callbacks to be able to call the before run
                    foreach (ITestApplicationLifecycleCallbacks testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestApplicationLifecycleCallbacks>())
                    {
                        await testApplicationLifecycleCallbacks.BeforeRunAsync(testApplicationCancellationToken);
                    }
                }

                exitCode = await InternalRunAsync();

                if (RunTestApplicationLifeCycleCallbacks)
                {
                    foreach (ITestApplicationLifecycleCallbacks testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestApplicationLifecycleCallbacks>())
                    {
                        await testApplicationLifecycleCallbacks.AfterRunAsync(exitCode, testApplicationCancellationToken);
                        await DisposeHelper.DisposeAsync(testApplicationLifecycleCallbacks);
                    }
                }
            }
            else
            {
                exitCode = ExitCodes.IncompatibleProtocolVersion;
            }
        }
        catch (OperationCanceledException) when (testApplicationCancellationToken.IsCancellationRequested)
        {
            // We do nothing we're cancelling
            exitCode = ExitCodes.TestSessionAborted;
        }
        finally
        {
#if NETCOREAPP
            if (dotnetTestPipeClient is not null)
            {
                await dotnetTestPipeClient.DisposeAsync();
            }
#else
            dotnetTestPipeClient?.Dispose();
#endif

            await DisposeServiceProviderAsync(ServiceProvider, isProcessShutdown: true);
            await DisposeHelper.DisposeAsync(ServiceProvider.GetService<FileLoggerProvider>());
            await DisposeHelper.DisposeAsync(ServiceProvider.GetTestApplicationCancellationTokenSource());
        }

        return exitCode;
    }

    private async Task<bool> IsDotnetTestHandshakeSuccessfulAsync(NamedPipeClient dotnetTestPipeClient, ITestApplicationModuleInfo? testApplicationModuleInfo, CancellationToken testApplicationCancellationToken)
    {
        HandshakeInfo handshakeInfo = new(new Dictionary<string, string>()
        {
            { HandshakeInfoPropertyNames.PID, ServiceProvider.GetProcessHandler().GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) },
            { HandshakeInfoPropertyNames.Architecture, RuntimeInformation.OSArchitecture.ToString() },
            { HandshakeInfoPropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeInfoPropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeInfoPropertyNames.ProtocolVersion, ProtocolConstants.Version },
            { HandshakeInfoPropertyNames.HostType, this is ConsoleTestHost ? nameof(ConsoleTestHost) : nameof(TestHostControllersTestHost) },
            { HandshakeInfoPropertyNames.ModulePath, testApplicationModuleInfo?.GetCurrentTestApplicationFullPath() ?? string.Empty },
        });

        HandshakeInfo response = await dotnetTestPipeClient.RequestReplyAsync<HandshakeInfo, HandshakeInfo>(handshakeInfo, testApplicationCancellationToken);

        return response.Properties.TryGetValue(HandshakeInfoPropertyNames.ProtocolVersion, out string? protocolVersion) &&
            protocolVersion.Equals(ProtocolConstants.Version, StringComparison.Ordinal);
    }

    protected abstract Task<int> InternalRunAsync();

    protected static async Task ExecuteRequestAsync(IPlatformOutputDevice outputDevice, ITestSessionContext testSessionInfo,
        ServiceProvider serviceProvider, BaseMessageBus baseMessageBus, ITestFramework testFramework, ClientInfo client)
    {
        CancellationToken testSessionCancellationToken = serviceProvider.GetTestSessionContext().CancellationToken;

        await DisplayBeforeSessionStartAsync(outputDevice, testSessionInfo, testSessionCancellationToken);

        try
        {
            await NotifyTestSessionStartAsync(testSessionInfo.SessionId, baseMessageBus, serviceProvider, testSessionCancellationToken);
            await serviceProvider.GetTestAdapterInvoker().ExecuteAsync(testFramework, client, testSessionCancellationToken);
            await NotifyTestSessionEndAsync(testSessionInfo.SessionId, baseMessageBus, serviceProvider, testSessionCancellationToken);
        }
        catch (OperationCanceledException) when (testSessionCancellationToken.IsCancellationRequested)
        {
            // Do nothing we're cancelled
        }

        // We keep the display after session out of the OperationCanceledException catch because we want to notify the IPlatformOutputDevice
        // also in case of cancellation. Most likely it needs to notify users that the session was cancelled.
        await DisplayAfterSessionEndRunAsync(outputDevice, testSessionInfo, testSessionCancellationToken);
    }

    private static async Task DisplayBeforeSessionStartAsync(IPlatformOutputDevice outputDevice, ITestSessionContext sessionInfo, CancellationToken cancellationToken)
    {
        // Display before session start
        await outputDevice.DisplayBeforeSessionStartAsync();

        if (outputDevice is ITestSessionLifetimeHandler testSessionLifetimeHandler)
        {
            await testSessionLifetimeHandler.OnTestSessionStartingAsync(
                sessionInfo.SessionId,
                cancellationToken);
        }
    }

    private static async Task DisplayAfterSessionEndRunAsync(IPlatformOutputDevice outputDevice, ITestSessionContext sessionInfo, CancellationToken cancellationToken)
    {
        // Display after session end
        await outputDevice.DisplayAfterSessionEndRunAsync();

        // We want to ensure that the output service is the last one to run
        if (outputDevice is ITestSessionLifetimeHandler testSessionLifetimeHandlerFinishing)
        {
            await testSessionLifetimeHandlerFinishing.OnTestSessionFinishingAsync(
                sessionInfo.SessionId,
                cancellationToken);
        }
    }

    private static async Task NotifyTestSessionStartAsync(SessionUid sessionUid, BaseMessageBus baseMessageBus, ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        TestSessionLifetimeHandlersContainer? testSessionLifetimeHandlersContainer = serviceProvider.GetService<TestSessionLifetimeHandlersContainer>();
        if (testSessionLifetimeHandlersContainer is null)
        {
            return;
        }

        foreach (ITestSessionLifetimeHandler testSessionLifetimeHandler in testSessionLifetimeHandlersContainer.TestSessionLifetimeHandlers)
        {
            await testSessionLifetimeHandler.OnTestSessionStartingAsync(sessionUid, cancellationToken);
        }

        // Drain messages generated by the session start notification before to start test execution.
        await baseMessageBus.DrainDataAsync();
    }

    private static async Task NotifyTestSessionEndAsync(SessionUid sessionUid, BaseMessageBus baseMessageBus, ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Drain messages generated by the test session execution before to process the session end notification.
        await baseMessageBus.DrainDataAsync();

        TestSessionLifetimeHandlersContainer? testSessionLifetimeHandlersContainer = serviceProvider.GetService<TestSessionLifetimeHandlersContainer>();
        if (testSessionLifetimeHandlersContainer is null)
        {
            return;
        }

        foreach (ITestSessionLifetimeHandler testSessionLifetimeHandler in serviceProvider.GetRequiredService<TestSessionLifetimeHandlersContainer>().TestSessionLifetimeHandlers)
        {
            await testSessionLifetimeHandler.OnTestSessionFinishingAsync(sessionUid, cancellationToken);

            // OnTestSessionFinishingAsync could produce information that needs to be handled by others.
            await baseMessageBus.DrainDataAsync();
        }

        // We disable after the drain because it's possible that the drain will produce more messages
        await baseMessageBus.DrainDataAsync();
        await baseMessageBus.DisableAsync();
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
            if (!isProcessShutdown &&
                service is ITelemetryCollector or
                 ITestApplicationLifecycleCallbacks)
            {
                continue;
            }

            if (!alreadyDisposed.Contains(service))
            {
                await DisposeHelper.DisposeAsync(service);
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
                        await DisposeHelper.DisposeAsync(dataConsumer);
                        alreadyDisposed.Add(service);
                    }
                }
            }
        }
    }
}
