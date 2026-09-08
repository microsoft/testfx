// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService, IDisposable
{
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly CancellationTokenRegistration _abortRegistration;

    private readonly ConcurrentQueue<Func<int, CancellationToken, Task>> _maxFailedTestsCallbacks = new();
#if NET9_0_OR_GREATER
    private readonly Lock _abortLock = new();
#else
    private readonly object _abortLock = new();
#endif
    private readonly List<Func<Task>> _abortCallbacks = [];

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
    private Func<Task<bool>>? _deadlineStopFallback;
    private Task? _abortCallbacksTask;

    // Whether the callbacks have run. This is the one-shot gate and it is never cleared: once the callbacks
    // have run, a second trigger must not run them again and a late registration must be invoked on the spot.
    private bool _areDeadlineCallbacksExecuted;
    private bool _areAbortCallbacksExecuted;

    // Whether the run is to be reported as stopped at the deadline.
#pragma warning disable IDE0032 // Use auto property - synchronized access requires a backing field.
    private bool _isDeadlineTriggered;
#pragma warning restore IDE0032
    private int _lastMaxFailedTests;

    // One policy service can observe nested execution starts, so count active executions rather than using
    // a Boolean completion flag.
    private int _activeTestExecutions;
    private volatile bool _hasTestExecutionStarted;

    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        // Note: If cancellation already requested, Register will still invoke the callback.
        _abortRegistration = testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync().ConfigureAwait(false));
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

    public bool IsTestExecutionCompleted
        => _hasTestExecutionStarted && Volatile.Read(ref _activeTestExecutions) == 0;

    public void NotifyTestExecutionStarting()
    {
        Interlocked.Increment(ref _activeTestExecutions);
        _hasTestExecutionStarted = true;
    }

    public void NotifyTestExecutionCompleted()
    {
        _hasTestExecutionStarted = true;

        int activeExecutions;
        do
        {
            activeExecutions = Volatile.Read(ref _activeTestExecutions);
            if (activeExecutions == 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _activeTestExecutions, activeExecutions - 1, activeExecutions) != activeExecutions);
    }

    public void Dispose()
        => _abortRegistration.Dispose();

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

    public Task ExecuteAbortCallbacksAsync()
    {
        Func<Task>[] callbacks;
        TaskCompletionSource<bool> completionSource;
        lock (_abortLock)
        {
            if (_abortCallbacksTask is not null)
            {
                return _abortCallbacksTask;
            }

            _areAbortCallbacksExecuted = true;
            IsAbortTriggered = true;
            callbacks = [.. _abortCallbacks];
            _abortCallbacks.Clear();
            completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _abortCallbacksTask = completionSource.Task;
        }

        _ = ExecuteAbortCallbacksCoreAsync(callbacks, completionSource);
        return completionSource.Task;
    }

    private static async Task ExecuteAbortCallbacksCoreAsync(
        Func<Task>[] callbacks,
        TaskCompletionSource<bool> completionSource)
    {
        try
        {
            foreach (Func<Task> callback in callbacks)
            {
                // For now, we are fine if the callback crashed us. It shouldn't happen for our
                // current usage anyway and the APIs around this are all internal for now.
                await callback.Invoke().ConfigureAwait(false);
            }

            completionSource.TrySetResult(true);
        }
        catch (Exception ex)
        {
            completionSource.TrySetException(ex);
        }
    }

    public async Task ExecuteDeadlineCallbacksAsync()
    {
        Func<Task>[] callbacks;
        lock (_deadlineLock)
        {
            if (_areDeadlineCallbacksExecuted)
            {
                // The deadline is one-shot; a second trigger must not run the callbacks again.
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

    public void RegisterDeadlineStopFallback(Func<Task<bool>> callback)
    {
        lock (_deadlineLock)
        {
            _deadlineStopFallback = callback;
        }
    }

    public Task<bool> TryExecuteDeadlineStopFallbackAsync()
    {
        Func<Task<bool>>? deadlineStopFallback;
        lock (_deadlineLock)
        {
            deadlineStopFallback = _deadlineStopFallback;
        }

        return deadlineStopFallback?.Invoke() ?? Task.FromResult(false);
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
        lock (_abortLock)
        {
            if (!_areAbortCallbacksExecuted)
            {
                _abortCallbacks.Add(callback);
                return;
            }
        }

        await callback().ConfigureAwait(false);
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
