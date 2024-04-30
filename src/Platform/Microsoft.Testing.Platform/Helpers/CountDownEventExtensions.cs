// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class CountDownEventExtensions
{
    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync(uint.MaxValue, cancellationToken);

    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, TimeSpan timeout, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync((uint)timeout.TotalMilliseconds, cancellationToken);

    internal static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, uint millisecondsTimeOutInterval, CancellationToken cancellationToken)
    {
        RegisteredWaitHandle? registeredHandle = null;
        CancellationTokenRegistration tokenRegistration = default;
        try
        {
            TaskCompletionSource<bool> tcs = new();

            // https://learn.microsoft.com/dotnet/api/system.threading.threadpool.registerwaitforsingleobject?view=net-7.0
#pragma warning disable SA1115 // Parameter should follow comma

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
#pragma warning restore SA1115 // Parameter should follow comma

            // Register the cancellation callback
            tokenRegistration = cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), tcs);

            return await tcs.Task;
        }
        finally
        {
            // https://learn.microsoft.com/dotnet/api/system.threading.registeredwaithandle.unregister?view=net-7.0
            // If the callback method executes because the WaitHandle is
            // signaled, stop future execution of the callback method
            // by unregistering the WaitHandle.
            registeredHandle?.Unregister(null);
            await DisposeHelper.DisposeAsync(tokenRegistration);
        }
    }
}
