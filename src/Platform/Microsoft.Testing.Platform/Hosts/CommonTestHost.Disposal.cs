// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Hosts;

internal abstract partial class CommonHost
{
    protected static Task DisposeServiceProviderAsync(
        ServiceProvider serviceProvider,
        Func<object, bool>? filter = null,
        List<object>? alreadyDisposed = null,
        bool isProcessShutdown = false)
        => DisposeServiceProviderCoreAsync(serviceProvider, filter, alreadyDisposed, isProcessShutdown, disposeServiceAsync: null);

    private static async Task DisposeServiceProviderCoreAsync(
        ServiceProvider serviceProvider,
        Func<object, bool>? filter,
        List<object>? alreadyDisposed,
        bool isProcessShutdown,
        Func<object, Task>? disposeServiceAsync)
    {
        alreadyDisposed ??= [];
        disposeServiceAsync ??= DisposeHelper.DisposeAsync;

        // Close the message bus handshake before disposing anything at all. Consumers are routinely registered
        // as services in their own right, and several of them (the output device, the coverage accumulator) land
        // in Services *before* the bus does, so disabling inline when the loop happens to reach the bus would
        // already have disposed those consumers while their ConsumeAsync was still running.
        foreach (object service in serviceProvider.Services)
        {
            if (service is not BaseMessageBus messageBus
                || alreadyDisposed.Contains(messageBus)
                || (filter is not null && !filter(messageBus)))
            {
                continue;
            }

            await EnsureMessageBusDisabledAsync(messageBus, serviceProvider).ConfigureAwait(false);

            // Disabling is bounded on an aborted run, so it can return with a consumer still inside
            // ConsumeAsync. Recording those as already disposed is what keeps every loop below off them:
            // disposing one now is precisely the race the handshake exists to prevent, so we leak it and let
            // the process exit reclaim it instead.
            foreach (IDataConsumer dataConsumer in messageBus.ConsumersStillRunning)
            {
                if (alreadyDisposed.Contains(dataConsumer))
                {
                    continue;
                }

                alreadyDisposed.Add(dataConsumer);
                await LogShutdownWarningAsync(
                    serviceProvider,
                    $"Not disposing data consumer '{dataConsumer.Uid}' because it is still consuming after the shutdown timeout elapsed.").ConfigureAwait(false);
            }
        }

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
                await disposeServiceAsync(service).ConfigureAwait(false);
                alreadyDisposed.Add(service);
            }

            if (service is BaseMessageBus busWithConsumers)
            {
                foreach (IDataConsumer dataConsumer in busWithConsumers.DataConsumerServices)
                {
                    if (filter is not null && !filter(dataConsumer))
                    {
                        continue;
                    }

                    // Consumers still inside ConsumeAsync were recorded as already disposed by the pre-pass
                    // above, so this check is what spares them here.
                    if (!alreadyDisposed.Contains(dataConsumer))
                    {
                        await disposeServiceAsync(dataConsumer).ConfigureAwait(false);
                        alreadyDisposed.Add(dataConsumer);
                    }
                }
            }
        }
    }
}
