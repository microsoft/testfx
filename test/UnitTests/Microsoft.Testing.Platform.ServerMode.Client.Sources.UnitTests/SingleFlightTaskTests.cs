// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

[TestClass]
public sealed class SingleFlightTaskTests
{
    [TestMethod]
    public async Task StartWithSynchronousActionReturnsSameTaskAndInvokesActionOnce()
    {
        var singleFlight = new SingleFlightTask();
        int invocationCount = 0;
        var tasks = new Task[32];

        Parallel.For(0, tasks.Length, i => tasks[i] = singleFlight.StartAsync(() => Interlocked.Increment(ref invocationCount)));

        await Task.WhenAll(tasks);
        Assert.AreEqual(1, invocationCount);
        foreach (Task task in tasks)
        {
            Assert.AreSame(tasks[0], task);
        }
    }

    [TestMethod]
    public async Task StartWithAsynchronousActionReturnsSameTaskAndInvokesActionOnce()
    {
        var singleFlight = new SingleFlightTask();
        int invocationCount = 0;
        var releaseAction = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task[32];

        Parallel.For(
            0,
            tasks.Length,
            i => tasks[i] = singleFlight.StartAsync(async () =>
            {
                Interlocked.Increment(ref invocationCount);
                await releaseAction.Task;
            }));
        releaseAction.SetResult(true);

        await Task.WhenAll(tasks);
        Assert.AreEqual(1, invocationCount);
        foreach (Task task in tasks)
        {
            Assert.AreSame(tasks[0], task);
        }
    }
}
