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

        Waiter waiter;
        lock (_gate)
        {
            if (CanGrantImmediately(isWriter))
            {
                Grant(isWriter);
                return Task.FromResult<IDisposable>(new Releaser(this, isWriter));
            }

            waiter = new Waiter(this, isWriter);
            waiter.Node = _queue.AddLast(waiter);
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return waiter.Task;
        }

        // Register outside the gate: when the token is already cancelled, Register invokes the callback
        // synchronously on this thread, and that callback needs the gate. Registering under the gate would
        // therefore re-enter it and could end up disposing other waiters' registrations while holding it.
        CancellationTokenRegistration registration = cancellationToken.Register(static state => ((Waiter)state!).Cancel(), waiter);

        bool stillQueued;
        lock (_gate)
        {
            stillQueued = waiter.Node is not null;
            if (stillQueued)
            {
                waiter.CancellationRegistration = registration;
            }
        }

        if (!stillQueued)
        {
            // The waiter was granted or cancelled while we were registering, so it will never consult the
            // registration. Dispose it here - outside the gate, like every other disposal.
#pragma warning disable VSTHRD103 // Dispose synchronously blocks - DisposeAsync is not available on all target frameworks of this project.
            registration.Dispose();
#pragma warning restore VSTHRD103
        }

        return waiter.Task;
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
        List<Waiter>? granted = null;
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

            ProcessQueue(ref granted);
        }

        CompleteGranted(granted);
    }

    // Must be called while holding _gate. Grants as many queued waiters as the current state allows, honoring
    // FIFO order: a writer at the head blocks everyone behind it; a run of leading readers is granted together.
    // Granted waiters are only collected here: the caller must complete them after releasing the gate, because
    // completing disposes the cancellation registration, and that disposal blocks until any concurrently running
    // cancellation callback finishes - a callback which itself needs the gate.
    private void ProcessQueue(ref List<Waiter>? granted)
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
                (granted ??= []).Add(head);
                break;
            }

            if (_activeWriter)
            {
                break;
            }

            _queue.RemoveFirst();
            head.Node = null;
            Grant(isWriter: false);
            (granted ??= []).Add(head);
        }
    }

    // Must be called WITHOUT holding _gate. Safe because each waiter's Node was cleared under the gate, so a
    // cancellation callback can no longer complete it - the grant is already final.
    private static void CompleteGranted(List<Waiter>? granted)
    {
        if (granted is null)
        {
            return;
        }

        foreach (Waiter waiter in granted)
        {
            waiter.CompleteGranted();
        }
    }

    private void CancelWaiter(Waiter waiter)
    {
        bool removed = false;
        List<Waiter>? granted = null;
        lock (_gate)
        {
            if (waiter.Node is { } node)
            {
                _queue.Remove(node);
                waiter.Node = null;
                removed = true;

                // Removing a queued writer that was at the head may unblock trailing readers.
                ProcessQueue(ref granted);
            }
        }

        CompleteGranted(granted);

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

        // Called WITHOUT the owner's gate held. Disposing the registration blocks until any concurrently running
        // cancellation callback completes, and that callback takes the gate - so doing this under the gate would
        // deadlock. Safe here because Node was cleared under the gate, so this waiter can no longer be cancelled.
        public void CompleteGranted()
        {
            CancellationRegistration.Dispose();
            _tcs.TrySetResult(new Releaser(_owner, IsWriter));
        }

        public void Cancel() => _owner.CancelWaiter(this);

        // Called WITHOUT the owner's gate held, for the same reason as CompleteGranted.
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
