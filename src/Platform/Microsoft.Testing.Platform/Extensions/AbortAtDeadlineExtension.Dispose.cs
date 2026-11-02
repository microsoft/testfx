// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

internal sealed partial class AbortAtDeadlineExtension
{
    public void Dispose()
    {
        // Capture the in-flight handler task under the lock so we either observe the task the timer
        // callback published (and drain it below) or set _disposed first (so the callback never
        // starts the handler). See OnDeadlineReached.
        Task? handleDeadlineTask;
        lock (_lock)
        {
            _disposed = true;
            handleDeadlineTask = _handleDeadlineTask;
        }

        _timer?.Dispose();
#if !NETCOREAPP
        // netstandard2.0 has no ValueTask/IAsyncDisposable, so there is no async drain path (see
        // DisposeAsync below, which is netcoreapp-only). Fall back to a bounded blocking drain so an
        // in-flight deadline handler can finish reporting before teardown, without letting a wedged
        // graceful stop hang disposal. HandleDeadlineAsync swallows its own failures, so this wait
        // never observes a fault.
        try
        {
            handleDeadlineTask?.Wait(DisposeDrainTimeout);
        }
        catch (Exception)
        {
            // Best-effort drain: disposal must never throw.
        }
#endif
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        // Capture the in-flight handler task under the lock so we either observe the task the timer
        // callback published (and drain it below) or set _disposed first (so the callback never
        // starts the handler). See OnDeadlineReached.
        Task? handleTask;
        lock (_lock)
        {
            _disposed = true;
            handleTask = _handleDeadlineTask;
        }

        _timer?.Dispose();

        // Drain an in-flight deadline handler so its reporting can finish before the host tears down,
        // but bound the wait so a wedged graceful stop cannot hang disposal. HandleDeadlineAsync
        // swallows its own failures, so awaiting the completed task here never throws.
        if (handleTask is not null)
        {
            Task completed = await Task.WhenAny(handleTask, Task.Delay(DisposeDrainTimeout)).ConfigureAwait(false);
            if (completed == handleTask)
            {
                await handleTask.ConfigureAwait(false);
            }
        }
    }
#endif
}
