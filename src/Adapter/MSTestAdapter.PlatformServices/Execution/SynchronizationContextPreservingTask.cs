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
internal sealed class SynchronizationContextPreservingTask
{
    private readonly Task _innerTask;

    public SynchronizationContextPreservingTask(Task innerTask)
        => _innerTask = innerTask;

    public TaskAwaiter GetAwaiter()
        => _innerTask.GetAwaiter();

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => _innerTask.ConfigureAwait(continueOnCapturedContext);
}
