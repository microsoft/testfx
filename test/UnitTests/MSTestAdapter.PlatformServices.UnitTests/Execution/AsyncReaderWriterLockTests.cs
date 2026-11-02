// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class AsyncReaderWriterLockTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task MultipleReaders_AcquireConcurrently()
    {
        var rwLock = new AsyncReaderWriterLock();

        IDisposable first = await rwLock.AcquireReaderAsync(CancellationToken.None);
        // A second reader must be grantable immediately while the first is held.
        Task<IDisposable> secondTask = rwLock.AcquireReaderAsync(CancellationToken.None);

        secondTask.IsCompleted.Should().BeTrue("readers do not block each other");
        IDisposable second = await secondTask;

        first.Dispose();
        second.Dispose();
    }

    public async Task Writer_IsExclusive_BlocksReaderUntilReleased()
    {
        var rwLock = new AsyncReaderWriterLock();

        IDisposable writer = await rwLock.AcquireWriterAsync(CancellationToken.None);
        Task<IDisposable> readerTask = rwLock.AcquireReaderAsync(CancellationToken.None);

        readerTask.IsCompleted.Should().BeFalse("a reader cannot proceed while a writer holds the lock");

        writer.Dispose();

        IDisposable reader = await WaitFor(readerTask);
        reader.Dispose();
    }

    public async Task Reader_BlocksWriterUntilReleased()
    {
        var rwLock = new AsyncReaderWriterLock();

        IDisposable reader = await rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> writerTask = rwLock.AcquireWriterAsync(CancellationToken.None);

        writerTask.IsCompleted.Should().BeFalse("a writer cannot proceed while a reader holds the lock");

        reader.Dispose();

        IDisposable writer = await WaitFor(writerTask);
        writer.Dispose();
    }

    public async Task QueuedWriter_BlocksLaterReaders_FifoFairness()
    {
        var rwLock = new AsyncReaderWriterLock();

        IDisposable firstReader = await rwLock.AcquireReaderAsync(CancellationToken.None);

        // Writer queues behind the active reader.
        Task<IDisposable> writerTask = rwLock.AcquireWriterAsync(CancellationToken.None);
        writerTask.IsCompleted.Should().BeFalse();

        // A reader arriving after the queued writer must not overtake it (no writer starvation).
        Task<IDisposable> lateReaderTask = rwLock.AcquireReaderAsync(CancellationToken.None);
        lateReaderTask.IsCompleted.Should().BeFalse("a later reader cannot overtake a queued writer");

        firstReader.Dispose();

        // With the first reader gone, the writer runs next.
        IDisposable writer = await WaitFor(writerTask);
        lateReaderTask.IsCompleted.Should().BeFalse("the late reader still waits behind the writer");

        writer.Dispose();

        IDisposable lateReader = await WaitFor(lateReaderTask);
        lateReader.Dispose();
    }

    public async Task Acquire_WithAlreadyCanceledToken_Throws()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cts = new CancellationTokenSource();
#pragma warning disable VSTHRD103 // Cancel synchronously blocks - CancelAsync is not available on all target frameworks.
        cts.Cancel();
#pragma warning restore VSTHRD103

        Func<Task> act = async () => await rwLock.AcquireWriterAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public async Task Cancellation_OfQueuedWaiter_UnblocksAndDoesNotBreakLock()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cts = new CancellationTokenSource();

        IDisposable writer = await rwLock.AcquireWriterAsync(CancellationToken.None);

        Task<IDisposable> queuedReaderTask = rwLock.AcquireReaderAsync(cts.Token);
        queuedReaderTask.IsCompleted.Should().BeFalse();

#pragma warning disable VSTHRD103 // Cancel synchronously blocks - CancelAsync is not available on all target frameworks.
        cts.Cancel();
#pragma warning restore VSTHRD103

        Func<Task> act = async () => await queuedReaderTask;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // The lock is still healthy: releasing the writer lets a fresh reader in.
        writer.Dispose();
        IDisposable reader = await WaitFor(rwLock.AcquireReaderAsync(CancellationToken.None));
        reader.Dispose();
    }

    public async Task Cancellation_RacingWithGrant_DoesNotDeadlock()
    {
        // Cancel a queued waiter at the same instant it is being granted. If the grant path disposes that
        // waiter's cancellation registration while holding the internal gate, the disposal blocks until the
        // cancel callback finishes - and that callback is itself blocked acquiring the same gate. The lock
        // then deadlocks permanently, wedging a scheduler worker so the entire test host hangs. Triggered by
        // any run cancellation (Ctrl-C, "Cancel run") that lands while a lock is being handed off.
        // Time-bounded so a regression fails this test instead of hanging the suite.
        for (int i = 0; i < 200; i++)
        {
            var rwLock = new AsyncReaderWriterLock();
            IDisposable held = await rwLock.AcquireWriterAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource();
            Task<IDisposable> queued = rwLock.AcquireWriterAsync(cts.Token);
            queued.IsCompleted.Should().BeFalse("the waiter must be queued behind the held writer");

            using var barrier = new Barrier(2);
            var canceller = Task.Run(() =>
            {
                barrier.SignalAndWait();
#pragma warning disable VSTHRD103 // Cancel synchronously blocks - CancelAsync is not available on all target frameworks.
                cts.Cancel();
#pragma warning restore VSTHRD103
            });
            var releaser = Task.Run(() =>
            {
                barrier.SignalAndWait();
                held.Dispose();
            });

            var both = Task.WhenAll(canceller, releaser);
            Task completed = await Task.WhenAny(both, Task.Delay(Timeout));
            completed.Should().BeSameAs(both, "cancelling a waiter while it is being granted must not deadlock");
            await both;

            // Either outcome is correct - the waiter won the grant, or it was cancelled first - but it must
            // settle rather than hang.
            try
            {
                (await WaitFor(queued)).Dispose();
            }
            catch (OperationCanceledException)
            {
                queued.IsCanceled.Should().BeTrue("the waiter settled as cancelled rather than faulted");
            }
        }
    }

    private static async Task<T> WaitFor<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        return await task;
    }
}
