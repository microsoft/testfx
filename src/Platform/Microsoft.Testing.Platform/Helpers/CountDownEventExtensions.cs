// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class CountDownEventExtensions
{
    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync(uint.MaxValue, cancellationToken).ConfigureAwait(false);

    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, TimeSpan timeout, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync((uint)timeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

    internal static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, uint millisecondsTimeOutInterval, CancellationToken cancellationToken)
        => OperatingSystem.IsBrowser()
            ? await countdownEvent.WaitBrowserAsync(millisecondsTimeOutInterval, cancellationToken).ConfigureAwait(false)
            : await countdownEvent.WaitNonBrowserAsync(millisecondsTimeOutInterval, cancellationToken).ConfigureAwait(false);

    private static async Task<bool> WaitBrowserAsync(this CountdownEvent countdownEvent, uint millisecondsTimeOutInterval, CancellationToken cancellationToken)
    {
        const int pollIntervalMs = 10;
        uint elapsedMs = 0;
        bool hasTimeout = millisecondsTimeOutInterval != uint.MaxValue;

        while (countdownEvent.CurrentCount > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hasTimeout && elapsedMs >= millisecondsTimeOutInterval)
            {
                return false;
            }

            await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
            elapsedMs += pollIntervalMs;
        }

        return true;
    }

    [UnsupportedOSPlatform("browser")]
    private static async Task<bool> WaitNonBrowserAsync(this CountdownEvent countdownEvent, uint millisecondsTimeOutInterval, CancellationToken cancellationToken)
    {
        RegisteredWaitHandle? registeredHandle = null;
        CancellationTokenRegistration tokenRegistration = default;
        try
        {
            TaskCompletionSource<bool> tcs = new();

            // https://learn.microsoft.com/dotnet/api/system.threading.threadpool.registerwaitforsingleobject
            // timedOut: true if the WaitHandle timed out; false if it was signaled.
            // executeOnlyOnce set to true to indicate that the thread will no longer wait on the waitObject
            // parameter after the delegate has been called;
            // false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.
            registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                waitObject: countdownEvent.WaitHandle,
                callBack: (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),
                state: tcs,
                millisecondsTimeOutInterval: millisecondsTimeOutInterval,
                executeOnlyOnce: true);

            // Register the cancellation callback
            tokenRegistration = cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), tcs);

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            // https://learn.microsoft.com/dotnet/api/system.threading.registeredwaithandle.unregister?view=net-7.0
            // If the callback method executes because the WaitHandle is
            // signaled, stop future execution of the callback method
            // by unregistering the WaitHandle.
            registeredHandle?.Unregister(null);
            await DisposeHelper.DisposeAsync(tokenRegistration).ConfigureAwait(false);
        }
    }
}
