// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class CountDownEventTests : TestBase
{
    public CountDownEventTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

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

    public async Task CountDownEvent_WaitAsyncCancelled_Succeeded()
    {
        CountdownEvent countdownEvent = new(1);
        CancellationTokenSource cts = new();
        var waiter = Task.Run(() => countdownEvent.WaitAsync(cts.Token));
#if NET8_0_OR_GREATER
        await cts.CancelAsync();
#else
        cts.Cancel();
#endif
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await waiter);
    }

    public async Task CountDownEvent_WaitAsyncCancelledByTimeout_Succeeded()
    {
        CountdownEvent countdownEvent = new(1);
        var waiter = Task.Run(() => countdownEvent.WaitAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None));
        await waiter.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsFalse(await waiter);
    }
}
