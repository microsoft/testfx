// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
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

    public void Decode_WhenPrefixIsUnrecognized_PreservesTheWholeKey()
    {
        // Only a prefix we actually wrote may be consumed. Stripping unconditionally would rewrite an
        // unrecognized payload into a different key, so it would silently stop conflicting with the key it
        // was meant to name - exclusive, but guarding the wrong resource.
        ResourceLockInfo.Decode("key-without-prefix").Resource.Should().Be("key-without-prefix");
        ResourceLockInfo.Decode("Xkey").Resource.Should().Be("Xkey");
        ResourceLockInfo.Decode(string.Empty).Resource.Should().BeEmpty();
    }

    public void Decode_WhenPayloadIsPrefixOnly_FailsClosedAndKeepsThePayload()
    {
        // Encode never emits a bare prefix, because [ResourceLock] rejects empty keys - so "R" is truncated
        // transport data. Consuming the prefix would decode it into an empty *shared* lock, which fails open
        // and invents a key the attribute forbids.
        var readOnlyPrefix = ResourceLockInfo.Decode("R");
        readOnlyPrefix.Mode.Should().Be(ResourceAccessMode.ReadWrite);
        readOnlyPrefix.Resource.Should().Be("R");

        var writeOnlyPrefix = ResourceLockInfo.Decode("W");
        writeOnlyPrefix.Mode.Should().Be(ResourceAccessMode.ReadWrite);
        writeOnlyPrefix.Resource.Should().Be("W");
    }

    public void Constructor_NormalizesUndefinedModeToReadWrite()
    {
        // 'Mode' is publicly settable, so (ResourceAccessMode)42 is valid attribute syntax. It must not be
        // carried through as a third state: strongest-mode merging and encoding both key off 'Read', so an
        // undefined value left as-is would be treated as shared and fail open.
        var info = new ResourceLockInfo("key", (ResourceAccessMode)42);

        info.Mode.Should().Be(ResourceAccessMode.ReadWrite);
        ResourceLockInfo.Encode(info).Should().StartWith("W");
        ResourceLockInfo.Decode(ResourceLockInfo.Encode(info)).Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void GetChunkLocks_WhenUndefinedModeMeetsRead_ResultIsExclusive()
    {
        // An undefined mode must not leave an existing 'Read' in place during the strongest-mode merge.
        IReadOnlyList<ResourceLockInfo> result = ResourceLockManager.GetChunkLocks(
        [
            CreateElement("A", new ResourceLockInfo("shared", ResourceAccessMode.Read)),
            CreateElement("B", new ResourceLockInfo("shared", (ResourceAccessMode)42)),
        ]);

        result.Should().ContainSingle();
        result[0].Mode.Should().Be(ResourceAccessMode.ReadWrite);
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

    public async Task ExecuteWithLocksAsync_WhenTestRunIsCanceled_UnblocksParkedWaiters()
    {
        // Run cancellation is usually requested *because* something is stuck - and the stuck test is the one
        // holding the lock, so every contending chunk is parked behind it. If TestRunCancellationToken hands
        // out a token that its own Cancel() cannot signal, those waiters never wake and the scheduler workers
        // stay wedged for the rest of the run.
        var manager = new ResourceLockManager();

        // Construct it the way MSTestEngine does, with a host-provided token. That is the configuration in
        // which the bug bites: Cancel() signals only the internally-owned source, so handing out the original
        // host token instead would produce a token that Cancel() can never signal.
        using var hostCancellationSource = new CancellationTokenSource();
        var testRunCancellationToken = new TestRunCancellationToken(hostCancellationSource.Token);
        IReadOnlyList<ResourceLockInfo> locks = [new ResourceLockInfo("shared", ResourceAccessMode.ReadWrite)];

        var holderEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHolder = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task holder = manager.ExecuteWithLocksAsync(locks, async () =>
        {
            holderEntered.SetResult(true);
            await releaseHolder.Task;
        }, CancellationToken.None);

        await WaitFor(holderEntered.Task);

        Task blocked = manager.ExecuteWithLocksAsync(locks, () => Task.CompletedTask, testRunCancellationToken.CancellationToken);
        blocked.IsCompleted.Should().BeFalse("the second chunk is blocked by the exclusive lock");

        testRunCancellationToken.Cancel();

        Task completed = await Task.WhenAny(blocked, Task.Delay(Timeout));
        completed.Should().BeSameAs(blocked, "cancelling the test run must unblock waiters parked on a resource lock");

        Func<Task> act = async () => await blocked;
        await act.Should().ThrowAsync<OperationCanceledException>();

        releaseHolder.SetResult(true);
        await WaitFor(holder);
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
