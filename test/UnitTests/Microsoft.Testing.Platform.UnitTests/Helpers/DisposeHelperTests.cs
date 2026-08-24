// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DisposeHelperTests
{
    [TestMethod]
    public async Task DisposeAsync_Null_DoesNothing()
        => await DisposeHelper.DisposeAsync(null);

    [TestMethod]
    public async Task DisposeAsync_UnsupportedObject_DoesNothing()
        => await DisposeHelper.DisposeAsync(new object());

    [TestMethod]
    public async Task DisposeAsync_CleanableDisposable_CleansUpBeforeDisposing()
    {
        RecordingSyncResource resource = new();

        await DisposeHelper.DisposeAsync(resource);

        Assert.AreSequenceEqual(
            new[] { nameof(RecordingSyncResource.CleanupAsync), nameof(RecordingSyncResource.Dispose) },
            resource.Invocations);
    }

    [TestMethod]
    public async Task DisposeAsync_CleanupThrows_PropagatesExceptionWithoutDisposing()
    {
        InvalidOperationException expectedException = new("Cleanup failed.");
        RecordingSyncResource resource = new(() => Task.FromException(expectedException));

        InvalidOperationException actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => DisposeHelper.DisposeAsync(resource));

        Assert.AreSame(expectedException, actualException);
        Assert.AreSequenceEqual(new[] { nameof(RecordingSyncResource.CleanupAsync) }, resource.Invocations);
    }

    [TestMethod]
    public async Task DisposeAsync_SynchronousDisposeThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("Dispose failed.");
        RecordingSyncResource resource = new(dispose: () => throw expectedException);

        InvalidOperationException actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => DisposeHelper.DisposeAsync(resource));

        Assert.AreSame(expectedException, actualException);
        Assert.AreSequenceEqual(
            new[] { nameof(RecordingSyncResource.CleanupAsync), nameof(RecordingSyncResource.Dispose) },
            resource.Invocations);
    }

    [TestMethod]
    public async Task DisposeAsync_CalledTwice_InvokesCleanupAndDisposeTwice()
    {
        RecordingSyncResource resource = new();

        await DisposeHelper.DisposeAsync(resource);
        await DisposeHelper.DisposeAsync(resource);

        Assert.AreSequenceEqual(
            new[]
            {
                nameof(RecordingSyncResource.CleanupAsync),
                nameof(RecordingSyncResource.Dispose),
                nameof(RecordingSyncResource.CleanupAsync),
                nameof(RecordingSyncResource.Dispose),
            },
            resource.Invocations);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_AsyncDisposable_CleansUpThenPrefersAsyncDispose()
    {
        RecordingAsyncResource resource = new();

        await DisposeHelper.DisposeAsync(resource);

        Assert.AreSequenceEqual(
            new[] { nameof(RecordingAsyncResource.CleanupAsync), nameof(RecordingAsyncResource.DisposeAsync) },
            resource.Invocations);
    }

    [TestMethod]
    public async Task DisposeAsync_AsyncDisposeIsCanceled_PropagatesCancellationWithoutSynchronousDispose()
    {
        CancellationToken cancellationToken = new(canceled: true);
        OperationCanceledException expectedException = new(cancellationToken);
        RecordingAsyncResource resource = new(() => ValueTask.FromException(expectedException));

        OperationCanceledException actualException = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => DisposeHelper.DisposeAsync(resource));

        Assert.AreSame(expectedException, actualException);
        Assert.AreEqual(cancellationToken, actualException.CancellationToken);
        Assert.AreSequenceEqual(
            new[] { nameof(RecordingAsyncResource.CleanupAsync), nameof(RecordingAsyncResource.DisposeAsync) },
            resource.Invocations);
    }
#endif

    private sealed class RecordingSyncResource : IAsyncCleanableExtension, IDisposable
    {
        private readonly Func<Task> _cleanupAsync;
        private readonly Action _dispose;

        public RecordingSyncResource(Func<Task>? cleanupAsync = null, Action? dispose = null)
        {
            _cleanupAsync = cleanupAsync ?? (() => Task.CompletedTask);
            _dispose = dispose ?? (() => { });
        }

        public List<string> Invocations { get; } = [];

        public Task CleanupAsync()
        {
            Invocations.Add(nameof(CleanupAsync));
            return _cleanupAsync();
        }

        public void Dispose()
        {
            Invocations.Add(nameof(Dispose));
            _dispose();
        }
    }

#if NETCOREAPP
    private sealed class RecordingAsyncResource : IAsyncCleanableExtension, IAsyncDisposable, IDisposable
    {
        private readonly Func<ValueTask> _disposeAsync;

        public RecordingAsyncResource(Func<ValueTask>? disposeAsync = null)
            => _disposeAsync = disposeAsync ?? (() => ValueTask.CompletedTask);

        public List<string> Invocations { get; } = [];

        public Task CleanupAsync()
        {
            Invocations.Add(nameof(CleanupAsync));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Invocations.Add(nameof(DisposeAsync));
            return _disposeAsync();
        }

        public void Dispose()
            => Invocations.Add(nameof(Dispose));
    }
#endif
}
