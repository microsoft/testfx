// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// A lightweight in-process asynchronous reader/writer lock used by the parallel test scheduler to serialize
/// only the tests that declare conflicting <c>[ResourceLock]</c> keys. Multiple readers may hold the lock
/// concurrently; a writer holds it exclusively. Acquisition is FIFO-fair (a queued writer prevents newly
/// arriving readers from overtaking it, so neither readers nor writers can be starved) and honors cancellation.
/// </summary>
/// <remarks>
/// The BCL has no asynchronous reader/writer lock, and named <see cref="System.Threading.Mutex"/> has thread
/// affinity that is fatal for a <see cref="Task"/>-based scheduler (a continuation after <c>await</c> may resume
/// on a different pool thread), so this small internal implementation is used instead. Scope is the test host
/// process only.
/// </remarks>
internal sealed class AsyncReaderWriterLock
{
#pragma warning disable IDE0330 // Use 'System.Threading.Lock' - not available on all target frameworks of this project.
    private readonly object _gate = new();
#pragma warning restore IDE0330
    private readonly LinkedList<Waiter> _queue = new();
    private int _activeReaders;
    private bool _activeWriter;

    /// <summary>
    /// Acquires the lock for shared (read) access, waiting asynchronously if a writer holds or is waiting for it.
    /// </summary>
    /// <param name="cancellationToken">Token used to abandon the wait.</param>
    /// <returns>A disposable that releases the read access when disposed.</returns>
    public Task<IDisposable> AcquireReaderAsync(CancellationToken cancellationToken)
        => AcquireAsync(isWriter: false, cancellationToken);

    /// <summary>
    /// Acquires the lock for exclusive (write) access, waiting asynchronously until no readers or writer hold it.
    /// </summary>
    /// <param name="cancellationToken">Token used to abandon the wait.</param>
    /// <returns>A disposable that releases the write access when disposed.</returns>
    public Task<IDisposable> AcquireWriterAsync(CancellationToken cancellationToken)
        => AcquireAsync(isWriter: true, cancellationToken);

    private Task<IDisposable> AcquireAsync(bool isWriter, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IDisposable>(cancellationToken);
        }

        lock (_gate)
        {
            if (CanGrantImmediately(isWriter))
            {
                Grant(isWriter);
                return Task.FromResult<IDisposable>(new Releaser(this, isWriter));
            }

            var waiter = new Waiter(this, isWriter);
            waiter.Node = _queue.AddLast(waiter);

            // Register cancellation while holding the gate so a concurrent ProcessQueue cannot grant this waiter
            // between the enqueue and the registration.
            if (cancellationToken.CanBeCanceled)
            {
                waiter.CancellationRegistration = cancellationToken.Register(static state => ((Waiter)state!).Cancel(), waiter);
            }

            return waiter.Task;
        }
    }

    private bool CanGrantImmediately(bool isWriter)
        => isWriter
            ? _activeReaders == 0 && !_activeWriter && _queue.Count == 0
            : !_activeWriter && _queue.Count == 0;

    private void Grant(bool isWriter)
    {
        if (isWriter)
        {
            _activeWriter = true;
        }
        else
        {
            _activeReaders++;
        }
    }

    private void Release(bool isWriter)
    {
        lock (_gate)
        {
            if (isWriter)
            {
                _activeWriter = false;
            }
            else
            {
                _activeReaders--;
            }

            ProcessQueue();
        }
    }

    // Must be called while holding _gate. Grants as many queued waiters as the current state allows, honoring
    // FIFO order: a writer at the head blocks everyone behind it; a run of leading readers is granted together.
    private void ProcessQueue()
    {
        while (_queue.First is { Value: Waiter head })
        {
            if (head.IsWriter)
            {
                if (_activeReaders != 0 || _activeWriter)
                {
                    break;
                }

                _queue.RemoveFirst();
                head.Node = null;
                Grant(isWriter: true);
                head.TryGrant();
                break;
            }

            if (_activeWriter)
            {
                break;
            }

            _queue.RemoveFirst();
            head.Node = null;
            Grant(isWriter: false);
            head.TryGrant();
        }
    }

    private void CancelWaiter(Waiter waiter)
    {
        bool removed = false;
        lock (_gate)
        {
            if (waiter.Node is { } node)
            {
                _queue.Remove(node);
                waiter.Node = null;
                removed = true;

                // Removing a queued writer that was at the head may unblock trailing readers.
                ProcessQueue();
            }
        }

        if (removed)
        {
            waiter.CompleteCanceled();
        }
    }

    private sealed class Waiter
    {
        private readonly AsyncReaderWriterLock _owner;
        private readonly TaskCompletionSource<IDisposable> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Waiter(AsyncReaderWriterLock owner, bool isWriter)
        {
            _owner = owner;
            IsWriter = isWriter;
        }

        public bool IsWriter { get; }

        public LinkedListNode<Waiter>? Node { get; set; }

        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public Task<IDisposable> Task => _tcs.Task;

        // Called under the owner's gate. The waiter has been granted; complete it (continuations run
        // asynchronously so this is safe to call while holding the gate).
        public void TryGrant()
        {
            CancellationRegistration.Dispose();
            _tcs.TrySetResult(new Releaser(_owner, IsWriter));
        }

        public void Cancel() => _owner.CancelWaiter(this);

        public void CompleteCanceled()
        {
            CancellationRegistration.Dispose();
            _tcs.TrySetCanceled();
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncReaderWriterLock _owner;
        private readonly bool _isWriter;
        private int _disposed;

        public Releaser(AsyncReaderWriterLock owner, bool isWriter)
        {
            _owner = owner;
            _isWriter = isWriter;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Release(_isWriter);
            }
        }
    }
}
