// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class HangDumpProcessLifetimeHandler
{
    public void Dispose()
    {
        StopTimersAndClaimDumpGate(out Task? activityIndicatorTask);

        if (activityIndicatorTask is not null)
        {
            bool waitResult;
            try
            {
                waitResult = activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
            }
            catch (Exception e)
            {
                _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).GetAwaiter().GetResult();
                throw;
            }

            if (!waitResult)
            {
                throw new InvalidOperationException($"_activityIndicatorTask didn't exit in {TimeoutHelper.DefaultHangTimeSpanTimeout} seconds");
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        StopTimersAndClaimDumpGate(out Task? activityIndicatorTask);

        if (activityIndicatorTask is not null)
        {
            try
            {
                await activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }
#endif

    // Stops the deadline and inactivity timers, claims the dump gate so no timer callback can start
    // a new dump while we tear down the pipes, and returns any dump already in flight so the caller
    // can wait for it. The happy path disposes the timers in OnTestHostProcessExitedAsync, but that
    // runs only on a clean exit; Ctrl+C or an exception skips it, so dispose here too (Timer.Dispose
    // is idempotent, so disposing twice is safe).
    private void StopTimersAndClaimDumpGate(out Task? activityIndicatorTask)
    {
        _deadlineTimer?.Dispose();
        _activityTimer?.Dispose();

        lock (_dumpLock)
        {
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }
    }
}
