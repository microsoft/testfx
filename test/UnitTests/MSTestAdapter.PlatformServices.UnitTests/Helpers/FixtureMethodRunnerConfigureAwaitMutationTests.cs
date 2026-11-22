// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class FixtureMethodRunnerConfigureAwaitMutationTests : TestContainer
{
    private const string Canceled = "cancelled {0}.{1}";
    private const string TimedOut = "timed out {0}.{1} after {2}";
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly MethodInfo FixtureMethod = typeof(FixtureMethodRunnerConfigureAwaitMutationTests).GetMethod(nameof(FixtureMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task RunWithTimeoutAndCancellationAsync_WithCooperativeCancellation_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        using CancellationTokenSource cancellation = new();
        var actionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(60_000) { CooperativeCancellation = true });

        // Cooperative cancellation also runs the action on the calling thread, through the cooperative cancellation helper.
        SynchronizationContextPreservingTask<TestFailedException?> call = probe.Run(() => FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            PendingAction, cancellation, timeout, FixtureMethod, null, Canceled, TimedOut));
        Task<TestFailedException?> task = AsTask(call);

        task.IsCompleted.Should().BeFalse();

        actionGate.SetResult(true);
        await WaitFor(task);

        (await task).Should().BeNull();
        probe.PostCount.Should().Be(0, "the cooperatively cancellable fixture must not be awaited on the caller's synchronization context");

        async SynchronizationContextPreservingTask PendingAction()
            => await actionGate.Task.ConfigureAwait(false);
    }

    private static void FixtureMarker()
    {
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
