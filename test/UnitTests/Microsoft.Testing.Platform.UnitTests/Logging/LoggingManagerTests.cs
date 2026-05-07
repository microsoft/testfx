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
        _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
    }

    [TestMethod]
    public async Task BuildAsync_NoProviders_ReturnsEmptyLoggerFactory()
    {
        LoggingManager manager = new();

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        Assert.IsNotNull(factory);
    }

    [TestMethod]
    public async Task BuildAsync_NonExtensionProvider_IsIncluded()
    {
        Mock<ILoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger("cat");

        mockProvider.Verify(p => p.CreateLogger("cat"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_ExtensionProviderEnabled_IsIncluded()
    {
        Mock<IEnabledLoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger("cat");

        mockProvider.Verify(p => p.CreateLogger("cat"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_ExtensionProviderDisabled_IsExcluded()
    {
        Mock<IEnabledLoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger("cat");

        mockProvider.Verify(p => p.CreateLogger("cat"), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_InitializableProvider_IsInitialized()
    {
        Mock<IInitializableLoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_ExtensionDisabled_IsNotInitialized()
    {
        Mock<IEnabledInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_MultipleProviders_AllIncluded()
    {
        Mock<ILoggerProvider> mockProvider1 = new();
        Mock<ILoggerProvider> mockProvider2 = new();
        Mock<ILogger> mockLogger = new();
        mockProvider1.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        mockProvider2.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider1.Object);
        manager.AddProvider((_, _) => mockProvider2.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger("cat");

        mockProvider1.Verify(p => p.CreateLogger("cat"), Times.Once);
        mockProvider2.Verify(p => p.CreateLogger("cat"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_MixedEnabledAndDisabledExtensions_OnlyEnabledIncluded()
    {
        Mock<IEnabledLoggerProvider> mockEnabled = new();
        Mock<IEnabledLoggerProvider> mockDisabled = new();
        Mock<ILogger> mockLogger = new();
        mockEnabled.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockEnabled.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        mockDisabled.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockEnabled.Object);
        manager.AddProvider((_, _) => mockDisabled.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger("cat");

        mockEnabled.Verify(p => p.CreateLogger("cat"), Times.Once);
        mockDisabled.Verify(p => p.CreateLogger("cat"), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_PassesLogLevelAndServiceProviderToFactory()
    {
        LogLevel capturedLogLevel = LogLevel.None;
        IServiceProvider? capturedServiceProvider = null;

        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((logLevel, sp) =>
        {
            capturedLogLevel = logLevel;
            capturedServiceProvider = sp;
            return mockProvider.Object;
        });

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
    }

    [TestMethod]
    public void AddProvider_NullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }
}

internal interface IEnabledLoggerProvider : ILoggerProvider, IExtension;

internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;

internal interface IEnabledInitializableLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;
