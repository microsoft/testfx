// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class CancellationTimeoutHelperConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    public async Task RunWithCooperativeCancellationAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        using CancellationTokenSource outerCancellation = new();
        var actionGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The action is awaited on the calling thread and is still pending.
        SynchronizationContextPreservingTask<string> call = probe.Run(() => CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            _ => PendingAction(), outerCancellation, 60_000, _ => "failure"));
        Task<string> task = AsTask(call);

        task.IsCompleted.Should().BeFalse();

        actionGate.SetResult("result");
        await WaitFor(task);

        (await task).Should().Be("result");
        probe.PostCount.Should().Be(0, "the action must not be awaited on the caller's synchronization context");

        async SynchronizationContextPreservingTask<string> PendingAction()
            => await actionGate.Task.ConfigureAwait(false);
    }

    private static async Task<T> AsTask<T>(SynchronizationContextPreservingTask<T> task)
        => await task.ConfigureAwait(false);

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }
}
