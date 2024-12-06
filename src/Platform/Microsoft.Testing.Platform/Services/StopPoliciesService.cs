// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource) =>
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync());
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

    private BlockingCollection<Func<int, CancellationToken, Task>>? _maxFailedTestsCallbacks;
    private BlockingCollection<Func<Task>>? _abortCallbacks;

    public bool IsMaxFailedTestsTriggered { get; private set; }

    public bool IsAbortTriggered { get; private set; }

    private static void RegisterCallback<T>(ref BlockingCollection<T>? callbacks, T callback)
        => (callbacks ??= new()).Add(callback);

    public async Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken)
    {
        if (_maxFailedTestsCallbacks is null)
        {
            return;
        }

        foreach (Func<int, CancellationToken, Task> callback in _maxFailedTestsCallbacks)
        {
            await callback.Invoke(maxFailedTests, cancellationToken);
        }
    }

    public async Task ExecuteAbortCallbacksAsync()
    {
        if (_abortCallbacks is null)
        {
            return;
        }

        foreach (Func<Task> callback in _abortCallbacks)
        {
            await callback.Invoke();
        }
    }

    public void RegisterOnMaxFailedTestsCallback(Func<int, CancellationToken, Task> callback)
        => RegisterCallback(ref _maxFailedTestsCallbacks, callback);

    public void RegisterOnAbortCallback(Func<Task> callback)
        => RegisterCallback(ref _abortCallbacks, callback);
}
