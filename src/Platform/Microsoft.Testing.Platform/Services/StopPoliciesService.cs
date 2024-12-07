// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Services;

internal sealed class StopPoliciesService : IStopPoliciesService
{
    private readonly Lock _abortLock = new();

    public StopPoliciesService(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        testApplicationCancellationTokenSource.CancellationToken.Register(async () => await ExecuteAbortCallbacksAsync());
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        // This happens in mocked tests because the test will do the Cancel on the first CancellationToken property access.
        // In theory, cancellation may happen in practice fast enough before StopPoliciesService is created.
        if (testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
            _ = ExecuteAbortCallbacksAsync();
        }

        // NOTE::: Don't move the CancellationToken.Register call to an "else" here.
        // If the call is moved to else, then this race may happen:
        // 1. Check IsCancellationRequested -> false
        // 2. Cancellation happens
        // 3. Go to the "else" and do CancellationToken.Register which will never happen because
        //    cancellation already happened after IsCancellationRequested check and before CancellationToken.Register.
        //
        // With the current implementation above, we always do the Register first.
        // Then, if IsCancellationRequested was false, we are sure the register already happened and we get the callback when cancelled.
        // However, if we found IsCancellationRequested to be true, we are not sure if cancellation happened before Register or not.
        // So, we may call ExecuteAbortCallbacksAsync twice. This is fine, we handle that with a lock inside ExecuteAbortCallbacksAsync.
    }

    private BlockingCollection<Func<int, CancellationToken, Task>>? _maxFailedTestsCallbacks;
    private BlockingCollection<Func<Task>>? _abortCallbacks;

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
            await callback.Invoke(maxFailedTests, cancellationToken);
        }
    }

    public async Task ExecuteAbortCallbacksAsync()
    {
        lock (_abortLock)
        {
            if (IsAbortTriggered)
            {
                return;
            }

            IsAbortTriggered = true;
        }

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
