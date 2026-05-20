// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggerFactoryProxyTests
{
    [TestMethod]
    public void CreateLogger_WhenNotInitialized_ThrowsInvalidOperationException()
    {
        LoggerFactoryProxy proxy = new();

        Assert.ThrowsExactly<InvalidOperationException>(() => proxy.CreateLogger("test"));
    }

    [TestMethod]
    public void SetLoggerFactory_WithNull_ThrowsArgumentNullException()
    {
        LoggerFactoryProxy proxy = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => proxy.SetLoggerFactory(null!));
    }

    [TestMethod]
    public void CreateLogger_WhenInitialized_DelegatesToInnerFactory()
    {
        Mock<ILogger> mockLogger = new();
        Mock<ILoggerFactory> mockFactory = new();
        mockFactory.Setup(f => f.CreateLogger("category")).Returns(mockLogger.Object);

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        ILogger result = proxy.CreateLogger("category");

        Assert.AreSame(mockLogger.Object, result);
        mockFactory.Verify(f => f.CreateLogger("category"), Times.Once);
    }

    [TestMethod]
    public void Dispose_WhenNoInnerFactorySet_DoesNotThrow()
    {
        LoggerFactoryProxy proxy = new();

        // Should be a no-op rather than NRE — host shutdown paths call this defensively.
        proxy.Dispose();
    }

    [TestMethod]
    public void Dispose_WhenInnerFactoryIsDisposable_DisposesInnerFactory()
    {
        Mock<ILoggerFactory> mockFactory = new();
        Mock<IDisposable> mockDisposable = mockFactory.As<IDisposable>();

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        proxy.Dispose();

        mockDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public void Dispose_WhenCalledTwice_DisposesInnerFactoryOnlyOnce()
    {
        Mock<ILoggerFactory> mockFactory = new();
        Mock<IDisposable> mockDisposable = mockFactory.As<IDisposable>();

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        proxy.Dispose();
        proxy.Dispose();

        mockDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public void CreateLogger_AfterDispose_ThrowsObjectDisposedException()
    {
        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(Mock.Of<ILoggerFactory>());

        proxy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => proxy.CreateLogger("category"));
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_WhenInnerFactoryIsAsyncDisposable_DisposesAsync()
    {
        Mock<ILoggerFactory> mockFactory = new();
        Mock<IAsyncDisposable> mockAsyncDisposable = mockFactory.As<IAsyncDisposable>();
        mockAsyncDisposable.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        await proxy.DisposeAsync();

        mockAsyncDisposable.Verify(d => d.DisposeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenInnerFactoryIsOnlyDisposable_FallsBackToSyncDispose()
    {
        Mock<ILoggerFactory> mockFactory = new();
        Mock<IDisposable> mockDisposable = mockFactory.As<IDisposable>();

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        await proxy.DisposeAsync();

        mockDisposable.Verify(d => d.Dispose(), Times.Once);
    }
#endif
}
