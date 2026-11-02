// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    private async Task HandleNotificationAsync(NotificationMessage message, CancellationToken serverClosing)
    {
        // We need to guarantee that all notification received before the "exit" are handled.
        // We check it before to "enqueue" the task that handle it
        if (serverClosing.IsCancellationRequested)
        {
            try
            {
                // We're closing we don't handle the "new notification"
                return;
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }

        if (Volatile.Read(ref _initializeState) != Initialized
            && message.Method != JsonRpcMethods.CancelRequest)
        {
            _requestCounter.Signal();
            return;
        }

        try
        {
            switch (message.Method, message.Params)
            {
                case (JsonRpcMethods.CancelRequest, CancelRequestArgs args):
                    if (_clientToServerRequests.TryGetValue(
                        GetRequestKey(args.CancelRequestId, args.StringId),
                        out RpcInvocationState? rpcState))
                    {
                        if (!rpcState.TryRequestCancellation())
                        {
                            break;
                        }

                        // Record cancellation synchronously so a queued request cannot resume into execution.
                        // Run token callbacks asynchronously so extension code cannot block the message reader.
                        Exception? cancellationException = await Task.Run(rpcState.CancelRequest).ConfigureAwait(false);
                        if (cancellationException is not null)
                        {
                            // This is intentionally not using PlatformResources.ExceptionDuringCancellationWarningMessage
                            // It's meant for troubleshooting and shouldn't be localized.
                            // The localized message that is user-facing will be displayed in the DisplayAsync call next line.
                            QueueLog(LogLevel.Warning, $"Exception during the cancellation of request id '{args.CancelRequestId}': {cancellationException}");

                            await ServiceProvider.GetOutputDevice().DisplayAsync(
                                this,
                                new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExceptionDuringCancellationWarningMessage, args.CancelRequestId)), serverClosing).ConfigureAwait(false);
                        }
                    }

                    break;
            }
        }
        finally
        {
            // Signal the notification
            _requestCounter.Signal();
        }
    }
}
