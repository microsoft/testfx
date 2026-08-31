// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Hosts;

internal abstract partial class CommonHost
{
    protected static async Task ExecuteRequestAsync(ProxyOutputDevice outputDevice, ITestSessionContext testSessionInfo,
        ServiceProvider serviceProvider, BaseMessageBus baseMessageBus, ITestFramework testFramework, TestHost.ClientInfo client,
        bool isDiscoveryRequest)
    {
        // Reset the shared, application-scoped coverage accumulator at the start of every request here, in the
        // common host/request lifecycle, so it happens for all output modes (terminal, pipe, server, custom)
        // rather than only when the terminal device renders. Without this a prior session's coverage rows and
        // thresholds would be reprinted and its threshold-failure verdict could poison a later session.
        serviceProvider.GetRequiredService<TestCoverageResult>().Reset();

        CancellationToken cancellationToken = testSessionInfo.CancellationToken;
        bool executionCompletedNotified = false;

        async Task NotifyTestExecutionCompletedAsync()
        {
            if (executionCompletedNotified)
            {
                return;
            }

            AbortAtDeadlineExtension? abortAtDeadlineExtension = serviceProvider.GetService<AbortAtDeadlineExtension>();
            abortAtDeadlineExtension?.NotifyTestExecutionCompleted();
            if (!isDiscoveryRequest)
            {
                serviceProvider.GetRequiredService<IStopPoliciesService>().NotifyTestExecutionCompleted();
            }

            executionCompletedNotified = true;
            if (abortAtDeadlineExtension is not null)
            {
                // A successful stop can make the invoker return before the deadline handler records
                // its verdict, while an asynchronously rejected stop must release its claim. Resolve
                // either outcome before reporters and exit-code consumers inspect the run.
                await abortAtDeadlineExtension.WaitForDeadlineHandlingAsync().ConfigureAwait(false);
            }
        }

        try
        {
            await DisplayBeforeSessionStartAsync(outputDevice, testSessionInfo).ConfigureAwait(false);

            try
            {
                IPlatformOpenTelemetryService? otelService = serviceProvider.GetPlatformOTelService();
                using (otelService?.StartActivity("OnTestSessionStarting"))
                {
                    await NotifyTestSessionStartAsync(testSessionInfo, baseMessageBus, serviceProvider, otelService).ConfigureAwait(false);
                }

                using (otelService?.StartActivity("TestFrameworkInvoker"))
                {
                    try
                    {
                        await serviceProvider.GetTestFrameworkInvoker().ExecuteAsync(testFramework, client, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Test execution is over -- normally, or because it failed or was canceled. Disarm the
                        // deadline here, before end-of-session draining and reporting begin, so a deadline
                        // reached while the reporters finalize an already-executed run cannot mark it as
                        // truncated. The extension cannot do this from ITestSessionLifetimeHandler: it is an
                        // IDataConsumer, and consumer handlers run last in NotifyTestSessionEndAsync, after the
                        // drains and after the reporters. Absent for discovery requests, where the extension is
                        // not registered.
                        await NotifyTestExecutionCompletedAsync().ConfigureAwait(false);
                    }
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
        finally
        {
            // Session startup can fail before the invoker is entered. Complete the run registration on that
            // path too, otherwise one failed server request leaves the application-scoped active count stuck.
            await NotifyTestExecutionCompletedAsync().ConfigureAwait(false);

            // The message bus shutdown handshake must complete before the services - and with them every
            // IDataConsumer - get disposed, otherwise a consumer can still be inside ConsumeAsync while it is
            // being disposed. NotifyTestSessionEndAsync does it on the happy path, but it is skipped whenever the
            // session is canceled or fails, so we close the handshake here for every outcome.
            await EnsureMessageBusDisabledAsync(baseMessageBus, serviceProvider).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disables the message bus, which stops accepting new payloads and awaits every consumer loop, so that no
    /// <see cref="Extensions.IDataConsumer.ConsumeAsync"/> can still be running once the consumers are disposed.
    /// </summary>
    /// <remarks>
    /// This is a best-effort safety net that runs on teardown paths, including the ones that are already unwinding
    /// because of a cancellation or a failure. Disabling is idempotent, so on the regular path this is a no-op and
    /// any consumer failure has already been surfaced by the real call. Failures here are logged and swallowed
    /// rather than replacing the exception that is being propagated; a consumer that survives the handshake is
    /// reported through <see cref="BaseMessageBus.ConsumersStillRunning"/> and left undisposed.
    /// </remarks>
    protected static async Task EnsureMessageBusDisabledAsync(BaseMessageBus baseMessageBus, IServiceProvider serviceProvider)
    {
        try
        {
            await baseMessageBus.DisableAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogShutdownWarningAsync(serviceProvider, $"Failed to disable the message bus during shutdown: {ex}").ConfigureAwait(false);
        }
    }
}
