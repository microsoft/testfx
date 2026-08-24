// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;

    private readonly ConcurrentQueue<Func<int, CancellationToken, Task>> _maxFailedTestsCallbacks = new();
    private readonly ConcurrentQueue<Func<Task>> _abortCallbacks = new();

    // Guards the deadline state together with its callback list, so registration and the one-shot trigger
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

    // Whether the callbacks have run. This is the one-shot gate and it is never cleared: once the callbacks
    // have run, a second trigger must not run them again and a late registration must be invoked on the spot.
    private bool _areDeadlineCallbacksExecuted;

    // Whether the run is to be reported as stopped at the deadline. This is only the exit-code verdict, and
    // RevertDeadlineTriggered clears it when the graceful stop it was meant to precede could not be requested.
    // Kept separate from _areDeadlineCallbacksExecuted so reverting the verdict cannot re-arm the callbacks.
#pragma warning disable IDE0032 // Use auto property - synchronized access requires a backing field.
    private bool _isDeadlineTriggered;
#pragma warning restore IDE0032
    private int _lastMaxFailedTests;

    // Read on the deadline timer thread and written on the host thread that runs the request, so it is
    // volatile. Server mode resets it before constructing the per-request deadline extension.
    private volatile bool _isTestExecutionCompleted;

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

    public bool IsTestExecutionCompleted => _isTestExecutionCompleted;

    public void NotifyTestExecutionStarting() => _isTestExecutionCompleted = false;

    public void NotifyTestExecutionCompleted() => _isTestExecutionCompleted = true;

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
            if (_areDeadlineCallbacksExecuted)
            {
                // The deadline is one-shot; a second trigger must not run the callbacks again. This is
                // gated on the callback flag rather than the verdict, so a reverted verdict cannot let a
                // later trigger run them a second time.
                return;
            }

            _areDeadlineCallbacksExecuted = true;
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

    public void RevertDeadlineTriggered()
    {
        lock (_deadlineLock)
        {
            // Only the verdict is cleared. _areDeadlineCallbacksExecuted deliberately stays set: the
            // callbacks have already run, and re-arming them here would let a later trigger run them a
            // second time and would queue a late registration into a list nothing drains any more.
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
            if (!_areDeadlineCallbacksExecuted)
            {
                _deadlineCallbacks.Add(callback);
                return;
            }
        }

        // The callbacks already ran, so this registration came too late for the snapshot in
        // ExecuteDeadlineCallbacksAsync. Invoke the callback here instead, outside the lock: it is
        // arbitrary code and must not run while the deadline transition is held.
        await callback().ConfigureAwait(false);
    }
}
