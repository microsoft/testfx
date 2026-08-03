// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// STEP 4 - BOUND TIME COOPERATIVELY, DON'T Thread.Sleep.
///
/// A test that can hang has no place in a reliable suite. [Timeout] bounds it, and
/// CooperativeCancellation = true makes the timeout cancel TestContext.CancellationToken and
/// wait for the test to unwind, so the work stops cleanly instead of being abandoned mid-flight
/// (which can leak file handles or background tasks and poison later tests).
///
/// Note what is NOT here: Thread.Sleep or Task.Wait to "pace" the test. Blocking sleeps are a
/// top source of slow, flaky suites; MSTEST0067 flags them. Real asynchronous waiting uses
/// await Task.Delay with the cancellation token.
/// </summary>
[TestClass]
public sealed class CancellableWorkTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task PollingLoop_ObservesCancellation()
    {
        CancellationToken token = TestContext.CancellationToken;

        // The SAME cooperative loop the deterministic test exercises. It awaits (never blocks) and
        // honors cancellation, so if the timeout fires the token is signaled and the loop exits
        // promptly. Sharing one loop means the deterministic test below actually covers this path.
        await RunCooperativeLoopAsync(token);

        Assert.IsFalse(token.IsCancellationRequested);
    }

    // Deterministic proof that the cooperative loop actually STOPS on cancellation - no timing,
    // no waiting, no flakiness. We hand it an already-cancelled token and assert it throws the
    // exact OperationCanceledException that TestContext's token would raise when [Timeout] fires.
    // It calls the SAME RunCooperativeLoopAsync the timeout test runs, so dropping cancellation
    // from that loop would fail here rather than leaving both tests green.
    [TestMethod]
    public async Task PollingLoop_StopsWhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => RunCooperativeLoopAsync(cts.Token));
    }

    // One cooperative loop, exercised by BOTH tests. It checks cancellation first (so an
    // already-cancelled token throws exactly OperationCanceledException before any awaiting) and
    // then performs a real, cancelable async wait - never a blocking Thread.Sleep.
    private static async Task RunCooperativeLoopAsync(CancellationToken token)
    {
        for (int i = 0; i < 3; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(10, token);
        }
    }
}
