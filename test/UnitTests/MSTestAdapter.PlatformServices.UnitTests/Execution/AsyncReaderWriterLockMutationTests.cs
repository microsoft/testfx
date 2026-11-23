// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class AsyncReaderWriterLockMutationTests : TestContainer
{
    public void ReleasingOneOfSeveralReaders_KeepsQueuedWriterPending()
    {
        var rwLock = new AsyncReaderWriterLock();
        IDisposable firstReader = Complete(rwLock.AcquireReaderAsync(CancellationToken.None));
        IDisposable secondReader = Complete(rwLock.AcquireReaderAsync(CancellationToken.None));
        Task<IDisposable> writer = rwLock.AcquireWriterAsync(CancellationToken.None);
        writer.IsCompleted.Should().BeFalse();

        firstReader.Dispose();

        writer.IsCompleted.Should().BeFalse("a writer cannot start while another reader still holds the lock");

        secondReader.Dispose();

        writer.IsCompleted.Should().BeTrue();
    }

    public void GrantedWriter_HoldsTheLockExclusively_AndHandsOverInFifoOrder()
    {
        var rwLock = new AsyncReaderWriterLock();
        IDisposable reader = Complete(rwLock.AcquireReaderAsync(CancellationToken.None));
        Task<IDisposable> writer = rwLock.AcquireWriterAsync(CancellationToken.None);
        reader.Dispose();
        IDisposable activeWriter = Complete(writer);

        Task<IDisposable> laterReader = rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> laterWriter = rwLock.AcquireWriterAsync(CancellationToken.None);

        laterReader.IsCompleted.Should().BeFalse("a reader cannot enter while a writer holds the lock");
        laterWriter.IsCompleted.Should().BeFalse("a writer cannot enter while another writer holds the lock");

        activeWriter.Dispose();

        laterReader.IsCompleted.Should().BeTrue();
        laterWriter.IsCompleted.Should().BeFalse("the queued writer waits for the reader that was queued before it");

        Complete(laterReader).Dispose();

        laterWriter.IsCompleted.Should().BeTrue();

        Complete(laterWriter).Dispose();

        rwLock.AcquireWriterAsync(CancellationToken.None).IsCompleted.Should().BeTrue("every holder released the lock");
    }

    public void ReleasingWriter_GrantsAllLeadingQueuedReaders_AndCountsEachOfThem()
    {
        var rwLock = new AsyncReaderWriterLock();
        IDisposable writer = Complete(rwLock.AcquireWriterAsync(CancellationToken.None));
        Task<IDisposable> first = rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> second = rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> third = rwLock.AcquireReaderAsync(CancellationToken.None);

        writer.Dispose();

        first.IsCompleted.Should().BeTrue();
        second.IsCompleted.Should().BeTrue();
        third.IsCompleted.Should().BeTrue();

        Task<IDisposable> extraReader = rwLock.AcquireReaderAsync(CancellationToken.None);
        extraReader.IsCompleted.Should().BeTrue("readers share the lock");

        Task<IDisposable> laterWriter = rwLock.AcquireWriterAsync(CancellationToken.None);
        laterWriter.IsCompleted.Should().BeFalse("readers hold the lock");

        Complete(first).Dispose();
        Complete(second).Dispose();
        Complete(third).Dispose();

        laterWriter.IsCompleted.Should().BeFalse("the extra reader still holds the lock");

        Complete(extraReader).Dispose();

        laterWriter.IsCompleted.Should().BeTrue();
    }

    public void CancellingQueuedWriter_GrantsTheReadersQueuedBehindIt()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cancellation = new CancellationTokenSource();
        IDisposable activeReader = Complete(rwLock.AcquireReaderAsync(CancellationToken.None));
        Task<IDisposable> writer = rwLock.AcquireWriterAsync(cancellation.Token);
        Task<IDisposable> first = rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> second = rwLock.AcquireReaderAsync(CancellationToken.None);
        first.IsCompleted.Should().BeFalse("the queued writer blocks the readers behind it");

        cancellation.Cancel();

        writer.IsCanceled.Should().BeTrue();
        first.IsCompleted.Should().BeTrue();
        second.IsCompleted.Should().BeTrue();

        Task<IDisposable> laterWriter = rwLock.AcquireWriterAsync(CancellationToken.None);
        laterWriter.IsCompleted.Should().BeFalse();

        activeReader.Dispose();
        Complete(first).Dispose();

        laterWriter.IsCompleted.Should().BeFalse("the second queued reader still holds the lock");

        Complete(second).Dispose();

        laterWriter.IsCompleted.Should().BeTrue();
    }

    public void CancellingQueuedWriter_KeepsReadersPendingWhileAnotherWriterIsActive()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cancellation = new CancellationTokenSource();
        IDisposable activeWriter = Complete(rwLock.AcquireWriterAsync(CancellationToken.None));
        Task<IDisposable> cancelableWriter = rwLock.AcquireWriterAsync(cancellation.Token);
        Task<IDisposable> reader = rwLock.AcquireReaderAsync(CancellationToken.None);

        cancellation.Cancel();

        cancelableWriter.IsCanceled.Should().BeTrue();
        reader.IsCompleted.Should().BeFalse("the active writer still owns the lock");

        activeWriter.Dispose();

        reader.IsCompleted.Should().BeTrue();
    }

    public void CancellingQueuedWriter_KeepsFollowingWriterPendingWhileAnotherWriterIsActive()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cancellation = new CancellationTokenSource();
        IDisposable activeWriter = Complete(rwLock.AcquireWriterAsync(CancellationToken.None));
        Task<IDisposable> followingWriter = rwLock.AcquireWriterAsync(CancellationToken.None);
        Task<IDisposable> cancelableWriter = rwLock.AcquireWriterAsync(cancellation.Token);

        cancellation.Cancel();

        cancelableWriter.IsCanceled.Should().BeTrue();
        followingWriter.IsCompleted.Should().BeFalse("the active writer still owns the lock");

        activeWriter.Dispose();

        followingWriter.IsCompleted.Should().BeTrue();
    }

    public void CancellationRacingWithAGrantInProgress_DoesNotCancelTheAlreadyGrantedWaiter()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var cancellation = new CancellationTokenSource();
        IDisposable writer = Complete(rwLock.AcquireWriterAsync(CancellationToken.None));
        Task<IDisposable> first = rwLock.AcquireReaderAsync(CancellationToken.None);
        Task<IDisposable> second = rwLock.AcquireReaderAsync(cancellation.Token);

        // Completing the first reader synchronously queues this continuation, which cancels the second reader
        // after it was granted (it left the queue) but before its own task was completed.
        var scheduler = new CancelOnQueueTaskScheduler(cancellation);
        _ = first.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.None, scheduler);

        writer.Dispose();

        scheduler.QueuedTaskCount.Should().Be(1);
        cancellation.IsCancellationRequested.Should().BeTrue();
        second.Status.Should().Be(TaskStatus.RanToCompletion);

        Task<IDisposable> laterWriter = rwLock.AcquireWriterAsync(CancellationToken.None);
        laterWriter.IsCompleted.Should().BeFalse();

        Complete(first).Dispose();
        Complete(second).Dispose();

        laterWriter.IsCompleted.Should().BeTrue();
    }

    public void GrantedCancelableWaiter_DoesNotKeepTheLockAliveThroughItsCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();

        WeakReference lockReference = GrantCancelableWaiterAndDropTheLock(cancellation.Token);

        // A registration that is never disposed stays reachable from the (still alive) source, and through its
        // callback state keeps the waiter and the lock alive.
        for (int i = 0; i < 5 && lockReference.IsAlive; i++)
        {
            // Force collection so the weak-reference assertion is deterministic.
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        lockReference.IsAlive.Should().BeFalse("the cancellation registration must be disposed once the waiter is granted");
        GC.KeepAlive(cancellation);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference GrantCancelableWaiterAndDropTheLock(CancellationToken cancellationToken)
    {
        var rwLock = new AsyncReaderWriterLock();
        IDisposable activeWriter = Complete(rwLock.AcquireWriterAsync(CancellationToken.None));
        Task<IDisposable> queuedWriter = rwLock.AcquireWriterAsync(cancellationToken);
        queuedWriter.IsCompleted.Should().BeFalse();

        activeWriter.Dispose();

        Complete(queuedWriter).Dispose();
        return new WeakReference(rwLock);
    }

    private static IDisposable Complete(Task<IDisposable> task)
    {
        task.IsCompleted.Should().BeTrue("the lock is expected to be granted synchronously");
        return task.Result;
    }

    private sealed class CancelOnQueueTaskScheduler : TaskScheduler
    {
        private readonly CancellationTokenSource _cancellationToCancel;

        public CancelOnQueueTaskScheduler(CancellationTokenSource cancellationToCancel)
            => _cancellationToCancel = cancellationToCancel;

        public int QueuedTaskCount { get; private set; }

        protected override void QueueTask(Task task)
        {
            QueuedTaskCount++;
            _cancellationToCancel.Cancel();
            TryExecuteTask(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            => false;

        protected override IEnumerable<Task> GetScheduledTasks()
            => [];
    }
}
