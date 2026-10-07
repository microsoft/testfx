// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VSTestBridge.Helpers;

/// <summary>
/// A helper class to provide synchronous awaiter. Because of vstest sync APIs we need to wait synchronously.
/// </summary>
internal static class SynchronousAwaiter
{
    public static void Await(this Task valueTask, bool busyWait = false)
    {
        if (busyWait)
        {
            var spin = default(SpinWait);
            while (!valueTask.IsCompleted)
            {
                spin.SpinOnce();
            }

            // We want to observe the exception
            valueTask.GetAwaiter().GetResult();
        }
        else
        {
            // Block rather than explicitly poll when the task is incomplete.
            // Already-completed tasks, including uncontended synchronous inline consumption,
            // do not wait; GetResult still propagates faults and cancellation.
            valueTask.GetAwaiter().GetResult();
        }
    }
}
