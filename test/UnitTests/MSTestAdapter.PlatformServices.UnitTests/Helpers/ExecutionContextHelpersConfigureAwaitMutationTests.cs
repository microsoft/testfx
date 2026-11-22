// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ExecutionContextHelpersConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    public async Task RunOnContextAsync_WithAnExecutionContext_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var actionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ExecutionContext executionContext = ExecutionContext.Capture()!;

        // The action runs synchronously under the captured execution context and is still pending when it returns.
        SynchronizationContextPreservingTask call = probe.Run(() => ExecutionContextHelpers.RunOnContextAsync(executionContext, PendingAction));
        Task task = AsTask(call);

        task.IsCompleted.Should().BeFalse();

        actionGate.SetResult(true);
        await WaitFor(task);

        probe.PostCount.Should().Be(0, "the action must not be awaited on the caller's synchronization context");

        async SynchronizationContextPreservingTask PendingAction()
            => await actionGate.Task.ConfigureAwait(false);
    }

    private static async Task AsTask(SynchronizationContextPreservingTask task)
        => await task.ConfigureAwait(false);

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }
}
