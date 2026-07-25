// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class ResourceLockManagerTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static UnitTestElement CreateElement(string methodName, params ResourceLockInfo[] locks)
        => new(new TestMethod(methodName, "DummyClass", "DummyAssembly", displayName: null))
        {
            ResourceLocks = locks.Length == 0 ? null : locks,
        };

    public void GetChunkLocks_WhenNoElementDeclaresLocks_ReturnsEmpty()
    {
        IReadOnlyList<ResourceLockInfo> result = ResourceLockManager.GetChunkLocks(
        [
            CreateElement("A"),
            CreateElement("B"),
        ]);

        result.Should().BeEmpty();
    }

    public void EncodeDecode_RoundTripsBothModes()
    {
        var readDecoded = ResourceLockInfo.Decode(ResourceLockInfo.Encode(new ResourceLockInfo("some:key", ResourceAccessMode.Read)));
        readDecoded.Resource.Should().Be("some:key");
        readDecoded.Mode.Should().Be(ResourceAccessMode.Read);

        var writeDecoded = ResourceLockInfo.Decode(ResourceLockInfo.Encode(new ResourceLockInfo("some:key", ResourceAccessMode.ReadWrite)));
        writeDecoded.Resource.Should().Be("some:key");
        writeDecoded.Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void Decode_WhenPrefixIsUnrecognized_FailsClosedToReadWrite()
    {
        // Truncated, corrupted, or future-version payloads must never silently decode to the weaker
        // 'Read' mode, which would turn a serialization glitch into a race.
        ResourceLockInfo.Decode(string.Empty).Mode.Should().Be(ResourceAccessMode.ReadWrite);
        ResourceLockInfo.Decode("Xkey").Mode.Should().Be(ResourceAccessMode.ReadWrite);
        ResourceLockInfo.Decode("key-without-prefix").Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void GetChunkLocks_MergesDistinctKeys_SortedOrdinally()
    {
        IReadOnlyList<ResourceLockInfo> result = ResourceLockManager.GetChunkLocks(
        [
            CreateElement("A", new ResourceLockInfo("zeta", ResourceAccessMode.Read)),
            CreateElement("B", new ResourceLockInfo("alpha", ResourceAccessMode.Read)),
        ]);

        result.Select(l => l.Resource).Should().ContainInOrder("alpha", "zeta");
    }

    public void GetChunkLocks_ReadWriteWinsOverRead_ForSameKey()
    {
        IReadOnlyList<ResourceLockInfo> result = ResourceLockManager.GetChunkLocks(
        [
            CreateElement("A", new ResourceLockInfo("shared", ResourceAccessMode.Read)),
            CreateElement("B", new ResourceLockInfo("shared", ResourceAccessMode.ReadWrite)),
        ]);

        result.Should().ContainSingle();
        result[0].Resource.Should().Be("shared");
        result[0].Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void GetChunkLocks_IsCaseSensitiveOrdinal_TreatsDifferentCasingAsDistinct()
    {
        IReadOnlyList<ResourceLockInfo> result = ResourceLockManager.GetChunkLocks(
        [
            CreateElement("A", new ResourceLockInfo("Key", ResourceAccessMode.ReadWrite)),
            CreateElement("B", new ResourceLockInfo("key", ResourceAccessMode.ReadWrite)),
        ]);

        result.Select(l => l.Resource).Should().ContainInOrder("Key", "key");
    }

    public async Task ExecuteWithLocksAsync_ConflictingWriters_AreSerialized()
    {
        var manager = new ResourceLockManager();
        IReadOnlyList<ResourceLockInfo> locks = [new ResourceLockInfo("shared", ResourceAccessMode.ReadWrite)];

        int concurrent = 0;
        int maxConcurrent = 0;
        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = manager.ExecuteWithLocksAsync(locks, async () =>
        {
            int now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxConcurrent, now);
            firstEntered.SetResult(true);
            await releaseFirst.Task;
            Interlocked.Decrement(ref concurrent);
        }, CancellationToken.None);

        await WaitFor(firstEntered.Task);

        Task second = manager.ExecuteWithLocksAsync(locks, () =>
        {
            int now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxConcurrent, now);
            Interlocked.Decrement(ref concurrent);
            return Task.CompletedTask;
        }, CancellationToken.None);

        // The second writer cannot run while the first holds the exclusive lock.
        second.IsCompleted.Should().BeFalse();

        releaseFirst.SetResult(true);
        await WaitFor(Task.WhenAll(first, second));

        maxConcurrent.Should().Be(1, "two ReadWrite locks on the same key never overlap");
    }

    public async Task ExecuteWithLocksAsync_DistinctKeys_RunConcurrently()
    {
        var manager = new ResourceLockManager();

        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = manager.ExecuteWithLocksAsync(
            [new ResourceLockInfo("a", ResourceAccessMode.ReadWrite)],
            async () =>
            {
                firstEntered.SetResult(true);
                await releaseFirst.Task;
            },
            CancellationToken.None);

        await WaitFor(firstEntered.Task);

        // A lock on an unrelated key must proceed immediately.
        Task second = manager.ExecuteWithLocksAsync(
            [new ResourceLockInfo("b", ResourceAccessMode.ReadWrite)],
            () => Task.CompletedTask,
            CancellationToken.None);

        await WaitFor(second);
        second.Status.Should().Be(TaskStatus.RanToCompletion, "unrelated keys do not contend");

        releaseFirst.SetResult(true);
        await WaitFor(first);
    }

    public async Task ExecuteWithLocksAsync_ReadLocks_RunConcurrently_ButNotWithWriter()
    {
        var manager = new ResourceLockManager();
        IReadOnlyList<ResourceLockInfo> readLock = [new ResourceLockInfo("shared", ResourceAccessMode.Read)];

        var firstReaderEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReaders = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReaderEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task firstReader = manager.ExecuteWithLocksAsync(readLock, async () =>
        {
            firstReaderEntered.SetResult(true);
            await releaseReaders.Task;
        }, CancellationToken.None);

        await WaitFor(firstReaderEntered.Task);

        Task secondReader = manager.ExecuteWithLocksAsync(readLock, async () =>
        {
            secondReaderEntered.SetResult(true);
            await releaseReaders.Task;
        }, CancellationToken.None);

        // Two readers on the same key run concurrently.
        await WaitFor(secondReaderEntered.Task);

        // A writer on the same key is blocked while readers are active.
        Task writer = manager.ExecuteWithLocksAsync(
            [new ResourceLockInfo("shared", ResourceAccessMode.ReadWrite)],
            () => Task.CompletedTask,
            CancellationToken.None);
        writer.IsCompleted.Should().BeFalse("a writer waits for all readers to release");

        releaseReaders.SetResult(true);
        await WaitFor(Task.WhenAll(firstReader, secondReader, writer));
    }

    private static async Task<T> WaitFor<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        return await task;
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current = Volatile.Read(ref target);
        while (value > current)
        {
            int previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }
}
