// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource) =>
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        // Note: If cancellation already requested, Register will still invoke the callback.
        testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync());
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

    private BlockingCollection<Func<int, CancellationToken, Task>>? _maxFailedTestsCallbacks;
    private BlockingCollection<Func<Task>>? _abortCallbacks;

    internal TestProcessRole? ProcessRole { get; set; }

    public bool IsMaxFailedTestsTriggered { get; private set; }

    public bool IsAbortTriggered { get; private set; }

    private static void RegisterCallback<T>(ref BlockingCollection<T>? callbacks, T callback)
        => (callbacks ??= new()).Add(callback);

    public async Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken)
    {
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

    public void RegisterOnMaxFailedTestsCallback(Func<int, CancellationToken, Task> callback)
    {
        if (ProcessRole != TestProcessRole.TestHost)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        RegisterCallback(ref _maxFailedTestsCallbacks, callback);
    }

    public void RegisterOnAbortCallback(Func<Task> callback)
        => RegisterCallback(ref _abortCallbacks, callback);
}
