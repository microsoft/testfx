// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggingManagerTests
{
    private const string CategoryName = "category";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<IMonitor> _mockMonitor = new();

    public LoggingManagerTests()
    {
        _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
    }

    [TestMethod]
    public async Task BuildAsync_WithNoProviders_ReturnsEmptyLoggerFactory()
    {
        LoggingManager manager = new();

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        ILogger logger = factory.CreateLogger(CategoryName);

        Assert.IsTrue(logger.IsEnabled(LogLevel.Information));
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
        factory.CreateLogger(CategoryName);

        mockProvider.Verify(p => p.CreateLogger(CategoryName), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_InvokesFactoryWithCorrectLogLevelAndServiceProvider()
    {
        Mock<ILoggerProvider> mockProvider = new();
        LogLevel? capturedLogLevel = null;
        IServiceProvider? capturedServiceProvider = null;

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
    public async Task BuildAsync_WithEnabledExtensionProvider_IncludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        Mock<ILogger> mockLogger = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger(CategoryName);

        mockProvider.Verify(p => p.CreateLogger(CategoryName), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_ExcludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger(CategoryName);

        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_CallsInitializeOnInitializableProvider()
    {
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_DoesNotCallInitializeOnDisabledExtensionProvider()
    {
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_CallsInitializeOnEnabledExtensionProvider()
    {
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithMultipleProviders_IncludesAll()
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
        factory.CreateLogger(CategoryName);

        mockProvider1.Verify(p => p.CreateLogger(CategoryName), Times.Once);
        mockProvider2.Verify(p => p.CreateLogger(CategoryName), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithMixedEnabledAndDisabled_IncludesOnlyEnabled()
    {
        Mock<IExtensionLoggerProvider> mockEnabled = new();
        Mock<IExtensionLoggerProvider> mockDisabled = new();
        Mock<ILogger> mockLogger = new();
        mockEnabled.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockEnabled.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        mockDisabled.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockEnabled.Object);
        manager.AddProvider((_, _) => mockDisabled.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        factory.CreateLogger(CategoryName);

        mockEnabled.Verify(p => p.CreateLogger(CategoryName), Times.Once);
        mockDisabled.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void AddProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_WithLogLevel_LoggerFiltersLowerLevels()
    {
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);
        ILogger logger = factory.CreateLogger(CategoryName);

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
    }
}
