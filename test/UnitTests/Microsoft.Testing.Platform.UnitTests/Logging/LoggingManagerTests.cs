// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggingManagerTests
{
    private readonly Mock<IMonitor> _mockMonitor = new();

    public LoggingManagerTests()
        => _mockMonitor.Setup(m => m.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);

    [TestMethod]
    public async Task BuildAsync_WithNoProviders_ReturnsNonNullFactory()
    {
        LoggingManager manager = new();
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        Assert.IsNotNull(factory);
    }

    [TestMethod]
    public void AddProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_NonExtensionProvider_IsAlwaysIncluded()
    {
        Mock<ILoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_EnabledExtensionProvider_IsIncluded()
    {
        Mock<IEnabledLoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_DisabledExtensionProvider_IsExcluded()
    {
        Mock<IEnabledLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_InitializableProvider_InitializeAsyncIsCalled()
    {
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        _ = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_DisabledExtensionAndInitializable_InitializeAsyncIsNotCalled()
    {
        Mock<IEnabledInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        _ = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_EnabledExtensionAndInitializable_InitializeAsyncIsCalled()
    {
        Mock<IEnabledInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);
        _ = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_MultipleProviders_AllIncluded()
    {
        Mock<ILoggerProvider> mockProvider1 = new();
        Mock<ILoggerProvider> mockProvider2 = new();
        mockProvider1.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        mockProvider2.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider1.Object);
        manager.AddProvider((_, _) => mockProvider2.Object);
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider1.Verify(p => p.CreateLogger("test"), Times.Once);
        mockProvider2.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_MixedEnabledDisabled_OnlyEnabledProviderIsUsed()
    {
        Mock<IEnabledLoggerProvider> enabledProvider = new();
        Mock<IEnabledLoggerProvider> disabledProvider = new();
        enabledProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        enabledProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        disabledProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => enabledProvider.Object);
        manager.AddProvider((_, _) => disabledProvider.Object);
        ILoggerFactory factory = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Trace, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        enabledProvider.Verify(p => p.CreateLogger("test"), Times.Once);
        disabledProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_FactoryReceivesCorrectLogLevel()
    {
        LogLevel capturedLogLevel = default;
        LoggingManager manager = new();
        manager.AddProvider((level, _) =>
        {
            capturedLogLevel = level;
            Mock<ILoggerProvider> p = new();
            p.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            return p.Object;
        });

        _ = await manager.BuildAsync(Mock.Of<IServiceProvider>(), LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
    }

    [TestMethod]
    public async Task BuildAsync_FactoryReceivesCorrectServiceProvider()
    {
        Mock<IServiceProvider> mockServiceProvider = new();
        IServiceProvider? capturedServiceProvider = null;

        LoggingManager manager = new();
        manager.AddProvider((_, sp) =>
        {
            capturedServiceProvider = sp;
            Mock<ILoggerProvider> p = new();
            p.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            return p.Object;
        });

        _ = await manager.BuildAsync(mockServiceProvider.Object, LogLevel.Trace, _mockMonitor.Object);

        Assert.AreSame(mockServiceProvider.Object, capturedServiceProvider);
    }
}

internal interface IEnabledLoggerProvider : ILoggerProvider, IExtension;

internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;

internal interface IEnabledInitializableLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;
