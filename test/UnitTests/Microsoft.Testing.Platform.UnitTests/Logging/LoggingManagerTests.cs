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
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<IMonitor> _mockMonitor = new();

    public LoggingManagerTests()
    {
        _mockMonitor.Setup(m => m.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
    }

    [TestMethod]
    public void AddProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_WithNoProviders_ReturnsLoggerFactory()
    {
        LoggingManager manager = new();

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        Assert.IsNotNull(factory);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionProvider_IncludesProvider()
    {
        Mock<ILoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionProvider_IncludesProvider()
    {
        Mock<ILoggerProviderAndExtension> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_ExcludesProvider()
    {
        Mock<ILoggerProviderAndExtension> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionInitializableProvider_CallsInitializeAsync()
    {
        Mock<ILoggerProviderAndInitializable> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_DoesNotCallInitializeAsync()
    {
        Mock<ILoggerProviderExtensionAndInitializable> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionProvider_CallsInitializeAsync()
    {
        Mock<ILoggerProviderExtensionAndInitializable> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_PassesCorrectLogLevelToFactory()
    {
        LogLevel capturedLogLevel = LogLevel.None;
        Mock<ILoggerProvider> mockProvider = new();

        LoggingManager manager = new();
        manager.AddProvider((logLevel, _) =>
        {
            capturedLogLevel = logLevel;
            return mockProvider.Object;
        });

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_PassesServiceProviderToFactory()
    {
        IServiceProvider? capturedServiceProvider = null;
        Mock<ILoggerProvider> mockProvider = new();

        LoggingManager manager = new();
        manager.AddProvider((_, sp) =>
        {
            capturedServiceProvider = sp;
            return mockProvider.Object;
        });

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithMultipleProviders_AllIncluded()
    {
        Mock<ILogger> mockLogger = new();

        Mock<ILoggerProvider> mockProvider1 = new();
        mockProvider1.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        Mock<ILoggerProvider> mockProvider2 = new();
        mockProvider2.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider1.Object);
        manager.AddProvider((_, _) => mockProvider2.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        mockProvider1.Verify(p => p.CreateLogger("test"), Times.Once);
        mockProvider2.Verify(p => p.CreateLogger("test"), Times.Once);
        DisposeFactory(factory);
    }

    [TestMethod]
    public async Task BuildAsync_WithMixedEnabledDisabledExtensionProviders_OnlyIncludesEnabled()
    {
        Mock<ILogger> mockLogger = new();

        Mock<ILoggerProviderAndExtension> enabledProvider = new();
        enabledProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        enabledProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        Mock<ILoggerProviderAndExtension> disabledProvider = new();
        disabledProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => enabledProvider.Object);
        manager.AddProvider((_, _) => disabledProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        _ = factory.CreateLogger("test");
        enabledProvider.Verify(p => p.CreateLogger("test"), Times.Once);
        disabledProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
        DisposeFactory(factory);
    }

    private static void DisposeFactory(ILoggerFactory factory)
        => (factory as IDisposable)?.Dispose();
}

internal interface ILoggerProviderAndExtension : ILoggerProvider, IExtension;

internal interface ILoggerProviderAndInitializable : ILoggerProvider, IAsyncInitializableExtension;

internal interface ILoggerProviderExtensionAndInitializable : ILoggerProvider, IExtension, IAsyncInitializableExtension;
