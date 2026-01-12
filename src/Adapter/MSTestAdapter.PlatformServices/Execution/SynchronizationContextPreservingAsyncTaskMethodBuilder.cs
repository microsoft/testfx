// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

[AsyncMethodBuilder(typeof(SynchronizationContextPreservingAsyncTaskMethodBuilder<>))]
internal sealed class SynchronizationContextPreservingTask<TResult>
{
    private readonly Task<TResult> _innerTask;

    public SynchronizationContextPreservingTask(Task<TResult> innerTask)
        => _innerTask = innerTask;

    public TaskAwaiter<TResult> GetAwaiter()
        => _innerTask.GetAwaiter();

    public ConfiguredTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext)
        => _innerTask.ConfigureAwait(continueOnCapturedContext);
}

[AsyncMethodBuilder(typeof(SynchronizationContextPreservingAsyncTaskMethodBuilder))]
internal class SynchronizationContextPreservingTask
{
    private readonly Task _innerTask;

    public SynchronizationContextPreservingTask(Task innerTask)
        => _innerTask = innerTask;

    public TaskAwaiter GetAwaiter()
        => _innerTask.GetAwaiter();

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => _innerTask.ConfigureAwait(continueOnCapturedContext);
}

internal struct SynchronizationContextPreservingAsyncTaskMethodBuilder
{
    private AsyncTaskMethodBuilder _inner;

    public static SynchronizationContextPreservingAsyncTaskMethodBuilder Create()
        => new() { _inner = AsyncTaskMethodBuilder.Create() };

    public SynchronizationContextPreservingTask Task
        => new SynchronizationContextPreservingTask(_inner.Task);

    public void SetResult()
        => _inner.SetResult();

    public void SetException(Exception ex)
        => _inner.SetException(ex);

    public void SetStateMachine(IAsyncStateMachine stateMachine)
        => _inner.SetStateMachine(stateMachine);

#pragma warning disable
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
#pragma warning restore
        where TStateMachine : IAsyncStateMachine
        // Start is the whole reason why we have this custom builder.
        // BCL implementation restores back SynchronizationContext.
        // See https://github.com/dotnet/runtime/blob/c591f971241e7074f8a31ccde744aec9794e2500/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncMethodBuilderCore.cs#L45-L46
        // We want to avoid restoring the SynchronizationContext for the case when the task completes synchronously on the same thread.
        // This allows TestInitialize to set SynchronizationContext, and lets us be still fully async in our implementation.
        // But then TestMethod can see the correct SynchronizationContext in the case of TestInitialize completing synchronously.
        => stateMachine.MoveNext();

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}

internal struct SynchronizationContextPreservingAsyncTaskMethodBuilder<TResult>
{
    private AsyncTaskMethodBuilder<TResult> _inner;

    public static SynchronizationContextPreservingAsyncTaskMethodBuilder<TResult> Create()
        => new() { _inner = AsyncTaskMethodBuilder<TResult>.Create() };

    public SynchronizationContextPreservingTask<TResult> Task
        => new SynchronizationContextPreservingTask<TResult>(_inner.Task);

    public void SetResult(TResult result)
        => _inner.SetResult(result);

    public void SetException(Exception ex)
        => _inner.SetException(ex);

    public void SetStateMachine(IAsyncStateMachine stateMachine)
        => _inner.SetStateMachine(stateMachine);

#pragma warning disable
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
#pragma warning restore
        where TStateMachine : IAsyncStateMachine
        // Start is the whole reason why we have this custom builder.
        // BCL implementation restores back SynchronizationContext.
        // See https://github.com/dotnet/runtime/blob/c591f971241e7074f8a31ccde744aec9794e2500/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncMethodBuilderCore.cs#L45-L46
        // We want to avoid restoring the SynchronizationContext for the case when the task completes synchronously on the same thread.
        // This allows TestInitialize to set SynchronizationContext, and lets us be still fully async in our implementation.
        // But then TestMethod can see the correct SynchronizationContext in the case of TestInitialize completing synchronously.
        => stateMachine.MoveNext();

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}
