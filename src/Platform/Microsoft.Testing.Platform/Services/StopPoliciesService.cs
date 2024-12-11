// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;

    private BlockingCollection<Func<int, CancellationToken, Task>>? _maxFailedTestsCallbacks;
    private BlockingCollection<Func<Task>>? _abortCallbacks;
    private int _lastMaxFailedTests;

    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        // Note: If cancellation already requested, Register will still invoke the callback.
        testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync());
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
    }

    internal TestProcessRole? ProcessRole { get; set; }

    public bool IsMaxFailedTestsTriggered { get; private set; }

    public bool IsAbortTriggered { get; private set; }

    private static void RegisterCallback<T>(ref BlockingCollection<T>? callbacks, T callback)
        => (callbacks ??= new()).Add(callback);

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
            await callback.Invoke(maxFailedTests, cancellationToken);
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
            await callback.Invoke();
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
            await callback(_lastMaxFailedTests, _testApplicationCancellationTokenSource.CancellationToken);
        }

        RegisterCallback(ref _maxFailedTestsCallbacks, callback);
    }

    public async Task RegisterOnAbortCallbackAsync(Func<Task> callback)
    {
        if (IsAbortTriggered)
        {
            await callback();
        }

        RegisterCallback(ref _abortCallbacks, callback);
    }
}
