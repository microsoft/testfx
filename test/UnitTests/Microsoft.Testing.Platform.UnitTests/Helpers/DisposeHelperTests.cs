// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DisposeHelperTests
{
    [TestMethod]
    public async Task DisposeAsync_WithNull_DoesNotThrow()
        => await DisposeHelper.DisposeAsync(null);

    [TestMethod]
    public async Task DisposeAsync_WithAsyncCleanableExtension_CallsCleanupAsync()
    {
        AsyncCleanableOnly obj = new();

        await DisposeHelper.DisposeAsync(obj);

        Assert.IsTrue(obj.CleanupCalled);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_WithAsyncDisposable_CallsDisposeAsyncNotDispose()
    {
        AsyncDisposableAndDisposable obj = new();

        await DisposeHelper.DisposeAsync(obj);

        Assert.IsTrue(obj.DisposeAsyncCalled);
        Assert.IsFalse(obj.DisposeCalled);
    }

    [TestMethod]
    public async Task DisposeAsync_WithOnlySyncDisposable_CallsDispose()
    {
        DisposableOnly obj = new();

        await DisposeHelper.DisposeAsync(obj);

        Assert.IsTrue(obj.DisposeCalled);
    }
#else
    [TestMethod]
    public async Task DisposeAsync_WithSyncDisposable_CallsDispose()
    {
        DisposableOnly obj = new();

        await DisposeHelper.DisposeAsync(obj);

        Assert.IsTrue(obj.DisposeCalled);
    }
#endif

    [TestMethod]
    public async Task DisposeAsync_WithBothCleanableAndDisposable_CallsBoth()
    {
        AsyncCleanableAndDisposable obj = new();

        await DisposeHelper.DisposeAsync(obj);

        Assert.IsTrue(obj.CleanupCalled);
        Assert.IsTrue(obj.DisposeCalled);
    }

    [TestMethod]
    public async Task DisposeAsync_WithObjectImplementingNeitherInterface_DoesNotThrow()
        => await DisposeHelper.DisposeAsync(new object());

    private sealed class AsyncCleanableOnly : IAsyncCleanableExtension
    {
        public bool CleanupCalled { get; private set; }

        public Task CleanupAsync()
        {
            CleanupCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class DisposableOnly : IDisposable
    {
        public bool DisposeCalled { get; private set; }

        public void Dispose() => DisposeCalled = true;
    }

#if NETCOREAPP
    private sealed class AsyncDisposableAndDisposable : IAsyncDisposable, IDisposable
    {
        public bool DisposeAsyncCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return default;
        }

        public void Dispose() => DisposeCalled = true;
    }
#endif

    private sealed class AsyncCleanableAndDisposable : IAsyncCleanableExtension, IDisposable
    {
        public bool CleanupCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public Task CleanupAsync()
        {
            CleanupCalled = true;
            return Task.CompletedTask;
        }

        public void Dispose() => DisposeCalled = true;
    }
}
