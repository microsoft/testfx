// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;

[TestClass]
public sealed class SynchronousAwaiterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Await_CompletedTask_CompletesNormally(bool busyWait)
    {
        bool completed = false;
        Task task = Task.CompletedTask.ContinueWith(_ => completed = true, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        task.Await(busyWait);

        Assert.IsTrue(completed);
        Assert.IsTrue(task.IsCompleted);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Await_FaultedTask_PropagatesOriginalExceptionWithoutAggregation(bool busyWait)
    {
        var failure = new InvalidOperationException("adapter failure");

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => Task.FromException(failure).Await(busyWait));

        Assert.AreSame(failure, actual);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Await_CanceledTask_PropagatesCancellationToken(bool busyWait)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        TaskCanceledException exception = Assert.ThrowsExactly<TaskCanceledException>(() => Task.FromCanceled(cancellation.Token).Await(busyWait));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }
}
