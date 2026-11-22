// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class StaThreadHelperConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task RunOnApartmentThreadIfNeededAsync_WithoutAnApartmentRequest_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var actionGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // No apartment is requested, so the action is awaited directly on the calling thread and is still pending.
        Task<int> call = probe.Run(() => StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            null,
            "unused",
            () => actionGate.Task,
            () => -1,
            _ => throw new InvalidOperationException("unexpected wait"),
            (_, _) => -2,
            () => { }));

        call.IsCompleted.Should().BeFalse();

        actionGate.SetResult(5);
        await WaitFor(call);

        (await call).Should().Be(5);
        probe.PostCount.Should().Be(0, "the action must not be awaited on the caller's synchronization context");
    }

    public async Task RunOnApartmentThreadIfNeededAsync_WithAnApartmentRequest_NeverResumesOnTheCapturedSynchronizationContextAfterWaitingForTheThread()
    {
        // Apartment threads only exist on Windows.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var probe = new ContinuationProbe();
        var waitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The probe thread is an MTA thread, so the STA branch is taken. Joining the entry point thread keeps its result
        // deterministic while the gate keeps the wait for the thread pending.
        Task<int> call = probe.Run(() => StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            ApartmentState.STA,
            "sta-entry-point",
            () => Task.FromResult(3),
            () => -1,
            thread =>
            {
                thread.Join();
                return waitGate.Task;
            },
            (_, _) => -2,
            () => { }));

        call.IsCompleted.Should().BeFalse();

        waitGate.SetResult(true);
        await WaitFor(call);

        (await call).Should().Be(3);
        probe.PostCount.Should().Be(0, "waiting for the apartment thread must not resume on the caller's synchronization context");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }
}
