// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class ResourceLockManagerConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task ExecuteWithLocksAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var manager = new ResourceLockManager();
        var holderGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writerGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ResourceLockInfo[] writeLock = [new ResourceLockInfo("resource", ResourceAccessMode.ReadWrite)];
        ResourceLockInfo[] readLock = [new ResourceLockInfo("resource", ResourceAccessMode.Read)];

        // The holder acquires the lock immediately and then waits on its action, so the action await is pending.
        Task holder = probe.Run(() => manager.ExecuteWithLocksAsync(writeLock, () => holderGate.Task, CancellationToken.None));

        // A reader and then a writer queue behind the holder, so both lock acquisitions are pending.
        Task reader = probe.Run(() => manager.ExecuteWithLocksAsync(readLock, () => readerGate.Task, CancellationToken.None));
        Task writer = probe.Run(() => manager.ExecuteWithLocksAsync(writeLock, () => writerGate.Task, CancellationToken.None));

        holder.IsCompleted.Should().BeFalse();
        reader.IsCompleted.Should().BeFalse();
        writer.IsCompleted.Should().BeFalse();

        holderGate.SetResult(true);
        await WaitFor(holder);
        readerGate.SetResult(true);
        await WaitFor(reader);
        writerGate.SetResult(true);
        await WaitFor(writer);

        probe.PostCount.Should().Be(0, "the lock manager must not capture the caller's synchronization context");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }
}
