// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CountDownEventTests
{
    private static readonly TimeSpan CompletionLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task CountDownEvent_WaitAsync_Succeeded()
    {
        CountdownEvent countdownEvent = new(3);
        Task<bool> waiter1 = Task.Run(() => countdownEvent.WaitAsync(CancellationToken.None));
        Task<bool> waiter2 = Task.Run(() => countdownEvent.WaitAsync(CancellationToken.None));

        await Task.Delay(500, TestContext.CancellationToken);
        countdownEvent.Signal();
        await Task.Delay(500, TestContext.CancellationToken);
        countdownEvent.Signal();
        await Task.Delay(500, TestContext.CancellationToken);
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
        Task<bool> waiter = Task.Run(() => countdownEvent.WaitAsync(cancelToken), cancelToken);
#if NETCOREAPP
        await cts.CancelAsync();
#else
        cts.Cancel();
#endif
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await waiter);
    }

    [TestMethod]
    public async Task CountDownEvent_WaitAsyncCanceledByTimeout_Succeeded()
    {
        CountdownEvent countdownEvent = new(1);
        Task<bool> waiter = Task.Run(() => countdownEvent.WaitAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None));
        await waiter.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
        Assert.IsFalse(await waiter);
    }

    [TestMethod]
    public async Task SingleThreadedWait_ZeroTimeoutDistinguishesPendingAndAlreadyCompletedCountdowns()
    {
        using CountdownEvent pending = new(2);
        using CountdownEvent completed = new(0);

        Assert.IsFalse(await WaitSingleThreadedAsync(pending, 0, CancellationToken.None));
        Assert.IsTrue(await WaitSingleThreadedAsync(completed, 0, CancellationToken.None));
        Assert.AreEqual(2, pending.CurrentCount);
    }

    [TestMethod]
    public async Task SingleThreadedWait_PreCanceledTokenRejectsPendingButDoesNotInvalidateCompletedCountdown()
    {
        using CountdownEvent pending = new(1);
        using CountdownEvent completed = new(0);
        CancellationToken canceled = new(canceled: true);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => WaitSingleThreadedAsync(pending, uint.MaxValue, canceled));

        Assert.AreEqual(canceled, exception.CancellationToken);
        Assert.IsTrue(await WaitSingleThreadedAsync(completed, uint.MaxValue, canceled));
        Assert.AreEqual(1, pending.CurrentCount);
    }

    [TestMethod]
    public async Task SingleThreadedWait_TwoSignalsCompleteTheOriginalWaitBeforeResourcesAreDisposed()
    {
        using CountdownEvent countdown = new(2);
        using CancellationTokenSource cancellation = new();
        Task<bool> originalWait = WaitSingleThreadedAsync(countdown, uint.MaxValue, cancellation.Token);
        try
        {
            Assert.IsFalse(originalWait.IsCompleted);
            countdown.Signal();
            Assert.IsFalse(originalWait.IsCompleted, "A partial countdown cannot complete the wait.");
            countdown.Signal();

            await originalWait.TimeoutAfterAsync(CompletionLimit);
            Assert.IsTrue(await originalWait);
            Assert.AreEqual(0, countdown.CurrentCount);
        }
        finally
        {
            if (countdown.CurrentCount > 0)
            {
                countdown.Signal(countdown.CurrentCount);
            }

#if NETCOREAPP
            await cancellation.CancelAsync();
#else
            cancellation.Cancel();
#endif
            // Keep and join the original wait, not only its timeout wrapper, before disposing the countdown.
            try
            {
                await originalWait.TimeoutAfterAsync(CleanupLimit);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested && originalWait.IsCanceled)
            {
                // The original wait has reached its canceled terminal state; preserve any test-body failure.
            }
        }
    }

    // The cooperative algorithm is compiled into the shipping assembly on every TFM, but selected
    // only on single-threaded runtimes. Probe that algorithm without changing global runtime flags.
    private static Task<bool> WaitSingleThreadedAsync(CountdownEvent countdown, uint timeout, CancellationToken cancellationToken)
        => (Task<bool>)typeof(CountDownEventExtensions)
            .GetMethod("WaitSingleThreadedAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [countdown, timeout, cancellationToken])!;
}
