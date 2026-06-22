// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

internal static class StaThreadHelper
{
    internal static async Task<T> RunOnApartmentThreadIfNeededAsync<T>(
        ApartmentState? requestedApartmentState,
        string threadName,
        Func<Task<T>> action,
        Func<T> threadResultFactory,
        Func<Thread, Task> waitForThreadAsync,
        Func<Exception, T> exceptionHandler,
        Action warningHandler)
    {
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindowsOS
            && requestedApartmentState is not null
            && Thread.CurrentThread.GetApartmentState() != requestedApartmentState)
        {
            T threadResult = default!;
            bool hasThreadResult = false;
            Thread entryPointThread = new(() =>
            {
                threadResult = action().GetAwaiter().GetResult();
                hasThreadResult = true;
            })
            {
                Name = threadName,
            };

            entryPointThread.SetApartmentState(requestedApartmentState.Value);
            entryPointThread.Start();

            try
            {
                await waitForThreadAsync(entryPointThread).ConfigureAwait(false);
                return hasThreadResult ? threadResult : threadResultFactory();
            }
            catch (Exception ex)
            {
                return exceptionHandler(ex);
            }
        }

        if (!isWindowsOS && requestedApartmentState is ApartmentState.STA)
        {
            warningHandler();
        }

        return await action().ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    internal static Task RunOnStaThreadAsync(Func<Task> action)
    {
        TaskCompletionSource<int> tcs = new();
        Thread entryPointThread = new(() =>
        {
            try
            {
                Task task = action();
                task.GetAwaiter().GetResult();
                tcs.SetResult(0);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        entryPointThread.SetApartmentState(ApartmentState.STA);
        entryPointThread.Start();
        return tcs.Task;
    }
}
