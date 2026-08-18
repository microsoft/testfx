// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;

    private readonly ConcurrentQueue<Func<int, CancellationToken, Task>> _maxFailedTestsCallbacks = new();
    private readonly ConcurrentQueue<Func<Task>> _abortCallbacks = new();

    // Guards the deadline verdict together with its callback list, so registration and the one-shot trigger
    // cannot interleave and drop a callback. A flag plus a concurrent queue is not enough: the registering
    // thread can read the flag as false, the trigger can then set it and snapshot a still-empty queue, and only
    // afterwards does the callback land in the queue -- where nothing will ever invoke it, because the deadline
    // fires once. Under this lock a callback is invoked exactly once, either by the trigger (it was in the list
    // when the snapshot was taken) or by the registering thread itself (the trigger had already happened).
#if NET9_0_OR_GREATER
    private readonly Lock _deadlineLock = new();
#else
    private readonly object _deadlineLock = new();
#endif
    private readonly List<Func<Task>> _deadlineCallbacks = [];
    private bool _isDeadlineTriggered;
    private int _lastMaxFailedTests;

    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        // Note: If cancellation already requested, Register will still invoke the callback.
        testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync().ConfigureAwait(false));
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
    }

    internal TestProcessRole? ProcessRole { get; set; }

    public bool IsMaxFailedTestsTriggered { get; private set; }

    public bool IsAbortTriggered { get; private set; }

    public bool IsDeadlineTriggered
    {
        get
        {
            lock (_deadlineLock)
            {
                return _isDeadlineTriggered;
            }
        }
    }

    public async Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken)
    {
        _lastMaxFailedTests = maxFailedTests;
        IsMaxFailedTestsTriggered = true;
        if (_maxFailedTestsCallbacks is null)
        {
            return;
        }

        foreach (Func<int, CancellationToken, Task> callback in _maxFailedTestsCallbacks)
        {
            // For now, we are fine if the callback crashed us. It shouldn't happen for our
            // current usage anyway and the APIs around this are all internal for now.
            await callback.Invoke(maxFailedTests, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ExecuteAbortCallbacksAsync()
    {
        IsAbortTriggered = true;

        if (_abortCallbacks is null)
        {
            return;
        }

        foreach (Func<Task> callback in _abortCallbacks)
        {
            // For now, we are fine if the callback crashed us. It shouldn't happen for our
            // current usage anyway and the APIs around this are all internal for now.
            await callback.Invoke().ConfigureAwait(false);
        }
    }

    public async Task ExecuteDeadlineCallbacksAsync()
    {
        Func<Task>[] callbacks;
        lock (_deadlineLock)
        {
            if (_isDeadlineTriggered)
            {
                // The deadline is one-shot; a second trigger must not run the callbacks again.
                return;
            }

            _isDeadlineTriggered = true;

            // Take the callbacks under the lock and clear the list, so a callback registered from now on is
            // invoked by RegisterOnDeadlineCallbackAsync instead of being silently dropped here.
            callbacks = [.. _deadlineCallbacks];
            _deadlineCallbacks.Clear();
        }

        foreach (Func<Task> callback in callbacks)
        {
            // For now, we are fine if the callback crashed us. It shouldn't happen for our
            // current usage anyway and the APIs around this are all internal for now.
            await callback.Invoke().ConfigureAwait(false);
        }
    }

    public void RevertDeadlineTrigger()
    {
        lock (_deadlineLock)
        {
            _isDeadlineTriggered = false;
        }
    }

    public async Task RegisterOnMaxFailedTestsCallbackAsync(Func<int, CancellationToken, Task> callback)
    {
        if (ProcessRole != TestProcessRole.TestHost)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        if (IsMaxFailedTestsTriggered)
        {
            await callback(_lastMaxFailedTests, _testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }

        _maxFailedTestsCallbacks.Enqueue(callback);
    }

    public async Task RegisterOnAbortCallbackAsync(Func<Task> callback)
    {
        if (IsAbortTriggered)
        {
            await callback().ConfigureAwait(false);
        }

        _abortCallbacks.Enqueue(callback);
    }

    public async Task RegisterOnDeadlineCallbackAsync(Func<Task> callback)
    {
        lock (_deadlineLock)
        {
            if (!_isDeadlineTriggered)
            {
                _deadlineCallbacks.Add(callback);
                return;
            }
        }

        // The deadline already fired, so this registration came too late for the snapshot in
        // ExecuteDeadlineCallbacksAsync. Invoke the callback here instead, outside the lock: it is
        // arbitrary code and must not run while the deadline transition is held.
        await callback().ConfigureAwait(false);
    }
}
