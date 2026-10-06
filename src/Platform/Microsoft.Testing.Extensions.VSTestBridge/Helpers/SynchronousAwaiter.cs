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
            // GetAwaiter().GetResult() blocks the calling thread the same way spinning does (both
            // keep this thread from returning to the producer until the task completes), but it parks
            // the thread on a kernel wait handle instead of burning CPU polling IsCompleted. Every call
            // site here is invoked once per test start/end or per discovered test case, so for large
            // suites the busy-wait default used to hold a core pegged for no measurable latency benefit
            // whenever the awaited publish involved any real (even inline-blocking-consumer) work.
            valueTask.GetAwaiter().GetResult();
        }
    }
}
