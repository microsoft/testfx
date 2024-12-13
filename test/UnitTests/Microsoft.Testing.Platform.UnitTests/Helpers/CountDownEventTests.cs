﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CountDownEventTests
{
    [TestMethod]
    public async Task CountDownEvent_WaitAsync_Succeeded()
    {
        CountdownEvent countdownEvent = new(3);
        var waiter1 = Task.Run(() => countdownEvent.WaitAsync(CancellationToken.None));
        var waiter2 = Task.Run(() => countdownEvent.WaitAsync(CancellationToken.None));

        await Task.Delay(500);
        countdownEvent.Signal();
        await Task.Delay(500);
        countdownEvent.Signal();
        await Task.Delay(500);
        countdownEvent.Signal();

        await waiter1.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(await waiter1);
        await waiter2.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(await waiter2);

        waiter1 = Task.Run(() => countdownEvent.WaitAsync(CancellationToken.None));
        await waiter1.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(await waiter1);
    }

    [TestMethod]
    public async Task CountDownEvent_WaitAsyncCanceled_Succeeded()
    {
        CountdownEvent countdownEvent = new(1);
        CancellationTokenSource cts = new();
        CancellationToken cancelToken = cts.Token;
        var waiter = Task.Run(() => countdownEvent.WaitAsync(cancelToken), cancelToken);
        await cts.CancelAsync();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await waiter);
    }

    [TestMethod]
    public async Task CountDownEvent_WaitAsyncCanceledByTimeout_Succeeded()
    {
        CountdownEvent countdownEvent = new(1);
        var waiter = Task.Run(() => countdownEvent.WaitAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None));
        await waiter.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsFalse(await waiter);
    }
}
